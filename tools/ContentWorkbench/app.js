// 内容包工作台：打开一个内容包文件夹，用表单编辑 dbmod.json、各个 JSON 和翻译，边改边按游戏的规则检查。
// Edge / Chrome 能直接写回文件夹（File System Access API）；其他浏览器只能读，保存时下载 zip。
"use strict";

const C = window.CATALOG;
const V = window.ContentCheck;
C.icons = new Set([...C.activeSkills, ...C.passiveSkills, ...C.relics, ...C.equipment].map(x => x.icon).filter(Boolean));

// ---- 文字 ----

const Lists = {
    activeSkills: { label: "主动技能", one: "主动技能", lead: "放进技能书，玩家学了以后可以释放。" },
    passiveSkills: { label: "被动", one: "被动", lead: "放进技能书，学了以后一直生效，按触发时机执行效果。" },
    relics: { label: "遗物", one: "遗物", lead: "进遗物三选一，可以升级（数值按 levelUp 成长）。" },
    equipEffects: { label: "装备特效", one: "装备特效", lead: "挂到游戏已有的装备上，或给这个包里的新装备用。" },
    equipment: { label: "装备", one: "装备", lead: "全新的装备，只能从神器三选一拿到，所以一般要勾上“进神器三选一”。" },
    heroVariants: { label: "变体英雄", one: "变体英雄", lead: "基于游戏原有的英雄，改属性、换专属技能或换模型。" },
};
const TriggerLabels = {
    onCast: "释放时", onEnd: "技能结束时", onTick: "持续期间每隔几秒",
    onAdded: "获得时", onRemoved: "失去时", onHit: "命中敌人时", onKill: "击杀敌人时", onCrit: "暴击时",
    onHurt: "受到伤害时", onWaveStart: "每波开始时",
};
const PassiveCastLabel = "自己放技能时";
const KindLabels = {
    damage: "造成伤害", heal: "回复生命", mana: "回复法力", gold: "获得金币", addStats: "加属性",
    addAttackSpeed: "加攻速", addArmor: "加护甲", effect: "播放特效", animation: "换动作", notice: "屏幕提示",
};
const ExtraLabels = {
    scale: ["伤害倍率", "乘技能公式伤害，默认 1"], amount: ["固定伤害", "写了就不用公式"], radius: ["半径", "默认技能范围"],
    seconds: ["持续秒数", "不写就跟着技能 / 被动走"], duration: ["时长（秒）", "默认 1"], at: ["位置", ""], speed: ["速度", "默认 1"],
    damage: ["打谁", ""], heal: ["回复量", ""], mana: ["回复量", ""], gold: ["金币", ""], addAttackSpeed: ["攻速", ""], addArmor: ["护甲", ""],
    effect: ["特效路径", ""], animation: ["动作名", ""], notice: ["文字", ""],
    chance: ["几率 %", "不写就是 100"], hpBelow: ["自己血量低于 %", ""], isBoss: ["目标", ""], cooldown: ["内部冷却（秒）", ""],
};
const SkillTags = {
    Area: "范围", Duration: "持续", Charge: "充能", Switch: "开关", Trap: "陷阱",
    Physical: "物理", Magic: "魔法", TrueDamage: "真实伤害", AttackEffect: "攻击特效", Aura: "光环",
    Fire: "火", Ice: "冰", Lightning: "雷", AttrNone: "无属性",
    Burn: "燃烧", Poison: "中毒", Shock: "感电", Slow: "减速", Stun: "眩晕", Status: "异常状态",
    Heal: "治疗", Shield: "护盾", Revive: "复活", LifeSteal: "吸血", Defense: "防御", Buff: "增益", Counterattack: "反击",
    Henshin: "变身", Summon: "召唤", Armed: "武装", Move: "位移", Control: "控制", Economy: "经济", Growth: "成长", Resource: "资源",
};
// 装备属性名的大致含义（游戏表里没有现成的中文）
const AttrLabels = {
    hp: "生命", mp: "法力", attack: "攻击力", attackSpeed: "攻击速度", xixue: "攻击回复", hptouqu: "生命偷取", Armor: "护甲",
    moveSpeed: "移动速度", STR: "力量", STA: "耐力", AGI: "敏捷", baojilv: "暴击率", baojiDamage: "暴击伤害", HPadd: "生命回复",
    MPadd: "法力回复", skillCd: "技能冷却", skillDamage: "技能伤害", breakShield: "破盾", skillBreakShield: "技能破盾",
    allBreakShield: "全能破盾", doge: "闪避", skillReduction: "技能减耗", luck: "幸运", normalAttackAddDamage: "普攻增伤",
    staAllAdd: "耐力加成", strAllAdd: "力量加成", agiAllAdd: "敏捷加成", attackPercent: "攻击力百分比", addDamage: "伤害加成",
    skillNoneDamage: "无属性技能伤害", fireDamage: "火焰伤害", iceDamage: "冰霜伤害", lightDamage: "雷电伤害", skillRange: "技能范围",
    skillTime: "技能持续", attackDistance: "攻击距离", buffDamage: "增益伤害", haloRangeAdd: "光环范围", forgeAdd: "锻造加成",
    maxHpAddPercent: "最大生命百分比", skillExpend: "技能消耗", reduceInjury: "固定减伤", extraDamage: "附加伤害", castSpeed: "施法速度",
    hpAddUpgrade: "苏生", addCallMonster: "召唤物数量", addCallMonsterTime: "召唤物持续", addHenshin: "变身加成",
    addHenshinTime: "变身持续", armedAdd: "武装", hpSecRate: "大恢复", magicXiXue: "法术吸血", effectDamage: "特效伤害",
};
const VariantStats = [
    ["strength", "力量"], ["agility", "敏捷"], ["stamina", "耐力"],
    ["strengthGrowth", "力量成长"], ["agilityGrowth", "敏捷成长"], ["staminaGrowth", "耐力成长"],
    ["attack", "攻击力"], ["armor", "护甲"], ["hp", "生命"], ["mp", "法力"],
    ["hpRecover", "每秒回血"], ["mpRecover", "每秒回蓝"], ["moveSpeed", "移动速度", true], ["attackSpeed", "攻击速度", true],
];
const DamageTypes = ["攻击力", "力量", "敏捷", "耐力", "最大生命"];
const Animations = ["Frank_RPG_2Hand_Skill01_WhirlWind", "Frank_RPG_2Hand_Combo03_All", "Frank_RPG_Dual_Skill04_InfinitySlash"];
// ModKit 的语言代码（游戏的语言顺序），翻译文件名是 <代码>.json
const LangCodes = {
    en: "英语", "zh-TW": "繁体中文", ja: "日语", ko: "韩语", ru: "俄语", fr: "法语", de: "德语", es: "西班牙语", it: "意大利语",
    pt: "葡萄牙语", da: "丹麦语", hu: "匈牙利语", tr: "土耳其语", no: "挪威语", th: "泰语", ro: "罗马尼亚语", nl: "荷兰语",
    vi: "越南语", uk: "乌克兰语", bg: "保加利亚语", id: "印尼语", el: "希腊语", cs: "捷克语", pl: "波兰语", sv: "瑞典语",
    fi: "芬兰语", ar: "阿拉伯语",
};

// ---- 小工具 ----

const $ = s => document.querySelector(s);
function h(tag, attrs, ...children) {
    const el = document.createElement(tag);
    for (const [k, v] of Object.entries(attrs || {})) {
        if (v === undefined || v === null || v === false) continue;
        if (k.startsWith("on")) el.addEventListener(k.slice(2), v);
        else if (k === "class") el.className = v;
        else if (k === "value") el.value = v;
        else if (k === "style") el.style.cssText = v;
        else if (k in el && typeof v !== "string") el[k] = v;
        else el.setAttribute(k, v === true ? "" : v);
    }
    for (const c of children.flat()) if (c !== null && c !== undefined && c !== false) el.append(c instanceof Node ? c : String(c));
    return el;
}
const own = (o, k) => o && Object.prototype.hasOwnProperty.call(o, k);
const isObj = t => t !== null && typeof t === "object" && !Array.isArray(t);
const esc = s => String(s).replace(/[&<>"]/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "\"": "&quot;" })[c]);
const fmtNum = n => (Math.round(n * 100) / 100).toString();

function toast(text, error) {
    const t = h("div", { class: "toast" + (error ? " error" : ""), role: "status" }, text);
    document.body.append(t);
    setTimeout(() => t.remove(), error ? 6000 : 2600);
}

// 包里的路径：去掉 ./、统一斜杠；跑到包外面时返回 null（和 validate.js 一致）
function normPath(rel) {
    const s = String(rel || "");
    if (/^[a-zA-Z]:/.test(s) || s.startsWith("/") || s.startsWith("\\")) return null;
    const parts = [];
    for (const p of s.replace(/\\/g, "/").split("/")) {
        if (p === "" || p === ".") continue;
        if (p === "..") {
            if (!parts.length) return null;
            parts.pop();
        } else parts.push(p);
    }
    return parts.join("/");
}

// ---- 写 JSON：浅层一行一个字段，深层能放进一行的就写成一行（和示例包的样子一样） ----

function formatJson(value, multilineDepth) {
    const inline = v => {
        if (Array.isArray(v)) return v.length ? `[${v.map(inline).join(", ")}]` : "[]";
        if (isObj(v)) {
            const keys = Object.keys(v);
            return keys.length ? `{ ${keys.map(k => `${JSON.stringify(k)}: ${inline(v[k])}`).join(", ")} }` : "{}";
        }
        return JSON.stringify(v);
    };
    const out = (v, depth, indent) => {
        if (!Array.isArray(v) && !isObj(v)) return JSON.stringify(v);
        const one = inline(v);
        if (depth >= multilineDepth && indent.length + one.length <= 110) return one;
        const pad = indent + "  ";
        if (Array.isArray(v)) {
            if (!v.length) return "[]";
            return "[\n" + v.map(x => pad + out(x, depth + 1, pad)).join(",\n") + "\n" + indent + "]";
        }
        const keys = Object.keys(v);
        if (!keys.length) return "{}";
        return "{\n" + keys.map(k => `${pad}${JSON.stringify(k)}: ${out(v[k], depth + 1, pad)}`).join(",\n") + "\n" + indent + "}";
    };
    return out(value, 0, "") + "\n";
}

// ---- 状态 ----

const S = {
    dir: null,              // 可写的文件夹（FileSystemDirectoryHandle）；只读打开时为 null
    folderName: "",
    entries: new Map(),     // 包里的文件：路径 → () => Promise<File>
    folders: new Set(),     // 包里的文件夹（小写）
    manifest: null,
    files: {},              // Content 里的 JSON：路径 → 数据，读不了时 { parseError, text }
    original: {},           // 路径 → 打开时的文字（换行符、有没有注释）
    loaded: {},             // 路径 → 打开或保存时的内容（JSON.stringify），内容没变的文件不写回
    langBroken: {},         // 读不了的翻译文件：语言代码 → 原因（不写回，免得覆盖）
    lang: {},               // 语言代码 → { 键: 文字 }
    images: new Map(),      // 包里图片的路径（小写）→ object URL
    currentFile: null,
    sel: { page: "welcome" },
    result: null,
    dirty: false,
    issuesOpen: false,
};
const langDir = () => normPath(S.manifest && S.manifest.Lang ? S.manifest.Lang : "lang") || "lang";
const contentPaths = () => (Array.isArray(S.manifest && S.manifest.Content) ? S.manifest.Content : [])
    .map(f => normPath(f)).filter(p => p !== null);
const labelOf = path => (S.manifest.Content || []).find(f => normPath(f) === path) || path;
const findKey = (map, path) => Object.keys(map).find(k => k.toLowerCase() === path.toLowerCase());

function markDirty() {
    S.dirty = true;
    renderHeader();
    scheduleCheck();
}

// ---- 打开 ----

async function openFolder() {
    if (!window.showDirectoryPicker) {
        $("#folderInput").click();
        return;
    }
    let dir;
    try {
        dir = await window.showDirectoryPicker({ id: "contentpack", mode: "readwrite" });
    } catch (e) {
        if (e.name !== "AbortError") toast("打不开这个文件夹：" + e.message, true);
        return;
    }
    if (!await confirmDiscard()) return;
    const entries = new Map(), folders = new Set();
    const walk = async (handle, prefix) => {
        for await (const [name, child] of handle.entries()) {
            const path = prefix + name;
            if (child.kind === "directory") {
                folders.add(path.toLowerCase());
                await walk(child, path + "/");
            } else entries.set(path, () => child.getFile());
        }
    };
    await walk(dir, "");
    if (!hasEntry(entries, "dbmod.json")) {
        if (entries.size > 0 && !confirm(`“${dir.name}”里没有 dbmod.json。要在这里新建一个内容包吗？`)) return;
        await createPack(dir, entries);
        return;
    }
    await load(dir.name, entries, folders, dir);
}

// 不支持写文件夹的浏览器：用 <input webkitdirectory> 只读打开
async function openFromInput(files) {
    if (!files.length || !await confirmDiscard()) return;
    const root = files[0].webkitRelativePath.split("/")[0];
    const entries = new Map(), folders = new Set();
    for (const f of files) {
        const path = f.webkitRelativePath.slice(root.length + 1);
        entries.set(path, async () => f);
        const parts = path.split("/");
        for (let i = 1; i < parts.length; i++) folders.add(parts.slice(0, i).join("/").toLowerCase());
    }
    if (!hasEntry(entries, "dbmod.json")) {
        toast(`“${root}”里没有 dbmod.json，不是内容包`, true);
        return;
    }
    await load(root, entries, folders, null);
    toast("这个浏览器不能直接写回文件夹：保存时会下载 zip。用 Edge 或 Chrome 可以直接保存。");
}

const entryKey = path => [...S.entries.keys()].find(k => k.toLowerCase() === path.toLowerCase());
const hasEntry = (entries, path) => [...entries.keys()].some(k => k.toLowerCase() === path.toLowerCase());

async function readText(path) {
    const file = await S.entries.get(path)();
    return (await file.text()).replace(/^﻿/, "");
}

async function load(name, entries, folders, dir) {
    for (const url of S.images.values()) URL.revokeObjectURL(url);
    Object.assign(S, { dir, folderName: name, entries, folders, files: {}, original: {}, loaded: {}, lang: {}, langBroken: {}, images: new Map(), dirty: false });
    try {
        S.original["dbmod.json"] = await readText(entryKey("dbmod.json"));
        const manifest = V.parseJson(S.original["dbmod.json"]);
        if (!isObj(manifest)) throw new Error("最外层应该是 { … }");
        S.loaded["dbmod.json"] = JSON.stringify(manifest);
        // 字段名换成正式的大小写（游戏读的时候不区分）
        S.manifest = V.canonicalManifest(manifest);
    } catch (e) {
        S.manifest = null;
        S.sel = { page: "welcome" };
        render();
        toast("dbmod.json 读不了：" + e.message, true);
        return;
    }
    for (const file of S.manifest.Content || []) await loadContent(normPath(file));
    const lang = langDir().toLowerCase() + "/";
    for (const path of entries.keys()) {
        if (path.toLowerCase().startsWith(lang) && /^[^/]+\.json$/i.test(path.slice(lang.length))) {
            const code = path.slice(lang.length, -5);
            const key = `${langDir()}/${code}.json`;
            try {
                S.original[key] = await readText(path);
                const table = V.parseJson(S.original[key]);
                if (!isObj(table)) throw new Error("最外层应该是 { \"键\": \"文字\" }");
                S.lang[code] = table;
                S.loaded[key] = JSON.stringify(table);
            } catch (e) {
                S.langBroken[code] = e.message;
            }
        }
        if (/\.(png|jpe?g)$/i.test(path)) S.images.set(path.toLowerCase(), URL.createObjectURL(await entries.get(path)()));
    }
    S.currentFile = contentPaths().find(p => S.files[p] && !S.files[p].parseError) || null;
    S.sel = { page: "pack" };
    S.dirty = false;
    render();
    check();
}

async function loadContent(path) {
    if (!path) return;
    const actual = [...S.entries.keys()].find(k => k.toLowerCase() === path.toLowerCase());
    if (!actual) return;
    const text = await readText(actual);
    S.original[path] = text;
    try {
        S.files[path] = V.parseJson(text);
        S.loaded[path] = JSON.stringify(S.files[path]);
    } catch (e) {
        S.files[path] = { parseError: e.message, text };
    }
}

async function confirmDiscard() {
    return !S.dirty || confirm("当前的修改还没保存，确定要丢掉吗？");
}

// ---- 新建 ----

async function newPack() {
    if (!window.showDirectoryPicker) {
        toast("新建内容包要用 Edge 或 Chrome（需要写文件夹）", true);
        return;
    }
    let dir;
    try {
        dir = await window.showDirectoryPicker({ id: "contentpack", mode: "readwrite" });
    } catch (e) {
        if (e.name !== "AbortError") toast("打不开这个文件夹：" + e.message, true);
        return;
    }
    if (!await confirmDiscard()) return;
    const entries = new Map(), folders = new Set();
    for await (const [name, child] of dir.entries()) if (child.kind === "file") entries.set(name, () => child.getFile());
    if (hasEntry(entries, "dbmod.json")) {
        toast("这个文件夹里已经有内容包了，直接打开");
        return openFolderFrom(dir);
    }
    await createPack(dir, entries);
}

async function openFolderFrom(dir) {
    const entries = new Map(), folders = new Set();
    const walk = async (handle, prefix) => {
        for await (const [name, child] of handle.entries()) {
            const path = prefix + name;
            if (child.kind === "directory") {
                folders.add(path.toLowerCase());
                await walk(child, path + "/");
            } else entries.set(path, () => child.getFile());
        }
    };
    await walk(dir, "");
    await load(dir.name, entries, folders, dir);
}

// 文件夹里还没有的文件名：content.json、content2.json …
function freeName(taken, base) {
    for (let i = 1; ; i++) {
        const name = `${base}${i > 1 ? i : ""}.json`;
        if (!taken(name)) return name;
    }
}

function createPack(dir, entries) {
    const contentName = freeName(n => hasEntry(entries, n), "content");
    const form = h("form", { method: "dialog" },
        h("h3", {}, "新建内容包"),
        h("p", { style: "color:var(--dim)" }, `在“${dir.name}”里写入 dbmod.json 和 ${contentName}。`),
        h("div", { class: "grid" },
            fieldBox("名字", h("input", { name: "name", required: true, placeholder: "雷霆套装" })),
            fieldBox("标识 Id", h("input", { name: "id", required: true, pattern: "[A-Za-z0-9._+\\-]+", placeholder: "作者名.包名，比如 alice.thunder" }),
                "只用字母、数字和 . _ - +，发布以后不要再改"),
            fieldBox("作者", h("input", { name: "author" }))),
        h("div", { class: "actions" },
            h("button", { value: "cancel", formnovalidate: true }, "取消"),
            h("button", { class: "primary", value: "ok" }, "新建")));
    const dlg = h("dialog", {}, form);
    document.body.append(dlg);
    return new Promise(resolve => {
        dlg.addEventListener("close", async () => {
            dlg.remove();
            if (dlg.returnValue !== "ok") return resolve();
            const data = new FormData(form);
            const manifest = {
                Format: 1, Type: "content", Id: data.get("id"), Name: data.get("name"), Author: data.get("author") || "",
                Version: "1.0.0", Description: "", MinModKit: "0.21.0", Content: [contentName], Lang: "lang",
            };
            try {
                await writeFile(dir, "dbmod.json", formatJson(manifest, 1));
                await writeFile(dir, contentName, "{\n}\n");
            } catch (e) {
                toast("写不进这个文件夹：" + e.message, true);
                return resolve();
            }
            await openFolderFrom(dir);
            toast("已新建。左边点“+”加第一个技能或遗物。");
            resolve();
        });
        dlg.showModal();
    });
}

// ---- 保存、导出 ----

// 要写的文件：dbmod.json、Content 里的 JSON、翻译。只返回内容变了的
function changedFiles() {
    const out = new Map();
    const put = (path, data, depth) => {
        if (S.loaded[path] === JSON.stringify(data)) return;
        let text = formatJson(data, depth);
        // 原文件用 CRLF 就继续用 CRLF
        const old = S.original[path];
        if (old && old.includes("\r\n")) text = text.replace(/\r?\n/g, "\r\n");
        out.set(path, text);
    };
    put("dbmod.json", S.manifest, 1);
    // 读不了的文件原样留着，在“读不了”页面修好以后才会写
    for (const [path, data] of Object.entries(S.files)) if (!data.parseError) put(path, data, 3);
    for (const [code, table] of Object.entries(S.lang)) if (!S.langBroken[code]) put(`${langDir()}/${code}.json`, table, 1);
    return out;
}

async function writeFile(dir, path, text) {
    const parts = path.split("/");
    let d = dir;
    for (const p of parts.slice(0, -1)) d = await d.getDirectoryHandle(p, { create: true });
    const w = await (await d.getFileHandle(parts[parts.length - 1], { create: true })).createWritable();
    await w.write(text);
    await w.close();
}

async function save() {
    const changed = changedFiles();
    if (!S.dir) {
        await downloadZip();
        return;
    }
    const lossy = [...changed.keys()].filter(p => S.original[p] !== undefined && hasComments(S.original[p]));
    if (lossy.length && !confirm(`${lossy.join("、")} 里有注释或多余的逗号，保存后会被整理掉。继续保存吗？`)) return;
    try {
        for (const [path, text] of changed) {
            await writeFile(S.dir, path, text);
            S.original[path] = text;
            S.loaded[path] = JSON.stringify(V.parseJson(text));
            if (!S.entries.has(path)) {
                const p = path;
                S.entries.set(p, async () => new File([S.original[p]], p.split("/").pop()));
            }
        }
    } catch (e) {
        toast("保存失败：" + e.message, true);
        return;
    }
    S.dirty = false;
    renderHeader();
    const errors = S.result ? S.result.errors.length : 0;
    toast(changed.size ? `已保存 ${changed.size} 个文件` + (errors ? `，还有 ${errors} 处错误，游戏里会跳过写错的条目` : "") : "没有要保存的修改");
}

function hasComments(text) {
    try {
        JSON.parse(text);
        return false;
    } catch (e) {
        return true;
    }
}

// 整个包的当前内容：磁盘上的文件，改过的换成内存里的
async function snapshot() {
    const out = new Map();
    for (const [path, get] of S.entries) out.set(path, new Uint8Array(await (await get()).arrayBuffer()));
    const enc = new TextEncoder();
    for (const [path, text] of changedFiles()) out.set(path, enc.encode(text));
    return out;
}

const crcTable = (() => {
    const t = new Uint32Array(256);
    for (let n = 0; n < 256; n++) {
        let c = n;
        for (let k = 0; k < 8; k++) c = c & 1 ? 0xEDB88320 ^ (c >>> 1) : c >>> 1;
        t[n] = c >>> 0;
    }
    return t;
})();
function crc32(bytes) {
    let c = 0xFFFFFFFF;
    for (let i = 0; i < bytes.length; i++) c = crcTable[(c ^ bytes[i]) & 0xFF] ^ (c >>> 8);
    return (c ^ 0xFFFFFFFF) >>> 0;
}

// 不压缩的 zip（文件名用 UTF-8），里面套一层包的文件夹，解压到 BepInEx/content 就能用
function makeZip(files, folder) {
    const enc = new TextEncoder();
    const parts = [], central = [];
    let offset = 0;
    const now = new Date();
    const time = (now.getHours() << 11) | (now.getMinutes() << 5) | (now.getSeconds() >> 1);
    const date = ((now.getFullYear() - 1980) << 9) | ((now.getMonth() + 1) << 5) | now.getDate();
    for (const [path, data] of files) {
        const name = enc.encode(`${folder}/${path}`);
        const crc = crc32(data);
        const head = new DataView(new ArrayBuffer(30));
        [[0, 0x04034b50, 4], [4, 20, 2], [6, 0x0800, 2], [8, 0, 2], [10, time, 2], [12, date, 2], [14, crc, 4],
            [18, data.length, 4], [22, data.length, 4], [26, name.length, 2], [28, 0, 2]]
            .forEach(([at, v, n]) => n === 4 ? head.setUint32(at, v, true) : head.setUint16(at, v, true));
        const dir = new DataView(new ArrayBuffer(46));
        [[0, 0x02014b50, 4], [4, 20, 2], [6, 20, 2], [8, 0x0800, 2], [10, 0, 2], [12, time, 2], [14, date, 2], [16, crc, 4],
            [20, data.length, 4], [24, data.length, 4], [28, name.length, 2], [30, 0, 2], [32, 0, 2], [34, 0, 2], [36, 0, 2],
            [38, 0, 4], [42, offset, 4]]
            .forEach(([at, v, n]) => n === 4 ? dir.setUint32(at, v, true) : dir.setUint16(at, v, true));
        parts.push(head, name, data);
        central.push(dir, name);
        offset += 30 + name.length + data.length;
    }
    const size = central.reduce((n, p) => n + p.byteLength, 0);
    const end = new DataView(new ArrayBuffer(22));
    [[0, 0x06054b50, 4], [8, files.size, 2], [10, files.size, 2], [12, size, 4], [16, offset, 4]]
        .forEach(([at, v, n]) => n === 4 ? end.setUint32(at, v, true) : end.setUint16(at, v, true));
    return new Blob([...parts, ...central, end], { type: "application/zip" });
}

async function downloadZip() {
    const files = await snapshot();
    const m = S.manifest;
    const a = h("a", { href: URL.createObjectURL(makeZip(files, S.folderName)), download: `${m.Id || S.folderName}-${m.Version || "0"}.zip` });
    a.click();
    setTimeout(() => URL.revokeObjectURL(a.href), 10000);
    toast(`已导出 ${files.size} 个文件。解压到游戏的 BepInEx/content 里就能用`);
}

// 复制到游戏：选游戏文件夹（或 BepInEx、BepInEx/content），写到 BepInEx/content/<包的文件夹名>/
async function installToGame() {
    let dir;
    try {
        dir = await window.showDirectoryPicker({ id: "gamedir", mode: "readwrite" });
    } catch (e) {
        if (e.name !== "AbortError") toast("打不开这个文件夹：" + e.message, true);
        return;
    }
    const child = async (d, name) => {
        try {
            return await d.getDirectoryHandle(name);
        } catch (e) {
            return null;
        }
    };
    let content = null;
    if (dir.name.toLowerCase() === "content") content = dir;
    else if (dir.name === "BepInEx") content = await dir.getDirectoryHandle("content", { create: true });
    else {
        const bep = await child(dir, "BepInEx");
        if (bep) content = await bep.getDirectoryHandle("content", { create: true });
    }
    if (!content) {
        toast("选游戏的文件夹（里面有 BepInEx 的那个），或者 BepInEx\\content", true);
        return;
    }
    if (await child(content, S.folderName) && !confirm(`游戏里已经有“${S.folderName}”，要覆盖吗？`)) return;
    const target = await content.getDirectoryHandle(S.folderName, { create: true });
    const files = await snapshot();
    try {
        for (const [path, data] of files) await writeFile(target, path, data);
    } catch (e) {
        toast("复制失败：" + e.message, true);
        return;
    }
    toast(`已复制 ${files.size} 个文件。游戏开着的话，在 Mod 设置 → DBModKit → 内容包里点“关闭”再点“启用”`);
}

// ---- 检查 ----

let checkTimer = 0;
function scheduleCheck() {
    clearTimeout(checkTimer);
    checkTimer = setTimeout(check, 150);
}

function check() {
    if (!S.manifest) return;
    const files = {};
    for (const p of contentPaths()) {
        const k = findKey(S.files, p);
        if (k !== undefined) files[p] = S.files[k];
    }
    const lower = new Set([...S.entries.keys()].map(p => p.toLowerCase()));
    S.result = V.checkPack({
        manifest: S.manifest, files, catalog: C,
        exists: p => lower.has(p.toLowerCase()),
        existsFolder: p => p === "" || S.folders.has(p.toLowerCase()),
    });
    translationWarnings(S.result.warnings);
    renderNav();
    renderIssues();
    renderAside();
    showFieldMessages();
}

// 有翻译文件时，漏翻的条目提醒一下
function translationWarnings(warnings) {
    const codes = Object.keys(S.lang);
    if (!codes.length) return;
    for (const { path, list, item, index } of allItems()) {
        if (!item.key) continue;
        const prefix = V.LangPrefix[list];
        const missing = codes.filter(c => !S.lang[c][`${prefix}.${item.key}.name`]);
        if (missing.length) warnings.push({ where: `${labelOf(path)}.${list}[${index}]`, text: `${item.name || item.key}：${missing.map(c => LangCodes[c] || c).join("、")}翻译里没有名字` });
    }
}

function allItems() {
    const out = [];
    for (const path of contentPaths()) {
        const data = S.files[path];
        if (!data || data.parseError) continue;
        for (const list of V.Lists) (Array.isArray(data[list]) ? data[list] : []).forEach((item, index) => {
            if (isObj(item)) out.push({ path, list, item, index });
        });
    }
    return out;
}

// 某个条目的错误和提醒
function issuesOf(path, list, index) {
    if (!S.result) return [];
    const where = `${labelOf(path)}.${list}[${index}]`;
    const mine = x => x.where === where || x.where.startsWith(where + ".") || x.where.startsWith(where + "[");
    return [
        ...S.result.errors.filter(mine).map(x => ({ ...x, level: "e" })),
        ...S.result.warnings.filter(mine).map(x => ({ ...x, level: "w" })),
    ];
}

const message = x => x.text.startsWith(x.where + "：") ? x.text.slice(x.where.length + 1) : x.text;

// ---- 渲染 ----

function render() {
    renderHeader();
    renderNav();
    renderEditor();
    renderAside();
    renderIssues();
}

function renderHeader() {
    const m = S.manifest;
    const name = $("#packName");
    name.textContent = "";
    if (m) name.append(h("b", {}, m.Name || S.folderName), `  ${m.Version || ""}  ${S.folderName}/`, S.dirty ? h("span", { class: "dirty" }, "  有未保存的修改") : "");
    else name.textContent = "还没有打开内容包";
    $("#btnSave").disabled = !m || !S.dirty && !!S.dir;
    $("#btnSave").textContent = S.dir || !m ? "保存" : "保存为 zip";
    $("#btnInstall").disabled = !m || !window.showDirectoryPicker;
    $("#btnZip").disabled = !m;
}

function select(sel) {
    if (sel.page !== S.sel.page || sel.path !== S.sel.path || sel.list !== S.sel.list || sel.index !== S.sel.index) rawOpen = false;
    S.sel = sel;
    renderNav();
    renderEditor();
    renderAside();
    showFieldMessages();
}

function renderNav() {
    const nav = $("#nav");
    nav.textContent = "";
    if (!S.manifest) return;
    const on = (cond) => cond ? "entry on" : "entry";
    const counts = (list) => {
        const e = list.filter(x => x.level === "e").length, w = list.filter(x => x.level === "w").length;
        return [e ? h("span", { class: "count e", title: `${e} 处错误` }, e) : null, !e && w ? h("span", { class: "count w", title: `${w} 条提醒` }, w) : null];
    };
    const packIssues = S.result ? [
        ...S.result.errors.filter(x => x.where === "dbmod.json").map(x => ({ level: "e" })),
        ...S.result.warnings.filter(x => x.where === "dbmod.json").map(x => ({ level: "w" })),
    ] : [];
    nav.append(
        h("button", { class: on(S.sel.page === "pack") + " page", onclick: () => select({ page: "pack" }) }, h("span", { class: "name" }, "包的信息"), counts(packIssues)),
        h("button", { class: on(S.sel.page === "lang") + " page", onclick: () => select({ page: "lang" }) },
            h("span", { class: "name" }, "翻译"), h("span", { class: "key" }, Object.keys(S.lang).join(" ") || "没有")));
    const paths = contentPaths();
    const broken = paths.filter(p => S.files[p] && S.files[p].parseError);
    for (const p of broken)
        nav.append(h("button", { class: on(S.sel.page === "broken" && S.sel.path === p) + " page", onclick: () => select({ page: "broken", path: p }) },
            h("span", { class: "name" }, p), h("span", { class: "count e" }, "读不了")));
    const good = paths.filter(p => S.files[p] && !S.files[p].parseError);
    if (good.length > 1) {
        nav.append(h("div", { class: "group-head" }, h("span", {}, "新条目加到"),
            h("select", { onchange: e => { S.currentFile = e.target.value; } },
                good.map(p => h("option", { value: p, selected: p === S.currentFile }, p)))));
    }
    const items = allItems();
    for (const [list, info] of Object.entries(Lists)) {
        const mine = items.filter(x => x.list === list);
        nav.append(h("div", { class: "group" },
            h("div", { class: "group-head" }, h("span", {}, `${info.label}`), h("button", { class: "quiet", title: `新建${info.one}`, "aria-label": `新建${info.one}`, onclick: () => addItem(list) }, "+")),
            mine.map(({ path, item, index }) => {
                const isOn = S.sel.page === "item" && S.sel.path === path && S.sel.list === list && S.sel.index === index;
                const q = ["D", "C", "B", "A", "S"].includes(item.quality) ? item.quality : list === "heroVariants" || list === "equipEffects" || list === "equipment" ? "B" : "C";
                return h("button", { class: on(isOn), onclick: () => select({ page: "item", path, list, index }) },
                    h("span", { class: "pip", style: `background:var(--q-${q})` }),
                    h("span", { class: "name" }, item.name || item.key || "（没有名字）"),
                    good.length > 1 ? h("span", { class: "key" }, path.replace(/\.json$/i, "")) : null,
                    counts(issuesOf(path, list, index)));
            })));
    }
}

function renderIssues() {
    const r = S.result;
    const sum = $("#summary");
    sum.textContent = "";
    const list = $("#issues");
    list.textContent = "";
    if (!S.manifest || !r) return;
    const n = allItems().length;
    sum.append(`${n} 个条目，`,
        r.errors.length ? h("span", { style: "color:var(--error)" }, `${r.errors.length} 处错误`) : h("span", { style: "color:var(--ok)" }, "没有错误"),
        r.warnings.length ? `，${r.warnings.length} 条提醒` : "");
    $("#btnIssues").textContent = S.issuesOpen ? "收起检查结果" : "展开检查结果";
    list.hidden = !S.issuesOpen;
    for (const x of r.errors) list.append(issueButton(x, "e", true));
    for (const x of r.warnings) list.append(issueButton(x, "w", true));
}

// full：整句（底栏）；否则去掉条目前缀，只留字段（右栏）
function issueButton(x, level, full, prefix) {
    let text = full ? x.text : message(x);
    if (!full && prefix && x.where.startsWith(prefix) && x.text.startsWith(x.where + "：")) {
        const rel = x.where.slice(prefix.length).replace(/^\./, "");
        if (rel) text = `${rel}：${text}`;
    }
    return h("button", { class: "issue" + (level === "w" ? " w" : ""), onclick: () => goTo(x.where) }, text);
}

// 点检查结果跳到对应的条目和字段
function goTo(where) {
    if (where === "dbmod.json") return select({ page: "pack" });
    for (const path of contentPaths()) {
        const label = labelOf(path);
        if (where === label) return S.files[path] && S.files[path].parseError ? select({ page: "broken", path }) : null;
        const m = where.startsWith(label + ".") && /^\.(\w+)\[(\d+)\]/.exec(where.slice(label.length));
        if (m) {
            select({ page: "item", path, list: m[1], index: +m[2] });
            const rel = where.slice(label.length + m[0].length).replace(/^\./, "");
            const el = fieldFor(rel);
            if (el) {
                el.scrollIntoView({ block: "center", behavior: "smooth" });
                const input = el.querySelector("input, select, textarea");
                if (input) input.focus({ preventScroll: true });
            }
            return;
        }
    }
}

// ---- 编辑区 ----

let fieldEls = new Map(); // 字段路径（如 cost、onCast[1]、attributes）→ 外框元素

function renderEditor() {
    const ed = $("#editor");
    ed.textContent = "";
    fieldEls = new Map();
    if (!S.manifest) return ed.append(welcome());
    if (S.sel.page === "pack") return ed.append(packPage());
    if (S.sel.page === "lang") return ed.append(langPage());
    if (S.sel.page === "broken") return ed.append(brokenPage(S.sel.path));
    if (S.sel.page === "item") {
        const item = currentItem();
        if (!item) return select({ page: "pack" });
        ed.append(itemPage(item, S.sel.list));
    }
}

function currentItem() {
    const { path, list, index } = S.sel;
    const data = S.files[path];
    return data && Array.isArray(data[list]) && isObj(data[list][index]) ? data[list][index] : null;
}

function welcome() {
    const api = !!window.showDirectoryPicker;
    return h("div", { class: "welcome" },
        h("h2", {}, "做一个不用写代码的 Mod"),
        h("p", {}, "内容包是一个文件夹：dbmod.json 加上几个 JSON 和图片，DBModKit 直接加载。在这里用表单填技能、被动、遗物、装备和变体英雄，写错的地方会按游戏的规则马上标出来。"),
        h("div", { class: "actions" },
            h("button", { class: "primary", onclick: newPack, disabled: !api }, "新建内容包"),
            h("button", { onclick: openFolder }, "打开已有的内容包")),
        !api ? h("p", { style: "color:var(--warn)" }, "这个浏览器不能写文件夹，只能打开查看和导出 zip。用 Edge 或 Chrome 打开这个页面可以直接保存。") : null,
        h("ol", {},
            h("li", {}, "新建或打开一个文件夹。想先看看写法，可以打开仓库里的 templates/ContentPack 示例。"),
            h("li", {}, "左边点“+”加条目，中间填表，右边看游戏里大概的样子和这一条的问题。"),
            h("li", {}, "保存后点“复制到游戏”，选游戏文件夹，会放进 BepInEx/content 里。"),
            h("li", {}, "游戏开着的话，到 Mod 设置 → DBModKit → 内容包里点“关闭”再点“启用”就会重新读。")));
}

// 一个字段的外框：标签、控件、说明、错误
function fieldBox(label, control, hint, opts = {}) {
    const box = h("div", { class: "field" + (opts.wide ? " wide" : "") + (opts.check ? " check" : "") },
        opts.check ? [control, h("label", {}, label)] : [h("label", {}, label, opts.key ? h("code", {}, opts.key) : null), control],
        hint ? h("div", { class: "hint" }, hint) : null,
        h("div", { class: "msg" }));
    if (opts.path) fieldEls.set(opts.path, box);
    return box;
}

function fieldFor(rel) {
    let p = rel;
    while (p) {
        if (fieldEls.has(p)) return fieldEls.get(p);
        const cut = Math.max(p.lastIndexOf("."), p.lastIndexOf("["));
        if (cut <= 0) break;
        p = p.slice(0, cut);
    }
    return null;
}

function showFieldMessages() {
    for (const box of fieldEls.values()) {
        const msg = box.querySelector(":scope > .msg");
        if (msg) msg.textContent = "";
        box.querySelectorAll(":scope > input, :scope > select, :scope > textarea, :scope > .row > input").forEach(i => i.classList.remove("bad"));
        box.classList.remove("has-error");
    }
    if (S.sel.page === "pack" && S.result) {
        const box = fieldEls.get("Content");
        if (box) box.querySelector(".msg").textContent = S.result.errors.filter(x => x.where === "dbmod.json").map(message).join("\n");
        return;
    }
    if (S.sel.page !== "item") return;
    const where = `${labelOf(S.sel.path)}.${S.sel.list}[${S.sel.index}]`;
    for (const x of issuesOf(S.sel.path, S.sel.list, S.sel.index)) {
        const rel = x.where.slice(where.length).replace(/^\./, "");
        // 条目级的错误（不认识的字段等）按字段名找
        const named = /不认识的字段 "(\w+)"/.exec(x.text);
        const box = fieldFor(rel) || (named && fieldFor(rel ? `${rel}.${named[1]}` : named[1])) || fieldEls.get("$raw");
        if (!box) continue;
        const msg = box.querySelector(":scope > .msg");
        if (msg) {
            msg.classList.toggle("w", x.level === "w" && !msg.textContent);
            msg.textContent += (msg.textContent ? "\n" : "") + message(x);
            msg.style.whiteSpace = "pre-wrap";
        }
        if (x.level === "e") {
            box.querySelectorAll(":scope > input, :scope > select, :scope > textarea, :scope > .row > input").forEach(i => i.classList.add("bad"));
            box.classList.add("has-error");
        }
    }
}

// ---- 通用控件：直接改 obj[key]，空着就删掉字段（用默认值） ----

function setField(obj, key, value) {
    V.clearFloat(obj, key);
    if (value === undefined) delete obj[key];
    else obj[key] = value;
    markDirty();
}

// 文字框里的数字：能读成数字就存数字，读不成就原样存文字（检查会报“应该是数字”）
function parseNumber(text) {
    const t = text.trim();
    if (t === "") return undefined;
    return /^-?(\d+\.?\d*|\.\d+)(e[+-]?\d+)?$/i.test(t) ? Number(t) : t;
}
// 表达式：纯数字存数字，其他存文字
const parseExpr = parseNumber;

function textInput(obj, key, opts = {}) {
    const v = own(obj, key) ? obj[key] : "";
    return h("input", {
        value: typeof v === "string" || typeof v === "number" ? String(v) : JSON.stringify(v),
        placeholder: opts.placeholder || "", list: opts.list, style: opts.style,
        oninput: e => {
            const t = e.target.value;
            setField(obj, key, t === "" && !opts.keepEmpty ? undefined : opts.number ? parseNumber(t) : t);
            if (opts.after) opts.after();
        },
    });
}

function boolInput(obj, key, def) {
    return h("input", {
        type: "checkbox", checked: own(obj, key) ? obj[key] === true : def,
        onchange: e => setField(obj, key, e.target.checked === def ? undefined : e.target.checked),
    });
}

function selectInput(obj, key, options, def, after) {
    const cur = own(obj, key) ? obj[key] : def;
    const known = options.some(([v]) => v === cur);
    return h("select", {
        onchange: e => {
            const v = options.find(([x]) => String(x) === e.target.value)[0];
            setField(obj, key, v === def ? undefined : v);
            if (after) after();
        },
    }, !known ? h("option", { selected: true, value: "\u0000" }, `${JSON.stringify(cur)}（写错了）`) : null,
    options.map(([v, label]) => h("option", { value: String(v), selected: v === cur }, label)));
}

function numsInput(obj, key, placeholder) {
    const v = own(obj, key) ? obj[key] : [];
    return h("input", {
        value: Array.isArray(v) ? v.join(", ") : JSON.stringify(v), placeholder,
        oninput: e => {
            const t = e.target.value.trim();
            setField(obj, key, t === "" ? undefined : t.split(/[,，\s]+/).filter(Boolean).map(x => parseNumber(x)));
            renderAside();
        },
    });
}

function datalist(id, options) {
    if (document.getElementById(id)) return null;
    return h("datalist", { id }, options.map(([v, label]) => h("option", { value: v, label })));
}

function chipsInput(obj, key, options, def) {
    const cur = () => own(obj, key) && Array.isArray(obj[key]) ? obj[key] : def;
    const wrap = h("div", { class: "chips" });
    const draw = () => {
        wrap.textContent = "";
        const values = cur();
        for (const [v, label] of options) {
            const on = values.includes(v);
            wrap.append(h("button", {
                class: "chip" + (on ? " on" : ""), type: "button", "aria-pressed": String(on), title: v,
                onclick: () => {
                    const next = on ? values.filter(x => x !== v) : [...values, v];
                    setField(obj, key, JSON.stringify(next) === JSON.stringify(def) ? undefined : next);
                    draw();
                },
            }, label));
        }
        for (const v of values.filter(v => !options.some(([x]) => x === v)))
            wrap.append(h("span", { class: "chip on", title: "不在常用列表里" }, String(v)));
    };
    draw();
    return wrap;
}

// ---- 包的信息 ----

function packPage() {
    const m = S.manifest;
    const deps = () => Array.isArray(m.Dependencies) ? m.Dependencies : [];
    const depsBox = h("div");
    const drawDeps = () => {
        depsBox.textContent = "";
        deps().forEach((d, i) => depsBox.append(h("div", { class: "row", style: "margin-top:6px" },
            textInput(d, "Guid", { placeholder: "插件 GUID 或内容包 Id" }),
            textInput(d, "Name", { placeholder: "显示的名字" }),
            textInput(d, "MinVersion", { placeholder: "最低版本", style: "max-width:100px" }),
            textInput(d, "Workshop", { placeholder: "工坊物品号", number: true, style: "max-width:130px" }),
            h("button", { class: "quiet danger", onclick: () => { m.Dependencies.splice(i, 1); if (!m.Dependencies.length) delete m.Dependencies; markDirty(); drawDeps(); } }, "删除"))));
        depsBox.append(h("button", { style: "margin-top:6px", onclick: () => { (m.Dependencies = deps()).push({ Guid: "" }); markDirty(); drawDeps(); } }, "加一个依赖"));
    };
    drawDeps();

    const filesBox = h("div");
    const drawFiles = () => {
        filesBox.textContent = "";
        (m.Content || []).forEach((f, i) => {
            const p = normPath(f);
            const data = p !== null ? S.files[p] : undefined;
            const n = data && !data.parseError ? V.Lists.reduce((s, l) => s + (Array.isArray(data[l]) ? data[l].length : 0), 0) : 0;
            filesBox.append(h("div", { class: "row", style: "margin-top:4px" },
                h("span", { style: "flex:1" }, f, h("span", { style: "color:var(--dim)" }, data === undefined ? "  （找不到）" : data.parseError ? "  （读不了）" : `  ${n} 个条目`)),
                h("button", {
                    class: "quiet danger", onclick: () => {
                        m.Content.splice(i, 1);
                        if (!contentPaths().includes(S.currentFile)) S.currentFile = contentPaths().find(x => S.files[x] && !S.files[x].parseError) || null;
                        markDirty();
                        drawFiles();
                        renderNav();
                    },
                }, "不再加载")));
        });
        const name = h("input", { placeholder: "新文件名，比如 relics.json" });
        filesBox.append(h("div", { class: "row", style: "margin-top:8px" }, name, h("button", {
            onclick: async () => {
                let f = name.value.trim();
                if (!f) return;
                if (!/\.json$/i.test(f)) f += ".json";
                const p = normPath(f);
                if (p === null) return toast("文件要放在内容包文件夹里", true);
                if ((m.Content || []).some(x => normPath(x) === p)) return toast(`${f} 已经在列表里了`, true);
                m.Content = [...(m.Content || []), f];
                // 文件夹里已经有这个文件就读进来，不能当成空文件（保存时会覆盖掉）
                if (!S.files[p]) await loadContent(p);
                if (!S.files[p]) S.files[p] = {};
                if (!S.currentFile) S.currentFile = p;
                markDirty();
                drawFiles();
                renderNav();
            },
        }, "添加")));
    };
    drawFiles();

    const contentBox = fieldBox("要加载的 JSON 文件", filesBox, null, { wide: true, path: "Content", key: "Content" });
    return h("div", {},
        h("div", { class: "title" }, h("h2", {}, "包的信息"), h("span", { class: "where" }, "dbmod.json")),
        h("p", { class: "lead" }, `本地测试放在游戏的 BepInEx/content/${S.folderName}/。发到创意工坊用仓库的 pack.ps1 -Content。`),
        h("div", { class: "grid" },
            fieldBox("名字", textInput(m, "Name", { after: renderHeader }), "游戏的 Mod 设置里显示的名字", { key: "Name" }),
            fieldBox("标识", textInput(m, "Id", { placeholder: "作者名.包名" }), "一定要写，发布以后不要改：技能编号和联机比对都靠它", { key: "Id" }),
            fieldBox("作者", textInput(m, "Author"), null, { key: "Author" }),
            fieldBox("版本", textInput(m, "Version", { placeholder: "1.0.0", after: renderHeader }), "联机时大家的版本要一样", { key: "Version" }),
            fieldBox("最低 DBModKit 版本", textInput(m, "MinModKit", { placeholder: "0.21.0" }), "用了遗物要 0.22.0，装备要 0.24.0", { key: "MinModKit" }),
            fieldBox("翻译文件夹", textInput(m, "Lang", { placeholder: "lang" }), null, { key: "Lang" }),
            fieldBox("简介", h("textarea", { rows: 2, value: m.Description || "", oninput: e => setField(m, "Description", e.target.value) }), null, { wide: true, key: "Description" }),
            contentBox),
        h("fieldset", {}, h("legend", {}, "依赖", h("small", {}, "缺了依赖时整个包不加载，填了工坊物品号会给玩家一个订阅按钮")), depsBox));
}

// ---- 读不了的文件 ----

function brokenPage(path) {
    const data = S.files[path];
    const area = h("textarea", { rows: 28, spellcheck: false, style: "font-family:Consolas, monospace", value: data.text });
    const msg = h("div", { class: "msg", style: "color:var(--error)" }, data.parseError);
    return h("div", {},
        h("div", { class: "title" }, h("h2", {}, path), h("span", { class: "where" }, "JSON 写错了，游戏读不了这个文件")),
        h("p", { class: "lead" }, "按提示的行列改好，再点“重新读取”。"), msg, area,
        h("div", { class: "row", style: "margin-top:8px" }, h("button", {
            class: "primary", onclick: () => {
                try {
                    S.files[path] = V.parseJson(area.value);
                    if (!S.currentFile) S.currentFile = path;
                    markDirty();
                    select({ page: "pack" });
                    renderNav();
                } catch (e) {
                    data.text = area.value;
                    msg.textContent = e.message;
                }
            },
        }, "重新读取")));
}

// ---- 翻译 ----

function langPage() {
    const codes = Object.keys(S.lang);
    const rows = [];
    for (const { list, item } of allItems()) {
        if (!item.key) continue;
        const prefix = V.LangPrefix[list];
        for (const [suffix, field, label] of [["name", "name", "名字"], ["desc", "description", "说明"]]) {
            const key = `${prefix}.${item.key}.${suffix}`;
            const zh = typeof item[field] === "string" ? item[field] : "";
            if (suffix === "desc" && !zh) continue;
            rows.push(h("tr", {},
                h("td", {}, h("div", {}, `${Lists[list].one} ${label}`), h("div", { style: "color:#8f95a0;font-size:12px" }, key)),
                h("td", { class: "zh" }, zh),
                codes.map(c => {
                    const t = S.lang[c];
                    const tag = suffix === "desc" ? "textarea" : "input";
                    return h("td", {}, h(tag, {
                        rows: 2, value: t[key] || "", class: t[key] ? "" : "missing", placeholder: "没有翻译时显示英文，再没有就显示中文",
                        oninput: e => {
                            if (e.target.value) t[key] = e.target.value;
                            else delete t[key];
                            e.target.classList.toggle("missing", !e.target.value);
                            markDirty();
                        },
                    }));
                })));
        }
    }
    const known = Object.keys(S.lang).flatMap(c => Object.keys(S.lang[c]));
    const expected = new Set(allItems().flatMap(({ list, item }) => [`${V.LangPrefix[list]}.${item.key}.name`, `${V.LangPrefix[list]}.${item.key}.desc`]));
    const stale = [...new Set(known.filter(k => !expected.has(k)))];
    const add = h("select", {}, h("option", { value: "" }, "选一种语言"),
        Object.entries(LangCodes).filter(([c]) => !S.lang[c] && !S.langBroken[c]).map(([c, n]) => h("option", { value: c }, `${n}（${c}）`)));
    return h("div", {},
        h("div", { class: "title" }, h("h2", {}, "翻译"), h("span", { class: "where" }, `${langDir()}/<语言>.json`)),
        h("p", { class: "lead" }, "中文写在条目里。游戏是其他语言时用对应的翻译，没有就用英文，再没有就显示中文。"),
        Object.entries(S.langBroken).map(([c, e]) => h("p", { style: "color:var(--error)" },
            `${langDir()}/${c}.json 读不了（${e}）。用文本编辑器修好后重新打开文件夹；在那之前这里不会改动它。`)),
        h("div", { class: "row", style: "margin-bottom:14px;max-width:420px" }, add, h("button", {
            onclick: () => {
                if (!add.value) return;
                S.lang[add.value] = {};
                markDirty();
                renderEditor();
                renderNav();
            },
        }, "加一种语言")),
        codes.length ? h("table", { class: "lang" },
            h("thead", {}, h("tr", {}, h("th", {}, "条目"), h("th", {}, "中文"), codes.map(c => h("th", {}, `${LangCodes[c] || c}（${c}）`)))),
            h("tbody", {}, rows)) : h("p", { class: "hint" }, "还没有翻译文件。"),
        stale.length ? h("p", { style: "color:var(--warn);margin-top:14px" }, `翻译文件里有 ${stale.length} 个键没有对应的条目（改过 key？）：${stale.slice(0, 8).join("、")}${stale.length > 8 ? " …" : ""}`) : null);
}

// ---- 条目 ----

function uniqueKey(list, base) {
    const used = new Set(allItems().filter(x => x.list === list).map(x => x.item.key));
    let i = 1;
    while (used.has(`${base}${i}`)) i++;
    return `${base}${i}`;
}

function addItem(list) {
    let path = S.currentFile;
    if (!path || !S.files[path] || S.files[path].parseError) {
        path = freeName(n => !!entryKey(n) || own(S.files, n), "content");
        S.manifest.Content = [...(S.manifest.Content || []), path];
        S.files[path] = {};
        S.currentFile = path;
    }
    const data = S.files[path];
    if (!Array.isArray(data[list])) data[list] = [];
    const base = { activeSkills: "skill", passiveSkills: "passive", relics: "relic", equipEffects: "effect", equipment: "equip", heroVariants: "hero" }[list];
    const item = { key: uniqueKey(list, base), name: `新${Lists[list].one}` };
    if (list === "equipment") Object.assign(item, { artifact: true, attributes: { attack: 50 } });
    if (list === "heroVariants") item.baseHero = 1;
    data[list].push(item);
    markDirty();
    select({ page: "item", path, list, index: data[list].length - 1 });
}

function itemPage(item, list) {
    const { path, index } = S.sel;
    const info = Lists[list];
    const rerender = () => {
        renderEditor();
        showFieldMessages();
        renderAside();
    };
    const after = () => {
        renderAside();
        renderNav();
    };
    const page = h("div", {},
        h("div", { class: "title" },
            h("h2", {}, item.name || item.key || "（没有名字）"),
            h("span", { class: "where" }, `${info.one}，${labelOf(path)} 第 ${index + 1} 个`),
            h("button", {
                onclick: () => {
                    const copy = JSON.parse(JSON.stringify(item));
                    copy.key = uniqueKey(list, (item.key || "copy") + "_");
                    S.files[path][list].splice(index + 1, 0, copy);
                    markDirty();
                    select({ page: "item", path, list, index: index + 1 });
                },
            }, "复制一份"),
            h("button", {
                class: "danger", onclick: () => {
                    if (!confirm(`删除“${item.name || item.key}”？`)) return;
                    S.files[path][list].splice(index, 1);
                    if (!S.files[path][list].length) delete S.files[path][list];
                    markDirty();
                    select({ page: "pack" });
                },
            }, "删除")),
        h("p", { class: "lead" }, info.lead));

    const basic = h("div", { class: "grid" },
        fieldBox("名字", textInput(item, "name", { after: () => { after(); page.querySelector("h2").textContent = item.name || item.key; } }), null, { path: "name", key: "name" }),
        fieldBox("键", textInput(item, "key", { keepEmpty: true, after }), "包里唯一，只用英文字母、数字、下划线；发布后不要改", { path: "key", key: "key" }),
        fieldBox("说明", h("textarea", { rows: 2, value: typeof item.description === "string" ? item.description : "", oninput: e => { setField(item, "description", e.target.value || undefined); renderAside(); } }),
            list === "activeSkills" ? "{0} 是游戏按伤害公式生成的文字；写了数值时 {0} {1} … 换成数值" : "{0} {1} … 换成第几个数值（含等级成长）",
            { wide: true, path: "description", key: "description" }));
    page.append(basic);

    if (list === "activeSkills") page.append(activeFields(item, after));
    if (list === "passiveSkills") page.append(passiveFields(item, after));
    if (list === "relics") page.append(relicFields(item, after, rerender));
    if (list === "equipEffects") page.append(equipEffectFields(item, after));
    if (list === "equipment") page.append(equipmentFields(item, after, rerender));
    if (list === "heroVariants") page.append(variantFields(item, after, rerender));
    if (list !== "equipment" && list !== "heroVariants") page.append(effectsSection(item, list, rerender));
    page.append(rawSection(item, rerender));
    return page;
}

function iconField(item, options, after) {
    const id = "icons-" + options.name;
    const img = typeof item.icon === "string" && /\.(png|jpe?g)$/i.test(item.icon);
    return fieldBox("图标", h("div", {}, datalist(id, options.list), textInput(item, "icon", { list: id, placeholder: options.def, after })),
        img ? "包里的图片" : "游戏图标名，或包里的图片路径（比如 assets/icon.png）", { path: "icon", key: "icon" });
}
const iconLists = {
    skill: () => ({ name: "skill", def: "SpellBook01_66", list: uniq([...C.activeSkills, ...C.passiveSkills].map(s => [s.icon, `${s.name}`])) }),
    relic: () => ({ name: "relic", def: "", list: uniq(C.relics.map(s => [s.icon, s.name])) }),
    equip: () => ({ name: "equip", def: "", list: uniq(C.equipment.map(s => [s.icon, s.name])) }),
};
function uniq(pairs) {
    const seen = new Map();
    for (const [v, l] of pairs) if (v && !seen.has(v)) seen.set(v, l);
    return [...seen];
}
const qualityOptions = V.Qualities.map(q => [q, q]);

function activeFields(item, after) {
    const anim = h("div");
    const drawAnim = () => {
        anim.textContent = "";
        const on = isObj(item.animation);
        anim.append(h("div", { class: "grid" },
            fieldBox("释放时换动作", h("input", { type: "checkbox", checked: on, onchange: e => { setField(item, "animation", e.target.checked ? { game: Animations[0] } : undefined); drawAnim(); } }), null, { check: true, path: "animation" }),
            on ? [
                fieldBox("动作名", h("div", {}, datalist("anims", Animations.map(a => [a, a])), textInput(item.animation, "game", { list: "anims", keepEmpty: true })), null, { path: "animation.game" }),
                fieldBox("速度", textInput(item.animation, "speed", { number: true, placeholder: "1" }), null, { path: "animation.speed" }),
                fieldBox("播放秒数", textInput(item.animation, "duration", { number: true, placeholder: "动作本身的长度" }), null, { path: "animation.duration" }),
            ] : null));
    };
    drawAnim();
    const n = (label, key, def, hint) => fieldBox(label, textInput(item, key, { number: true, placeholder: String(def), after }), hint, { path: key, key });
    return h("div", {},
        h("fieldset", {}, h("legend", {}, "外观"), h("div", { class: "grid" },
            iconField(item, iconLists.skill(), after),
            fieldBox("品质", selectInput(item, "quality", qualityOptions, "C", after), null, { path: "quality", key: "quality" }),
            fieldBox("瞄准方式", selectInput(item, "indicator", [["None", "不用瞄准（脚下）"], ["Range", "选一个范围"], ["Vector", "朝一个方向"], ["Target", "锁定一个敌人"]], "Range"), null, { path: "indicator", key: "indicator" }),
            fieldBox("标签", chipsInput(item, "tags", Object.entries(SkillTags), ["Area"]), "影响技能书分类和其他 Mod 的判断", { wide: true, path: "tags", key: "tags" }))),
        h("fieldset", {}, h("legend", {}, "释放"), h("div", { class: "grid" },
            n("蓝耗", "cost", 20), n("冷却（秒）", "cooldown", 8), n("施法距离", "castingRange", 8), n("作用半径", "range", 3),
            n("持续秒数", "duration", 0, "0 是一次性技能"), n("说明里的伤害间隔", "interval", 0),
            n("充能次数", "charges", 0), n("充能时间", "chargeTime", 0),
            fieldBox("自动释放", boolInput(item, "canAuto", true), null, { check: true, path: "canAuto" }),
            fieldBox("进技能书", boolInput(item, "inSkillBook", true), null, { check: true, path: "inSkillBook" }))),
        h("fieldset", {}, h("legend", {}, "伤害公式", h("small", {}, "基础 + 属性 × 系数，效果里的“造成伤害”默认用它")), h("div", { class: "grid" },
            n("基础伤害", "damageBase", 0, null),
            fieldBox("按哪个属性", selectInput(item, "damageType", DamageTypes.map((l, i) => [i, l]), 0, after), null, { path: "damageType", key: "damageType" }),
            n("系数", "damageValue", 0),
            fieldBox("数值", numsInput(item, "values", "比如 20, 5"), "效果和说明里用 {0} {1} … 引用", { path: "values", key: "values" }))),
        h("fieldset", {}, h("legend", {}, "动作"), anim));
}

function passiveFields(item, after) {
    return h("fieldset", {}, h("legend", {}, "外观和数值"), h("div", { class: "grid" },
        iconField(item, iconLists.skill(), after),
        fieldBox("品质", selectInput(item, "quality", qualityOptions, "C", after), null, { path: "quality", key: "quality" }),
        fieldBox("数值", numsInput(item, "values", "比如 20, 5"), "用 {0} {1} … 引用", { path: "values", key: "values" }),
        fieldBox("每级成长", numsInput(item, "levelUp", "比如 10, 1"), "每升一级加多少，和数值一一对应", { path: "levelUp", key: "levelUp" }),
        fieldBox("进技能书", boolInput(item, "inSkillBook", true), null, { check: true, path: "inSkillBook" }),
        fieldBox("可以重复学", boolInput(item, "repeatable", false), null, { check: true, path: "repeatable" }),
        fieldBox("标签", chipsInput(item, "tags", Object.entries(SkillTags), ["Buff"]), null, { wide: true, path: "tags", key: "tags" })));
}

function relicFields(item, after, rerender) {
    const types = h("div", { class: "row", style: "flex-wrap:wrap" });
    const values = Array.isArray(item.values) ? item.values : [];
    const cur = Array.isArray(item.valueTypes) ? item.valueTypes : [];
    const typeLabels = [["Normal", "原样"], ["Percent", "小数转百分比"], ["PercentConst", "直接加 %"], ["InversePercent", "(1 - 值) × 100%"]];
    values.forEach((_, i) => types.append(h("label", { style: "display:flex;gap:4px;align-items:center" }, `{${i}}`,
        h("select", {
            onchange: e => {
                const next = values.map((_, j) => j === i ? e.target.value : cur[j] || "Normal");
                setField(item, "valueTypes", next.every(t => t === "Normal") ? undefined : next);
                renderAside();
            },
        }, typeLabels.map(([v, l]) => h("option", { value: v, selected: (cur[i] || "Normal") === v }, l))))));
    return h("fieldset", {}, h("legend", {}, "外观和数值"), h("div", { class: "grid" },
        iconField(item, iconLists.relic(), after),
        fieldBox("品质", selectInput(item, "quality", qualityOptions, "C", after), null, { path: "quality", key: "quality" }),
        fieldBox("数值", numsInput(item, "values", "比如 30, 5"), "用 {0} {1} … 引用", { path: "values", key: "values" }),
        fieldBox("每级成长", numsInput(item, "levelUp", "比如 15, 2"), null, { path: "levelUp", key: "levelUp" }),
        fieldBox("最高等级", textInput(item, "maxLevel", { number: true, placeholder: "-1（不限）" }), "0 是不能强化", { path: "maxLevel", key: "maxLevel" }),
        fieldBox("只能拿一个", boolInput(item, "unique", true), null, { check: true, path: "unique" }),
        fieldBox("进三选一", boolInput(item, "inPool", true), "不进就只能由别的 Mod 发放", { check: true, path: "inPool" }),
        fieldBox("数值的显示方式", values.length ? types : h("span", { class: "hint" }, "先填数值"), null, { wide: true, path: "valueTypes", key: "valueTypes" })));
}

function equipEffectFields(item, after) {
    return h("fieldset", {}, h("legend", {}, "数值和挂载"), h("div", { class: "grid" },
        fieldBox("数值", numsInput(item, "values", "比如 20, 5"), "用 {0} {1} … 引用", { path: "values", key: "values" }),
        fieldBox("每次强化成长", numsInput(item, "levelUp", ""), null, { path: "levelUp", key: "levelUp" }),
        fieldBox("挂到游戏装备上", idListInput(item, "attachTo", C.equipment.map(e => [e.id, e.name]), "equips", after),
            "写装备编号，挂上后游戏装备的说明里会多出这个特效；只给包里的新装备用就不写", { wide: true, path: "attachTo", key: "attachTo" })));
}

// 编号列表：输入框 + 下拉提示，存成整数数组
function idListInput(item, key, options, listId, after) {
    const cur = Array.isArray(item[key]) ? item[key] : [];
    const names = new Map(options);
    const shown = h("div", { class: "hint" });
    const show = () => {
        const v = Array.isArray(item[key]) ? item[key] : [];
        shown.textContent = v.map(id => `${id} ${names.get(id) || "（游戏里没有）"}`).join("，");
    };
    show();
    return h("div", {}, datalist(listId, options.map(([id, n]) => [id, n])),
        h("input", {
            value: cur.join(", "), list: listId, placeholder: "比如 2, 3",
            oninput: e => {
                const t = e.target.value.trim();
                setField(item, key, t === "" ? undefined : t.split(/[,，\s]+/).filter(Boolean).map(x => parseNumber(x)));
                show();
                after();
            },
        }), shown);
}

function equipmentFields(item, after, rerender) {
    const attrs = h("div");
    const drawAttrs = () => {
        attrs.textContent = "";
        const a = isObj(item.attributes) ? item.attributes : null;
        if (a) for (const k of Object.keys(a)) {
            attrs.append(h("div", { class: "row", style: "margin-top:4px" },
                h("input", {
                    value: k, list: "attrs", style: "max-width:220px",
                    onchange: e => {
                        const nk = e.target.value.trim();
                        if (!nk || nk === k) return;
                        if (own(a, nk)) {
                            toast(`已经有 ${nk} 了`, true);
                            e.target.value = k;
                            return;
                        }
                        item.attributes = Object.fromEntries(Object.entries(a).map(([x, v]) => [x === k ? nk : x, v]));
                        markDirty();
                        drawAttrs();
                        renderAside();
                    },
                }),
                h("span", { class: "hint", style: "min-width:90px" }, AttrLabels[k] || ""),
                textInput(a, k, { number: true, keepEmpty: true, after: renderAside }),
                h("button", { class: "quiet danger", onclick: () => { delete a[k]; markDirty(); drawAttrs(); renderAside(); } }, "删除")));
        }
        attrs.append(h("button", {
            style: "margin-top:6px", onclick: () => {
                if (!isObj(item.attributes)) item.attributes = {};
                const k = C.equipAttributes.find(x => !own(item.attributes, x));
                item.attributes[k] = 10;
                markDirty();
                drawAttrs();
                renderAside();
            },
        }, "加一条属性"));
    };
    drawAttrs();
    const packEffects = allItems().filter(x => x.list === "equipEffects" && x.item.key).map(x => [x.item.key, x.item.name || x.item.key]);
    return h("div", {}, datalist("attrs", C.equipAttributes.map(a => [a, AttrLabels[a] || a])),
        h("fieldset", {}, h("legend", {}, "外观"), h("div", { class: "grid" },
            iconField(item, iconLists.equip(), after),
            fieldBox("进神器三选一", boolInput(item, "artifact", false), "内容包里的装备只能从神器三选一拿到", { check: true, path: "artifact" }))),
        h("fieldset", {}, h("legend", {}, "属性", h("small", {}, "名字用装备表的，百分比类写小数（0.2 = 20%）")),
            fieldBox("", attrs, null, { wide: true, path: "attributes" })),
        h("fieldset", {}, h("legend", {}, "特效"), h("div", { class: "grid" },
            fieldBox("这个包里的装备特效", packEffects.length ? chipsInput(item, "effects", packEffects, []) : h("span", { class: "hint" }, "先在左边“装备特效”里建一个"), null, { wide: true, path: "effects", key: "effects" }),
            fieldBox("游戏装备的特效", chipsInput(item, "gameEffects", C.gameEffects.filter(e => e !== "OldSword").map(e => [e, gameEffectLabel(e)]), []),
                "借用游戏装备自带的效果", { wide: true, path: "gameEffects", key: "gameEffects" }))));
}

function gameEffectLabel(e) {
    const eq = C.equipment.find(x => x.effect === e);
    return eq ? eq.name : e;
}

function variantFields(item, after, rerender) {
    const base = C.heroes.find(x => x.id === item.baseHero);
    const stats = VariantStats.map(([k, label, float]) => fieldBox(label, textInput(item, k, {
        number: true, placeholder: base && base.stats[k] !== null ? `原来 ${base.stats[k]}` : "", after,
    }), null, { path: k, key: k }));
    const kinds = [
        ["", "不换"], ["activeSkill", "换成包里的主动技能"], ["passiveSkill", "换成包里的被动"],
        ["gameActiveSkill", "换成游戏的主动技能"], ["gamePassiveSkill", "换成游戏的被动"],
    ];
    const cur = kinds.find(([k]) => k && own(item, k));
    const kind = cur ? cur[0] : "";
    const skillBox = h("div");
    const choose = h("select", {
        onchange: e => {
            for (const [k] of kinds) if (k) delete item[k];
            const k = e.target.value;
            if (k === "activeSkill" || k === "passiveSkill") {
                const first = allItems().find(x => x.list === (k === "activeSkill" ? "activeSkills" : "passiveSkills"));
                item[k] = first ? first.item.key : "";
            } else if (k) item[k] = k === "gameActiveSkill" ? C.activeSkills[0].id : C.passiveSkills[0].id;
            markDirty();
            rerender();
        },
    }, kinds.map(([k, l]) => h("option", { value: k, selected: k === kind }, l)));
    if (kind === "activeSkill" || kind === "passiveSkill") {
        const list = kind === "activeSkill" ? "activeSkills" : "passiveSkills";
        const options = allItems().filter(x => x.list === list).map(x => [x.item.key, x.item.name || x.item.key]);
        skillBox.append(selectInput(item, kind, options, undefined, after));
    } else if (kind) {
        const options = (kind === "gameActiveSkill" ? C.activeSkills : C.passiveSkills).map(s => [s.id, `${s.id} ${s.name}`]);
        skillBox.append(selectInput(item, kind, options, undefined, after));
    }
    if (base) {
        const exclusive = (base.active ? C.activeSkills : C.passiveSkills).find(s => s.id === base.skill);
        skillBox.append(h("div", { class: "hint" }, `原来的专属技能：${exclusive ? exclusive.name : base.skill}（${base.active ? "主动" : "被动"}）`));
    }
    return h("div", {},
        h("fieldset", {}, h("legend", {}, "原英雄"), h("div", { class: "grid" },
            fieldBox("基于哪个英雄", selectInput(item, "baseHero", C.heroes.map(x => [x.id, `${x.id} ${x.name}`]), undefined, rerender), null, { path: "baseHero", key: "baseHero" }))),
        h("fieldset", {}, h("legend", {}, "属性", h("small", {}, "不填就用原英雄的")), h("div", { class: "grid" }, stats)),
        h("fieldset", {}, h("legend", {}, "专属技能"), h("div", { class: "grid" },
            fieldBox("怎么换", choose, null, { path: "activeSkill" }),
            fieldBox("换成", skillBox, null, { path: kind || "skill" }))),
        h("fieldset", {}, h("legend", {}, "模型", h("small", {}, "两个都不填就用原英雄的模型")), h("div", { class: "grid" },
            fieldBox("包里的模型文件夹", textInput(item, "modelFolder", { after }), null, { path: "modelFolder", key: "modelFolder" }),
            fieldBox("创意工坊的模型", textInput(item, "workshopModel", { number: true, placeholder: "物品号", after }), "要先订阅", { path: "workshopModel", key: "workshopModel" }))));
}

// ---- 效果 ----

function triggerList(list) {
    if (list === "activeSkills") return ["onCast", "onEnd"];
    return V.PassiveTriggers;
}
function triggerLabel(list, t) {
    if (t === "onCast" && list !== "activeSkills") return PassiveCastLabel;
    if (list === "equipEffects" && t === "onAdded") return "穿上时";
    if (list === "equipEffects" && t === "onRemoved") return "卖掉时";
    if (list === "relics" && t === "onAdded") return "获得或升级时";
    return TriggerLabels[t];
}
const triggerHints = {
    onHit: "damage 是这次命中的伤害", onHurt: "damage 是掉的血", onKill: "", onAdded: "加属性不写持续秒数时，失去时自动撤销",
};

function effectsSection(item, list, rerender) {
    const inSkill = list === "activeSkills";
    const box = h("fieldset", {}, h("legend", {}, "效果", h("small", {}, inSkill ? "技能释放时按顺序执行" : "满足时机就按顺序执行")));
    const triggers = triggerList(list);
    const shown = triggers.filter(t => Array.isArray(item[t]) || own(item, t));
    for (const t of shown) box.append(triggerBlock(item, t, triggerLabel(list, t), t, inSkill, rerender));
    // onTick：{ every, do: [...] }
    if (own(item, "onTick")) {
        const tick = isObj(item.onTick) ? item.onTick : null;
        const head = h("div", { class: "trigger-head" }, h("b", {}, TriggerLabels.onTick),
            h("span", {}, inSkill ? "需要持续秒数大于 0" : ""),
            tick ? fieldBox("间隔秒数", textInput(tick, "every", { number: true, keepEmpty: true }), null, { path: "onTick.every" }) : null,
            h("button", { class: "quiet danger", onclick: () => { delete item.onTick; markDirty(); rerender(); } }, "去掉"));
        const block = h("div", { class: "trigger" }, head);
        fieldEls.set("onTick", block);
        if (tick) block.append(effectRows(tick, "do", "onTick.do", inSkill, rerender));
        box.append(block);
    }
    const missing = triggers.filter(t => !shown.includes(t));
    if (!own(item, "onTick")) missing.push("onTick");
    if (missing.length) {
        const pick = h("select", {}, missing.map(t => h("option", { value: t }, t === "onTick" ? TriggerLabels.onTick : triggerLabel(list, t))));
        box.append(h("div", { class: "row", style: "margin-top:12px;max-width:420px" }, h("span", { class: "hint" }, "加一个时机"), pick, h("button", {
            onclick: () => {
                const t = pick.value;
                if (t === "onTick") item.onTick = { every: 1, do: [{ heal: 10 }] };
                else item[t] = [defaultEffect(inSkill)];
                markDirty();
                rerender();
            },
        }, "添加")));
    }
    if (!shown.length && !own(item, "onTick")) box.insertBefore(h("p", { class: "empty-trigger" }, "还没有效果。选一个时机，再给它加效果。"), box.lastChild);
    return box;
}

function defaultEffect(inSkill) {
    return inSkill ? { damage: "area" } : { heal: 10 };
}

function triggerBlock(item, key, label, path, inSkill, rerender) {
    const block = h("div", { class: "trigger" },
        h("div", { class: "trigger-head" }, h("b", {}, label), h("span", {}, triggerHints[key] || ""),
            h("button", { class: "quiet danger", onclick: () => { delete item[key]; markDirty(); rerender(); } }, "去掉这个时机")));
    fieldEls.set(path, block);
    block.append(effectRows(item, key, path, inSkill, rerender));
    return block;
}

function effectRows(owner, key, path, inSkill, rerender) {
    const wrap = h("div");
    const list = Array.isArray(owner[key]) ? owner[key] : null;
    if (!list) {
        wrap.append(h("div", { class: "empty-trigger" }, "这里应该是效果的列表，在下面“直接改 JSON”里修。"));
        return wrap;
    }
    list.forEach((fx, i) => wrap.append(effectRow(list, i, `${path}[${i}]`, inSkill, rerender)));
    wrap.append(h("button", { class: "quiet", style: "margin:6px 0 0 14px", onclick: () => { list.push(defaultEffect(inSkill)); markDirty(); rerender(); } }, "+ 加一个效果"));
    return wrap;
}

function effectRow(list, i, path, inSkill, rerender) {
    const fx = list[i];
    if (!isObj(fx)) {
        const row = h("div", { class: "effect" }, h("span", {}, "写错了"), h("code", {}, JSON.stringify(fx)),
            h("button", { class: "quiet danger", onclick: () => { list.splice(i, 1); markDirty(); rerender(); } }, "删除"), h("div", { class: "msgs msg" }));
        fieldEls.set(path, row);
        return row;
    }
    const kinds = Object.keys(V.Kinds).filter(k => own(fx, k));
    const kind = kinds.length === 1 ? kinds[0] : null;
    const allowed = Object.keys(V.Kinds).filter(k => inSkill || (k !== "damage" && k !== "animation"));
    const kindSelect = h("select", {
        "aria-label": "效果",
        onchange: e => {
            const k = e.target.value;
            const keep = Object.fromEntries(Object.entries(fx).filter(([x]) => !own(V.Kinds, x)));
            const fresh = { damage: "area", heal: 10, mana: 5, gold: 10, addStats: { str: 10 }, addAttackSpeed: 0.2, addArmor: 10, effect: "Effect/SpellThunder", animation: Animations[0], notice: "" }[k];
            list[i] = { [k]: fresh, ...keep };
            markDirty();
            rerender();
        },
    }, !kind ? h("option", { selected: true }, kinds.length ? "写了好几种" : "没写效果") : null,
    allowed.concat(kind && !allowed.includes(kind) ? [kind] : []).map(k => h("option", { value: k, selected: k === kind }, KindLabels[k])));

    const main = h("div", { class: "extras" });
    const extra = (k, control) => {
        const [label, hint] = ExtraLabels[k] || [k, ""];
        const box = h("div", { class: "field" }, h("label", {}, label), control, h("div", { class: "msg" }));
        if (hint) control.title = hint;
        fieldEls.set(`${path}.${k}`, box);
        return box;
    };
    const exprInput = (obj, k, placeholder) => textInput(obj, k, { number: true, keepEmpty: true, placeholder: placeholder || "数字或公式" });
    if (kind === "damage") {
        main.append(extra("damage", selectInput(fx, "damage", [["area", "范围内的敌人"], ["target", "锁定的目标"]], undefined)));
        for (const k of ["scale", "amount", "radius"]) main.append(extra(k, textInput(fx, k, { number: true, placeholder: ExtraLabels[k][1] })));
    } else if (["heal", "mana", "gold", "addAttackSpeed", "addArmor"].includes(kind)) {
        const unit = { heal: "比如 50 或 maxHp * 0.1", mana: "比如 5", gold: "只在单人时给", addAttackSpeed: "比如 0.2", addArmor: "比如 10" }[kind];
        main.append(extra(kind, h("input", {
            value: typeof fx[kind] === "string" || typeof fx[kind] === "number" ? String(fx[kind]) : "", placeholder: unit, style: "min-width:220px",
            oninput: e => setField(fx, kind, e.target.value === "" ? "" : parseExpr(e.target.value)),
        })));
    } else if (kind === "addStats") {
        const st = isObj(fx.addStats) ? fx.addStats : null;
        if (st) for (const [k, l] of [["str", "力量"], ["agi", "敏捷"], ["sta", "耐力"], ["attack", "攻击力"]]) {
            const box = h("div", { class: "field" }, h("label", {}, l), textInput(st, k, { number: true, placeholder: "不加" }), h("div", { class: "msg" }));
            fieldEls.set(`${path}.addStats.${k}`, box);
            main.append(box);
        }
    } else if (kind === "effect") {
        main.append(h("div", {}, datalist("fxpaths", C.effects.map(e => [e, e]))), extra("effect", textInput(fx, "effect", { list: "fxpaths", keepEmpty: true, style: "min-width:240px" })));
        main.append(extra("at", selectInput(fx, "at", [["position", "技能位置 / 事件目标"], ["self", "自己"], ["target", "目标"]], "position")));
    } else if (kind === "animation") {
        main.append(h("div", {}, datalist("anims", Animations.map(a => [a, a]))), extra("animation", textInput(fx, "animation", { list: "anims", keepEmpty: true, style: "min-width:240px" })));
    } else if (kind === "notice") {
        main.append(extra("notice", textInput(fx, "notice", { keepEmpty: true, placeholder: "可以用 {0}", style: "min-width:240px" })));
    }
    if (kind) for (const k of V.Kinds[kind].filter(k => !["scale", "amount", "radius", "at"].includes(k)))
        main.append(extra(k, exprInput(fx, k, ExtraLabels[k] ? ExtraLabels[k][1] : "")));
    // 条件
    main.append(extra("chance", exprInput(fx, "chance", "100")));
    main.append(extra("hpBelow", exprInput(fx, "hpBelow", "不限")));
    main.append(extra("isBoss", selectInput(fx, "isBoss", [[undefined, "不限"], [true, "只对 Boss"], [false, "只对非 Boss"]], undefined)));
    main.append(extra("cooldown", textInput(fx, "cooldown", { number: true, placeholder: "0" })));

    const move = (d) => {
        const j = i + d;
        if (j < 0 || j >= list.length) return;
        [list[i], list[j]] = [list[j], list[i]];
        markDirty();
        rerender();
    };
    const row = h("div", { class: "effect" }, kindSelect, main,
        h("div", { class: "tools" },
            h("button", { class: "quiet", title: "上移", "aria-label": "上移", onclick: () => move(-1) }, "↑"),
            h("button", { class: "quiet", title: "下移", "aria-label": "下移", onclick: () => move(1) }, "↓"),
            h("button", { class: "quiet danger", title: "删除", "aria-label": "删除这个效果", onclick: () => { list.splice(i, 1); markDirty(); rerender(); } }, "✕")),
        h("div", { class: "msgs msg" }));
    fieldEls.set(path, row);
    return row;
}

// ---- 直接改 JSON ----

// “直接改 JSON”展开着时，重绘编辑区（应用、加效果等）后保持展开；换条目时收起
let rawOpen = false;

function rawSection(item, rerender) {
    // 表单改了以后框里的文字会过时：展开时、还没动过就开始编辑时，重新从条目填一遍
    let edited = false;
    const refresh = () => {
        if (!edited) area.value = formatJson(item, 99).trim();
    };
    const area = h("textarea", {
        rows: 10, spellcheck: false, style: "font-family:Consolas, monospace;font-size:13px",
        onfocus: refresh, oninput: () => { edited = true; },
    });
    const box = fieldBox("", area, "表单里没有的字段、或者想一次改很多时用。改完点“应用”。", { wide: true, path: "$raw" });
    const { path, list, index } = S.sel;
    return h("details", { style: "margin-top:24px", open: rawOpen, ontoggle: e => { rawOpen = e.target.open; refresh(); } }, h("summary", { style: "cursor:pointer;color:var(--dim)" }, "直接改 JSON"),
        box, h("button", {
            onclick: () => {
                try {
                    const v = V.parseJson(area.value);
                    if (!isObj(v)) throw new Error("应该是 { … }");
                    S.files[path][list][index] = v;
                    markDirty();
                    rerender();
                    renderNav();
                } catch (e) {
                    box.querySelector(".msg").textContent = e.message;
                }
            },
        }, "应用"));
}

// ---- 右栏：游戏里的样子 + 这一条的问题 ----

let previewLevel = 0;

function renderAside() {
    const aside = $("#aside");
    aside.textContent = "";
    if (!S.manifest) return;
    if (S.sel.page !== "item") {
        const r = S.result;
        aside.append(h("h3", { style: "margin-top:0" }, "这个包"));
        const items = allItems();
        aside.append(h("dl", { class: "card", style: "display:grid;grid-template-columns:auto 1fr;gap:4px 12px;margin:0" },
            Object.entries(Lists).flatMap(([list, info]) => {
                const n = items.filter(x => x.list === list).length;
                return n ? [h("dt", { style: "color:var(--dim)" }, info.label), h("dd", { style: "margin:0" }, n)] : [];
            }), items.length ? null : h("dd", { style: "margin:0;color:var(--dim)" }, "还是空的")));
        if (r) {
            const mine = [...r.errors.filter(x => x.where === "dbmod.json").map(x => [x, "e"]), ...r.warnings.filter(x => x.where === "dbmod.json").map(x => [x, "w"])];
            if (mine.length) {
                aside.append(h("h3", {}, "dbmod.json"));
                for (const [x, l] of mine) aside.append(issueButton(x, l));
            }
        }
        return;
    }
    const item = currentItem();
    if (!item) return;
    let card = previewCard(item, S.sel.list);
    aside.append(h("h3", { style: "margin-top:0" }, "游戏里大概的样子"), card);
    // 拖滑块时只换卡片，不重画滑块
    if (Array.isArray(item.levelUp) && item.levelUp.length && S.sel.list !== "activeSkills") {
        const label = h("span", {}, `${previewLevel} 级`);
        aside.append(h("div", { class: "level" }, "预览等级", h("input", {
            type: "range", min: 0, max: 10, value: previewLevel, "aria-label": "预览等级",
            oninput: e => {
                previewLevel = +e.target.value;
                label.textContent = `${previewLevel} 级`;
                const next = previewCard(item, S.sel.list);
                card.replaceWith(next);
                card = next;
            },
        }), label));
    }
    const issues = issuesOf(S.sel.path, S.sel.list, S.sel.index);
    aside.append(h("h3", {}, "这一条的检查"));
    if (!issues.length) aside.append(h("div", { class: "all-good" }, "没有问题"));
    const prefix = `${labelOf(S.sel.path)}.${S.sel.list}[${S.sel.index}]`;
    for (const x of issues) aside.append(issueButton(x, x.level, false, prefix));
}

function previewCard(item, list) {
    const q = V.Qualities.includes(item.quality) ? item.quality : ["equipment", "equipEffects", "heroVariants"].includes(list) ? "B" : "C";
    const values = Array.isArray(item.values) ? item.values.filter(v => typeof v === "number") : [];
    const ups = Array.isArray(item.levelUp) ? item.levelUp : [];
    const types = Array.isArray(item.valueTypes) ? item.valueTypes : [];
    const value = i => (values[i] || 0) + (typeof ups[i] === "number" ? ups[i] * previewLevel : 0);
    const show = i => {
        const v = value(i), t = types[i];
        if (t === "Percent") return fmtNum(v * 100) + "%";
        if (t === "PercentConst") return fmtNum(v) + "%";
        if (t === "InversePercent") return fmtNum((1 - v) * 100) + "%";
        return fmtNum(v);
    };
    const damageText = () => {
        const base = typeof item.damageBase === "number" ? item.damageBase : 0;
        const k = typeof item.damageValue === "number" ? item.damageValue : 0;
        const attr = DamageTypes[typeof item.damageType === "number" ? item.damageType : 0] || "?";
        return k ? `${base} + ${attr}×${fmtNum(k)}` : String(base);
    };
    // 说明：{n} 换成数值，<color=…> 照游戏显示
    let desc = esc(typeof item.description === "string" ? item.description : "");
    desc = desc.replace(/\{(\d+)\}/g, (_, n) => {
        if (list === "activeSkills" && !values.length && n === "0") return `<em>${esc(damageText())}</em>`;
        return +n < values.length ? `<em>${show(+n)}</em>` : `<em style="color:var(--error)">{${n}}</em>`;
    });
    desc = desc.replace(/&lt;color=(#[0-9a-fA-F]{3,8}|\w+)&gt;([\s\S]*?)&lt;\/color&gt;/g, (_, c, t) => `<span style="color:${c}">${t}</span>`);

    const iconName = typeof item.icon === "string" ? item.icon : "";
    const img = iconName && S.images.get((normPath(iconName) || "").toLowerCase());
    const icon = h("div", { class: "icon", title: iconName }, img ? h("img", { src: img, alt: "" }) : iconName || (list === "activeSkills" || list === "passiveSkills" ? "SpellBook01_66" : "无图标"));
    const card = h("div", { class: "card", style: `--q:var(--q-${q})` }, list !== "equipEffects" && list !== "heroVariants" ? icon : null,
        h("div", { class: "name" }, item.name || "（没有名字）"),
        h("div", { class: "sub" }, `${Lists[list].one}  ${["activeSkills", "passiveSkills", "relics"].includes(list) ? q + " 级" : ""}`));
    const d = h("div", { class: "desc" });
    d.innerHTML = desc || "<span style='color:var(--dim)'>没有说明</span>";
    card.append(d);

    const stats = [];
    const num = (k, def) => typeof item[k] === "number" ? item[k] : def;
    if (list === "activeSkills") {
        stats.push(["蓝耗", num("cost", 20)], ["冷却", num("cooldown", 8) + " 秒"], ["施法距离", num("castingRange", 8)], ["作用半径", num("range", 3)]);
        if (num("duration", 0) > 0) stats.push(["持续", num("duration", 0) + " 秒"]);
        if (num("charges", 0) > 0) stats.push(["充能", `${num("charges", 0)} 次，每次 ${num("chargeTime", 0)} 秒`]);
        stats.push(["伤害公式", damageText()]);
    }
    if (list === "equipment" && isObj(item.attributes))
        for (const [k, v] of Object.entries(item.attributes)) stats.push([AttrLabels[k] || k, v]);
    if (list === "equipment") {
        const fx = Array.isArray(item.effects) ? item.effects : [];
        for (const k of fx) {
            const e = allItems().find(x => x.list === "equipEffects" && x.item.key === k);
            stats.push(["特效", e ? e.item.name : `${k}（没有这个特效）`]);
        }
        for (const g of Array.isArray(item.gameEffects) ? item.gameEffects : []) stats.push(["特效", gameEffectLabel(g)]);
    }
    if (list === "equipEffects" && Array.isArray(item.attachTo))
        stats.push(["挂在", item.attachTo.map(id => (C.equipment.find(e => e.id === id) || { name: `${id} 号` }).name).join("、")]);
    if (list === "heroVariants") {
        const base = C.heroes.find(x => x.id === item.baseHero);
        stats.push(["原英雄", base ? base.name : "（没选）"]);
        for (const [k, label] of VariantStats)
            if (own(item, k)) stats.push([label, base && base.stats[k] !== null ? `${base.stats[k]} → ${item[k]}` : item[k]]);
    }
    if (stats.length) card.append(h("dl", { class: "stats" }, stats.flatMap(([k, v]) => [h("dt", {}, k), h("dd", {}, String(v))])));
    return card;
}

// ---- 启动 ----

$("#btnOpen").addEventListener("click", openFolder);
$("#btnNew").addEventListener("click", newPack);
$("#btnSave").addEventListener("click", save);
$("#btnZip").addEventListener("click", downloadZip);
$("#btnInstall").addEventListener("click", installToGame);
$("#btnIssues").addEventListener("click", () => { S.issuesOpen = !S.issuesOpen; renderIssues(); });
$("#folderInput").addEventListener("change", e => openFromInput([...e.target.files]));
window.addEventListener("beforeunload", e => { if (S.dirty) e.preventDefault(); });
document.addEventListener("keydown", e => {
    if ((e.ctrlKey || e.metaKey) && e.key === "s") {
        e.preventDefault();
        if (S.manifest) save();
    }
});
render();
