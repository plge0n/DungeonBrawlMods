// 内容包检查：照搬 DBModKit 的 ContentReader / ContentExprParser / ContentPacks.Load，错误文字和游戏里报的一样。
// 改 ModKit 的读包规则时同步改这里，再跑 node test/run.js 对一遍。
// 浏览器里挂在 window.ContentCheck，node 里 module.exports。
(function (root) {
    "use strict";

    // ---- 和 ModKit 一致的常量 ----

    const Qualities = ["D", "C", "B", "A", "S"];
    const Indicators = ["None", "Range", "Vector", "Target"];
    const RelicValueTypes = ["Normal", "Percent", "PercentConst", "InversePercent"];
    const PassiveTriggers = ["onAdded", "onRemoved", "onHit", "onKill", "onCrit", "onHurt", "onWaveStart", "onCast"];
    // 效果名 → 允许的附加字段
    const Kinds = {
        damage: ["scale", "amount", "radius"],
        heal: [],
        mana: [],
        gold: [],
        addStats: ["seconds"],
        addAttackSpeed: ["seconds"],
        addArmor: ["seconds"],
        effect: ["duration", "scale", "at"],
        animation: ["speed", "duration"],
        notice: ["seconds"],
    };
    const Conditions = ["chance", "hpBelow", "isBoss", "cooldown"];
    const Variables = ["damage", "maxHp", "hp", "mana", "maxMana", "str", "agi", "sta", "attack", "level", "wave"];
    const CustomEquipmentFirstId = 5000000;
    const Lists = ["activeSkills", "passiveSkills", "relics", "equipEffects", "equipment", "heroVariants"];

    class ContentError extends Error {}

    const isObj = t => t !== null && typeof t === "object" && !Array.isArray(t);
    const isNum = t => typeof t === "number";
    const isInt = t => typeof t === "number" && Number.isInteger(t);
    // parseJson 读到 20.0、1e3 这类写法时记下来：Newtonsoft 把它们当小数，整数字段会报错
    const floatLiterals = new WeakMap();
    const isFloatLiteral = (container, key) => !!(floatLiterals.get(container) || new Set()).has(key);
    const markFloat = (container, key) => {
        if (!floatLiterals.has(container)) floatLiterals.set(container, new Set());
        floatLiterals.get(container).add(key);
    };
    // 表单改了字段后调用：新值不再是原文里的写法
    const clearFloat = (container, key) => {
        const set = floatLiterals.get(container);
        if (set) set.delete(key);
    };
    const own = (o, k) => Object.prototype.hasOwnProperty.call(o, k);

    // ---- 表达式（ContentExprParser）----

    // 写错时抛出带 message 的 Error；返回用到的 {n} 序号
    function parseExpr(text) {
        if (text.trim() === "") throw new Error("表达式是空的");
        const s = text;
        let i = 0;
        const used = [];
        const err = what => new Error(`表达式 "${s}" 第 ${i + 1} 个字符：${what}`);
        const skip = () => { while (i < s.length && /\s/.test(s[i])) i++; };
        const eat = c => {
            skip();
            if (i < s.length && s[i] === c) { i++; return true; }
            return false;
        };
        const sum = () => {
            product();
            while (eat("+") || eat("-")) product();
        };
        const product = () => {
            unary();
            while (eat("*") || eat("/")) unary();
        };
        const unary = () => {
            if (eat("-") || eat("+")) return unary();
            atom();
        };
        const atom = () => {
            skip();
            if (i >= s.length) throw err("表达式不完整");
            const c = s[i];
            if (c === "(") {
                i++;
                sum();
                if (!eat(")")) throw err("缺少 )");
                return;
            }
            if (c === "{") {
                const start = ++i;
                while (i < s.length && /[0-9]/.test(s[i])) i++;
                if (i === start || i >= s.length || s[i] !== "}") throw err("{n} 里要写数值的序号，比如 {0}");
                used.push(parseInt(s.substring(start, i), 10));
                i++;
                return;
            }
            if (/[0-9.]/.test(c)) {
                const start = i;
                while (i < s.length && /[0-9.]/.test(s[i])) i++;
                if (!/^([0-9]+\.?[0-9]*|\.[0-9]+)$/.test(s.substring(start, i))) throw err("数字写错了");
                return;
            }
            if (/\p{L}/u.test(c)) {
                const start = i;
                while (i < s.length && (/[\p{L}\p{Nd}]/u.test(s[i]) || s[i] === "_")) i++;
                const name = s.substring(start, i);
                if (Variables.some(v => v.toLowerCase() === name.toLowerCase())) return;
                i = start;
                throw err(`不认识的变量 "${name}"（可以用：${Variables.join("、")}）`);
            }
            throw err(`不认识的字符 '${c}'`);
        };
        sum();
        skip();
        if (i < s.length) throw err("多出来的内容");
        return used;
    }

    // ---- JSON 读取辅助（ContentReader.Obj）：类型不对、多余字段都记成错误 ----

    class Obj {
        constructor(o, where, out) {
            this.o = o;
            this.where = where;
            this.out = out;
            this.used = new Set();
        }

        get keys() { return Object.keys(this.o); }
        has(k) { return own(this.o, k); }
        // 不存在时返回 undefined；JSON 里写的 null 是存在的值
        take(k) {
            this.used.add(k);
            return this.has(k) ? this.o[k] : undefined;
        }
        child(t, k) {
            if (isObj(t)) return new Obj(t, `${this.where}.${k}`, this.out);
            this.error(k, "应该是 { … }");
            throw new ContentError();
        }
        error(k, msg) { this.out.error(`${this.where}.${k}`, msg); }
        done() {
            for (const k of this.keys.filter(k => !this.used.has(k)))
                this.out.error(this.where, `不认识的字段 "${k}"（大小写也要一致）`);
        }
        guard(k, read) {
            const msg = read();
            if (msg) {
                this.error(k, msg);
                throw new ContentError();
            }
        }
        key() {
            const key = this.str("key", "");
            if (key === "" || ![...key].every(c => /^[A-Za-z0-9_]$/.test(c))) {
                this.error("key", `只能用英文字母、数字、下划线："${key}"`);
                throw new ContentError();
            }
            return key;
        }
        str(k, fallback) {
            const t = this.take(k);
            if (t === undefined) return fallback;
            if (typeof t === "string") return t;
            this.error(k, "应该是文字（加引号）");
            return fallback;
        }
        num(k, fallback) {
            const t = this.take(k);
            if (t === undefined) return fallback;
            if (isNum(t)) return t;
            this.error(k, "应该是数字");
            return fallback;
        }
        int(k, fallback) {
            const t = this.take(k);
            if (t === undefined) return fallback;
            if (isInt(t) && !isFloatLiteral(this.o, k)) return t;
            this.error(k, "应该是整数");
            return fallback;
        }
        optInt(k) { return this.has(k) ? this.int(k, 0) : null; }
        bool(k, fallback) {
            const t = this.take(k);
            if (t === undefined) return fallback;
            if (typeof t === "boolean") return t;
            this.error(k, "应该是 true 或 false");
            return fallback;
        }
        // C# 的 Enum.TryParse：区分大小写，也认得序号
        enumOf(k, names, fallback) {
            const s = this.str(k, null);
            if (s === null) return fallback;
            const v = s.trim();
            if (names.includes(v)) return v;
            if (/^[+-]?[0-9]+$/.test(v) && +v >= 0 && +v < names.length) return names[+v];
            this.error(k, `可以写：${names.join("、")}`);
            return fallback;
        }
        nums(k) {
            const t = this.take(k);
            if (t === undefined) return [];
            if (Array.isArray(t) && t.every(isNum)) return t;
            this.error(k, "应该是数字列表，比如 [20, 5]");
            return [];
        }
        strs(k) {
            const t = this.take(k);
            if (t === undefined) return null;
            if (Array.isArray(t) && t.every(x => typeof x === "string")) return t;
            this.error(k, "应该是文字列表，比如 [\"Area\", \"Buff\"]");
            return null;
        }
        expr(k) {
            const t = this.take(k);
            if (t === undefined || isNum(t)) return;
            if (typeof t === "string") {
                try {
                    this.out.exprs.push({ where: `${this.where}.${k}`, text: t, values: parseExpr(t) });
                } catch (e) {
                    this.error(k, e.message);
                }
                return;
            }
            this.error(k, "应该是数字或表达式（比如 \"damage * {1} / 100\"）");
        }
    }

    // ---- 条目 ----

    // ctx.icon(值) → 包里的图片找不到时返回错误文字，否则 null
    function readActive(o, ctx) {
        const d = { key: o.key() };
        d.name = o.str("name", "");
        o.str("description", "");
        o.enumOf("quality", Qualities, "C");
        o.enumOf("indicator", Indicators, "Range");
        o.int("cost", 20);
        o.num("cooldown", 8);
        o.num("castingRange", 8);
        o.num("range", 3);
        d.duration = o.num("duration", 0);
        o.num("interval", 0);
        o.int("damageBase", 0);
        o.num("damageValue", 0);
        o.int("damageType", 0);
        o.int("charges", 0);
        o.num("chargeTime", 0);
        o.nums("values");
        o.bool("canAuto", true);
        o.bool("inSkillBook", true);
        o.strs("tags");
        icon(o, ctx);
        if (d.name === "") o.error("name", "技能要有名字");

        actions(o, "onCast", true);
        actions(o, "onEnd", true);
        const tick = o.take("onTick");
        if (tick !== undefined) {
            const t = o.child(tick, "onTick");
            if (t.num("every", 0) <= 0) t.error("every", "要写大于 0 的间隔秒数");
            actions(t, "do", true);
            t.done();
            if (d.duration <= 0) o.error("onTick", "一次性技能（duration 为 0）不会有 onTick，给技能设置 duration");
        }
        const anim = o.take("animation");
        if (anim !== undefined) {
            const a = o.child(anim, "animation");
            if (a.str("game", null) === null) a.error("game", "写游戏动作的名字，比如 Frank_RPG_2Hand_Skill01_WhirlWind");
            a.num("speed", 1);
            if (a.has("duration")) a.num("duration", 0);
            a.done();
        }
        return d;
    }

    function readPassive(o, ctx) {
        const d = { key: o.key() };
        d.name = o.str("name", "");
        o.str("description", "");
        o.enumOf("quality", Qualities, "C");
        o.nums("values");
        o.nums("levelUp");
        o.bool("inSkillBook", true);
        o.bool("repeatable", false);
        o.strs("tags");
        icon(o, ctx);
        if (d.name === "") o.error("name", "被动要有名字");
        triggers(o);
        return d;
    }

    // 被动、遗物、装备特效共用的触发器
    function triggers(o) {
        for (const k of PassiveTriggers) actions(o, k, false);
        const tick = o.take("onTick");
        if (tick !== undefined) {
            const t = o.child(tick, "onTick");
            if (t.num("every", 0) <= 0) t.error("every", "要写大于 0 的间隔秒数");
            actions(t, "do", false);
            t.done();
        }
    }

    function readRelic(o, ctx) {
        const d = { key: o.key() };
        d.name = o.str("name", "");
        o.str("description", "");
        o.enumOf("quality", Qualities, "C");
        o.nums("values");
        o.nums("levelUp");
        const types = o.strs("valueTypes") || [];
        o.int("maxLevel", -1);
        o.bool("unique", true);
        o.bool("inPool", true);
        icon(o, ctx);
        if (d.name === "") o.error("name", "遗物要有名字");
        for (const t of types.filter(t => !RelicValueTypes.includes(t)))
            o.error("valueTypes", `不认识 "${t}"（可以用：${RelicValueTypes.join("、")}）`);
        triggers(o);
        return d;
    }

    function readEquipEffect(o) {
        const d = { key: o.key() };
        d.name = o.str("name", "");
        o.str("description", "");
        o.nums("values");
        o.nums("levelUp");
        if (d.name === "") o.error("name", "装备特效要有名字");
        const attach = o.take("attachTo");
        if (attach !== undefined && !(Array.isArray(attach) && attach.every((x, i) => isInt(x) && !isFloatLiteral(attach, i) && x > 0 && x < CustomEquipmentFirstId)))
            o.error("attachTo", "应该是游戏装备编号的列表，比如 [2, 3]");
        triggers(o);
        return d;
    }

    function readEquipment(o, ctx) {
        const d = { key: o.key() };
        d.name = o.str("name", "");
        o.str("description", "");
        o.bool("artifact", false);
        icon(o, ctx);
        if (d.name === "") o.error("name", "装备要有名字");
        const attrs = o.take("attributes");
        if (attrs !== undefined) {
            if (isObj(attrs)) {
                for (const [k, v] of Object.entries(attrs))
                    if (!isNum(v)) o.error(`attributes.${k}`, "应该是数字");
            } else o.error("attributes", "应该是 { \"属性名\": 数值 }，比如 { \"attack\": 80 }");
        }
        d.effects = o.strs("effects") || [];
        const gameEffects = o.strs("gameEffects") || [];
        d.gameEffects = gameEffects;
        d.attributes = isObj(attrs) ? Object.keys(attrs) : [];
        if ((attrs === undefined || isObj(attrs) && Object.keys(attrs).length === 0) && d.effects.length === 0 && gameEffects.length === 0)
            o.error("attributes", "装备至少要有属性或特效");
        return d;
    }

    function readVariant(o) {
        const d = { key: o.key() };
        d.name = o.str("name", "");
        o.str("description", "");
        const baseHero = o.int("baseHero", 0);
        for (const k of ["strength", "agility", "stamina", "strengthGrowth", "agilityGrowth", "staminaGrowth",
            "attack", "armor", "hp", "mp", "hpRecover", "mpRecover"]) o.optInt(k);
        if (o.has("moveSpeed")) o.num("moveSpeed", 0);
        if (o.has("attackSpeed")) o.num("attackSpeed", 0);
        const gameActive = o.optInt("gameActiveSkill");
        const gamePassive = o.optInt("gamePassiveSkill");
        d.modelFolder = o.str("modelFolder", null);
        o.int("workshopModel", 0);
        if (d.name === "") o.error("name", "变体要有名字");
        if (baseHero < 1 || baseHero > 43) o.error("baseHero", "写原英雄的编号（1~43）");
        d.active = o.str("activeSkill", null);
        d.passive = o.str("passiveSkill", null);
        const skills = [d.active, d.passive, gameActive, gamePassive].filter(x => x !== null).length;
        if (skills > 1) o.error("activeSkill", "activeSkill、passiveSkill、gameActiveSkill、gamePassiveSkill 只能写一个");
        return d;
    }

    function icon(o, ctx) {
        const v = o.str("icon", null);
        if (v !== null) o.guard("icon", () => ctx.icon(v));
    }

    // ---- 效果 ----

    function actions(parent, key, inSkill) {
        const t = parent.take(key);
        if (t === undefined) return;
        if (!Array.isArray(t)) {
            parent.error(key, "应该是效果的列表 [ { … }, … ]");
            return;
        }
        t.forEach((item, i) => {
            const where = `${parent.where}.${key}[${i}]`;
            if (!isObj(item)) {
                parent.out.error(where, "效果应该是 { \"heal\": 100 } 这样的对象");
                return;
            }
            const o = new Obj(item, where, parent.out);
            try {
                action(o, inSkill);
                o.done();
            } catch (e) {
                if (!(e instanceof ContentError)) throw e;
            }
        });
    }

    function action(o, inSkill) {
        const kinds = Object.keys(Kinds).filter(k => o.has(k));
        if (kinds.length !== 1) {
            // 整个效果作废，字段都算看过了，不再逐个报"不认识"
            for (const k of o.keys) o.take(k);
            o.out.error(o.where, kinds.length === 0
                ? `没有效果（可以用：${Object.keys(Kinds).join("、")}）；多余的字段：${o.keys.join("、")}`
                : `一个效果只能写一种，这里有 ${kinds.join("、")}，拆成几个`);
            return;
        }
        const kind = kinds[0];
        for (const k of o.keys.filter(k => k !== kind && !Conditions.includes(k) && !Kinds[kind].includes(k))) {
            o.take(k);
            o.error(k, `${kind} 不认识这个字段（可以用：${Kinds[kind].concat(Conditions).join("、")}）`);
        }
        if (o.has("chance")) o.expr("chance");
        if (o.has("hpBelow")) o.expr("hpBelow");
        if (o.has("isBoss")) o.bool("isBoss", false);
        o.num("cooldown", 0);
        if (o.has("seconds")) o.expr("seconds");

        switch (kind) {
            case "damage": {
                if (!inSkill) o.error(kind, "被动和遗物里暂时不能造成伤害（装备特效同样）");
                const mode = o.str(kind, "area");
                if (o.has("scale")) o.expr("scale");
                if (o.has("amount")) o.expr("amount");
                const radius = o.has("radius");
                if (radius) o.expr("radius");
                if (mode === "target") {
                    if (radius) o.error("radius", "\"target\" 用不到 radius");
                } else if (mode !== "area") o.error(kind, `写 "area"（范围）或 "target"（锁定的目标），不认识 "${mode}"`);
                break;
            }
            case "heal":
            case "mana":
            case "gold":
            case "addAttackSpeed":
            case "addArmor":
                o.expr(kind);
                break;
            case "addStats": {
                const stats = o.child(o.take(kind), kind);
                for (const k of ["str", "agi", "sta", "attack"]) if (stats.has(k)) stats.expr(k);
                stats.done();
                break;
            }
            case "effect": {
                const path = o.str(kind, "");
                if (path === "") o.error(kind, "写游戏特效的路径，比如 Effect/SpellThunder");
                else o.out.refs.push({ where: `${o.where}.effect`, type: "effect", value: path });
                if (o.has("duration")) o.expr("duration");
                if (o.has("scale")) o.expr("scale");
                const at = o.str("at", "position");
                if (at !== "position" && at !== "self" && at !== "target") o.error("at", "写 position（技能位置 / 事件目标）、self（自己）或 target（目标）");
                break;
            }
            case "animation":
                if (!inSkill) o.error(kind, "只有主动技能能换动作");
                o.str(kind, "");
                o.num("speed", 1);
                if (o.has("duration")) o.num("duration", 0);
                break;
            case "notice":
                o.str(kind, "");
                break;
        }
    }

    // ---- 一个 JSON 文件（ContentReader.ReadFile）----

    // 返回 { errors: [{where, text}], items: { activeSkills: [{index, key, name, ok, ...}] }, exprs, refs }
    function checkFile(root, label, ctx) {
        const out = {
            errors: [],
            exprs: [],
            refs: [],
            items: {},
            error(where, msg) { this.errors.push({ where, text: `${where}：${msg}` }); },
        };
        if (!isObj(root)) {
            // Newtonsoft 的 JObject.Parse 读到的不是对象时抛异常
            out.errors.push({ where: label, text: `${label}：读不了（最外层应该是 { … }）` });
            return out;
        }
        const top = new Obj(root, label, out);
        const readers = {
            activeSkills: o => readActive(o, ctx),
            passiveSkills: o => readPassive(o, ctx),
            relics: o => readRelic(o, ctx),
            equipEffects: o => readEquipEffect(o),
            equipment: o => readEquipment(o, ctx),
            heroVariants: o => readVariant(o),
        };
        for (const list of Lists) {
            out.items[list] = [];
            const t = top.take(list);
            if (t === undefined) continue;
            if (!Array.isArray(t)) {
                top.error(list, "应该是列表 [ … ]");
                continue;
            }
            t.forEach((jo, i) => {
                const where = `${label}.${list}[${i}]`;
                const entry = { index: i, where, ok: false, key: isObj(jo) && typeof jo.key === "string" ? jo.key : "" };
                out.items[list].push(entry);
                if (!isObj(jo)) {
                    out.error(where, "应该是 { … }");
                    return;
                }
                const before = out.errors.length;
                const o = new Obj(jo, where, out);
                try {
                    Object.assign(entry, readers[list](o));
                    o.done();
                    entry.ok = out.errors.length === before;
                } catch (e) {
                    if (!(e instanceof ContentError)) throw e;
                }
            });
        }
        top.done();
        return out;
    }

    // ---- 整个包（ContentPacks.Load）----

    // pack = { manifest, files: { 相对路径: 解析后的 JSON 或 { parseError } }, exists(相对路径) → bool }
    // 返回 { errors, warnings, files: { 路径: checkFile 的结果 } }
    function checkPack(pack) {
        const m = canonicalManifest(pack.manifest);
        const errors = [];
        const warnings = [];
        const files = {};
        const add = (list, where, text) => list.push({ where, text });

        // 包里的文件只能在包的文件夹里
        const inside = rel => {
            const parts = [];
            for (const p of String(rel || "").replace(/\\/g, "/").split("/")) {
                if (p === "" || p === ".") continue;
                if (p === "..") {
                    if (parts.length === 0) return null;
                    parts.pop();
                } else parts.push(p);
            }
            if (/^[a-zA-Z]:/.test(String(rel)) || String(rel).startsWith("/") || String(rel).startsWith("\\")) return null;
            return parts.join("/");
        };
        const ctx = {
            icon(value) {
                const ext = (/\.[^./\\]*$/.exec(value) || [""])[0].toLowerCase();
                if (ext !== ".png" && ext !== ".jpg" && ext !== ".jpeg") return null; // 游戏图标名
                const path = inside(value);
                if (path === null) return `路径不在内容包文件夹里：${value}`;
                if (!pack.exists(path)) return `找不到图片 ${value}`;
                return null;
            },
        };

        const content = Array.isArray(m.Content) ? m.Content : [];
        if (content.length === 0) add(errors, "dbmod.json", "dbmod.json 的 Content 里没有列出 JSON 文件");
        for (const file of content) {
            const path = inside(file);
            if (path === null) {
                add(errors, "dbmod.json", `路径不在内容包文件夹里：${file}`);
                continue;
            }
            const data = pack.files[path];
            if (data === undefined) {
                add(errors, "dbmod.json", `找不到 ${file}`);
                continue;
            }
            if (data && data.parseError) {
                add(errors, file, `${file}：读不了（${data.parseError}）`);
                continue;
            }
            const r = checkFile(data, file, ctx);
            r.label = file;
            files[path] = r;
            errors.push(...r.errors);
        }

        // 注册阶段：同一个包里重复的键、装备引用的特效、变体引用的技能
        const all = list => Object.values(files).flatMap(r => r.items[list]);
        const registered = {};
        const register = (list, what, label) => {
            const keys = new Set();
            for (const e of all(list).filter(e => e.ok)) {
                if (keys.has(e.key)) {
                    e.ok = false;
                    add(errors, e.where, `${what} ${e.key}：${label} ${e.key} 已经注册过`);
                } else keys.add(e.key);
            }
            registered[list] = keys;
        };
        register("activeSkills", "主动技能", "技能");
        register("passiveSkills", "被动", "被动");
        register("relics", "遗物", "遗物");
        register("equipEffects", "装备特效", "装备特效");
        const equipKeys = new Set();
        for (const e of all("equipment").filter(e => e.ok)) {
            const missing = e.effects.find(k => !registered.equipEffects.has(k));
            if (missing !== undefined) {
                e.ok = false;
                add(errors, e.where, `${e.where}.effects：这个包里没有键为 "${missing}" 的装备特效`);
            } else if (equipKeys.has(e.key)) {
                e.ok = false;
                add(errors, e.where, `装备 ${e.key}：装备 ${e.key} 已经注册过`);
            } else if (pack.catalog && equipmentError(e, pack.catalog)) {
                e.ok = false;
                add(errors, e.where, equipmentError(e, pack.catalog));
            } else equipKeys.add(e.key);
        }
        const variantKeys = new Set();
        for (const e of all("heroVariants").filter(e => e.ok)) {
            let fail = null;
            const folder = e.modelFolder !== null ? inside(e.modelFolder) : null;
            if (e.modelFolder !== null && folder === null) fail = `${e.where}.modelFolder：路径不在内容包文件夹里：${e.modelFolder}`;
            if (!fail && e.active !== null && !registered.activeSkills.has(e.active))
                fail = `${e.where}.activeSkill：这个包里没有键为 "${e.active}" 的主动技能`;
            if (!fail && e.passive !== null && !registered.passiveSkills.has(e.passive))
                fail = `${e.where}.passiveSkill：这个包里没有键为 "${e.passive}" 的被动`;
            if (!fail && variantKeys.has(e.key)) fail = `变体 ${e.key}：变体 ${e.key} 已经注册过`;
            // 游戏里报的是完整路径，这里写包里的相对路径
            if (!fail && folder && pack.existsFolder && !pack.existsFolder(folder))
                fail = `变体 ${e.key}：变体 ${e.key} 的模型文件夹不存在：${e.modelFolder}`;
            if (fail) {
                e.ok = false;
                add(errors, e.where, fail);
            } else variantKeys.add(e.key);
        }

        // ---- 以下是工作台额外的提醒：游戏不报错，但多半是写漏了 ----
        if (m.Type !== "content") add(errors, "dbmod.json", "Type 要写 \"content\"，否则 DBModKit 不会把它当内容包加载");
        if (!m.Id) add(warnings, "dbmod.json", "没写 Id：会用文件夹名，本地和创意工坊里的技能编号会不一样");
        else if (!/^[A-Za-z0-9._+-]+$/.test(m.Id)) add(warnings, "dbmod.json", "Id 只用字母、数字和 . _ - +，其他字符会被换成 _");
        if (!m.Name) add(warnings, "dbmod.json", "没写 Name：游戏里会显示文件夹名");
        if (!m.Version) add(warnings, "dbmod.json", "没写 Version：联机比对版本时当作 0");
        if (m.Format > 1) add(errors, "dbmod.json", "这个内容包需要更新版本的 DBModKit");
        // 旧版 DBModKit 不认识新加的列表，会整段报错：MinModKit 要写够
        const used = list => Object.values(pack.files).some(d => isObj(d) && Array.isArray(d[list]) && d[list].length);
        const needs = [["0.21.0", "内容包"]];
        if (used("relics")) needs.push(["0.22.0", "遗物"]);
        if (Array.isArray(m.Dependencies) && m.Dependencies.length) needs.push(["0.23.0", "依赖"]);
        if (used("equipEffects") || used("equipment")) needs.push(["0.24.0", "装备"]);
        const ver = v => String(v || "0").split(".").map(x => parseInt(x, 10) || 0);
        const less = (a, b) => {
            const x = ver(a), y = ver(b);
            for (let i = 0; i < Math.max(x.length, y.length); i++) if ((x[i] || 0) !== (y[i] || 0)) return (x[i] || 0) < (y[i] || 0);
            return false;
        };
        const need = needs.reduce((a, b) => less(a[0], b[0]) ? b : a);
        if (!m.MinModKit || less(m.MinModKit, need[0]))
            add(warnings, "dbmod.json", `用到了${need[1]}，MinModKit 至少要写 ${need[0]}（玩家的 DBModKit 太旧时会提示更新，而不是报一堆错）`);

        const C = pack.catalog;
        for (const [path, r] of Object.entries(files)) {
            const data = pack.files[path];
            for (const list of Lists) {
                const arr = Array.isArray(data[list]) ? data[list] : [];
                r.items[list].forEach(e => {
                    const item = arr[e.index];
                    if (!isObj(item)) return;
                    const values = Array.isArray(item.values) ? item.values.length : 0;
                    const desc = typeof item.description === "string" ? item.description : "";
                    for (const n of (desc.match(/\{(\d+)\}/g) || []).map(x => +x.slice(1, -1))) {
                        // 主动技能没有 values 时 {0} 是游戏生成的伤害文字
                        if (n >= values && !(list === "activeSkills" && values === 0 && n === 0))
                            add(warnings, e.where, `${e.where}.description：{${n}} 没有对应的数值（values 只有 ${values} 个）`);
                    }
                    if (C) catalogWarnings(list, item, e.where, C, warnings);
                });
            }
            for (const x of r.exprs) {
                const item = itemOf(data, x.where, r.label);
                const count = item && Array.isArray(item.values) ? item.values.length : 0;
                for (const n of x.values)
                    if (n >= count) add(warnings, x.where, `${x.where}：{${n}} 没有对应的数值（values 只有 ${count} 个），算出来是 0`);
            }
            if (C) for (const ref of r.refs)
                if (ref.type === "effect" && !C.effects.includes(ref.value))
                    add(warnings, ref.where, `${ref.where}：游戏代码里没见过特效 "${ref.value}"，确认一下路径`);
        }
        return { errors, warnings, files };
    }

    const ManifestFields = ["Format", "Type", "Id", "Name", "Author", "Version", "Description", "MinModKit", "Assemblies", "Content", "Lang", "Dependencies"];
    // 字段名换成正式的大小写（游戏读 dbmod.json 时不区分大小写）
    function canonicalManifest(m) {
        const out = {};
        if (!isObj(m)) return out;
        for (const [k, v] of Object.entries(m)) out[ManifestFields.find(f => f.toLowerCase() === k.toLowerCase()) || k] = v;
        return out;
    }

    // "skills.json.activeSkills[0].onCast[1].scale" → 对应的条目
    function itemOf(data, where, label) {
        const m = new RegExp("^" + label.replace(/[.*+?^${}()|[\]\\]/g, "\\$&") + "\\.(\\w+)\\[(\\d+)\\]").exec(where);
        return m && Array.isArray(data[m[1]]) ? data[m[1]][+m[2]] : null;
    }

    // 属性名先去空格、按别名换成正式名字（不分大小写），再看在不在游戏的属性表里（区分大小写）
    function normalizeAttribute(name, C) {
        const t = name.trim();
        return C.equipAttributeAliases[t.toLowerCase()] || t;
    }

    function equipmentError(e, C) {
        const bad = e.attributes.find(k => !C.equipAttributes.includes(normalizeAttribute(k, C)));
        if (bad !== undefined) return `装备 ${e.key}：装备 ${e.key} 的 Attributes 里有不认识的属性 "${bad}"（可以用的见 CustomEquipment.AttributeNames）`;
        const effect = e.gameEffects.find(n => !n || /^[0-9-]/.test(n) || !C.equipSkillTypes.includes(n.trim()));
        if (effect !== undefined) return `装备 ${e.key}：装备 ${e.key} 的 GameEffects 里有不认识的特效 "${effect}"（写 EquipSkillType 的名字）`;
        return null;
    }

    function catalogWarnings(list, item, where, C, warnings) {
        const add = (field, text) => warnings.push({ where, text: `${where}.${field}：${text}` });
        const iconOk = v => typeof v !== "string" || /\.(png|jpe?g)$/i.test(v) || C.icons.has(v);
        if (!iconOk(item.icon)) add("icon", `游戏里没找到图标 "${item.icon}"，会显示成空白`);
        if (list === "equipEffects" && Array.isArray(item.attachTo))
            for (const id of item.attachTo.filter(isInt))
                if (!C.equipment.some(e => e.id === id)) add("attachTo", `游戏里没有 ${id} 号装备`);
        if (list === "heroVariants") {
            if (isInt(item.gameActiveSkill) && !C.activeSkills.some(s => s.id === item.gameActiveSkill))
                add("gameActiveSkill", `游戏里没有 ${item.gameActiveSkill} 号主动技能`);
            if (isInt(item.gamePassiveSkill) && !C.passiveSkills.some(s => s.id === item.gamePassiveSkill))
                add("gamePassiveSkill", `游戏里没有 ${item.gamePassiveSkill} 号被动`);
        }
    }

    // ---- 翻译 ----

    const LangPrefix = {
        activeSkills: "skill", passiveSkills: "passive", relics: "relic",
        equipEffects: "equipEffect", equipment: "equip", heroVariants: "hero",
    };

    // ---- 宽松的 JSON：和 Newtonsoft 一样认注释、末尾多余的逗号、单引号和不加引号的字段名；出错时带行号 ----

    function parseJson(text) {
        let i = 0;
        const fail = msg => {
            const before = text.slice(0, i);
            const line = before.split("\n").length;
            const col = i - before.lastIndexOf("\n");
            throw new Error(`第 ${line} 行第 ${col} 列：${msg}`);
        };
        const ws = () => {
            for (;;) {
                while (i < text.length && /\s/.test(text[i])) i++;
                if (text.startsWith("//", i)) {
                    while (i < text.length && text[i] !== "\n") i++;
                } else if (text.startsWith("/*", i)) {
                    const end = text.indexOf("*/", i + 2);
                    if (end < 0) fail("注释没有结束");
                    i = end + 2;
                } else return;
            }
        };
        let floatInt = false; // 刚读的数字是不是 20.0 这种写法
        const value = () => {
            ws();
            floatInt = false;
            const c = text[i];
            if (c === "{") {
                i++;
                const o = {};
                ws();
                while (text[i] !== "}") {
                    ws();
                    let k;
                    if (text[i] === "\"" || text[i] === "'") k = string();
                    else {
                        const m = /^[A-Za-z_$][A-Za-z0-9_$]*/.exec(text.slice(i, i + 200));
                        if (!m) fail("字段名要加双引号");
                        k = m[0];
                        i += k.length;
                    }
                    ws();
                    if (text[i] !== ":") fail("字段名后面要有冒号");
                    i++;
                    o[k] = value();
                    if (floatInt) markFloat(o, k);
                    ws();
                    if (text[i] === ",") {
                        i++;
                        ws();
                    } else if (text[i] !== "}") fail("少了逗号或 }");
                }
                i++;
                return o;
            }
            if (c === "[") {
                i++;
                const a = [];
                ws();
                while (text[i] !== "]") {
                    a.push(value());
                    if (floatInt) markFloat(a, a.length - 1);
                    ws();
                    if (text[i] === ",") {
                        i++;
                        ws();
                    } else if (text[i] !== "]") fail("少了逗号或 ]");
                }
                i++;
                return a;
            }
            if (c === "\"" || c === "'") return string();
            const m = /^(-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?|true|false|null)/.exec(text.slice(i, i + 64));
            if (!m) fail(i >= text.length ? "文件不完整" : `看不懂的内容 "${text.slice(i, i + 10)}"`);
            i += m[0].length;
            const v = JSON.parse(m[0]);
            floatInt = typeof v === "number" && Number.isInteger(v) && /[.eE]/.test(m[0]);
            return v;
        };
        const escapes = { "\"": "\"", "'": "'", "\\": "\\", "/": "/", b: "\b", f: "\f", n: "\n", r: "\r", t: "\t" };
        const string = () => {
            const quote = text[i++];
            let out = "";
            while (i < text.length && text[i] !== quote) {
                if (text[i] === "\\") {
                    const e = text[i + 1];
                    if (e === "u" && /^[0-9a-fA-F]{4}$/.test(text.slice(i + 2, i + 6))) {
                        out += String.fromCharCode(parseInt(text.slice(i + 2, i + 6), 16));
                        i += 6;
                        continue;
                    }
                    if (!own(escapes, e)) fail("文字里的转义写错了");
                    out += escapes[e];
                    i += 2;
                } else out += text[i++];
            }
            if (i >= text.length) fail(`文字没有结束（少了 ${quote}）`);
            i++;
            return out;
        };
        const v = value();
        ws();
        if (i < text.length) fail("后面多出来内容");
        return v;
    }

    const api = {
        parseExpr, parseJson, checkFile, checkPack, clearFloat, canonicalManifest,
        Qualities, Indicators, RelicValueTypes, PassiveTriggers, Kinds, Conditions, Variables, Lists, LangPrefix,
    };
    if (typeof module === "object" && module.exports) module.exports = api;
    else root.ContentCheck = api;
})(typeof window !== "undefined" ? window : globalThis);
