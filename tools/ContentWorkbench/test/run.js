// 工作台的检查和游戏里的读包代码对一遍：cases/*.json 的错误要和 expected.json 一字不差。
// expected.json 由 DBModKit 的 ContentReader 生成（见 README 的"改了读包规则之后"）。
// 用法：node test/run.js
const fs = require("fs");
const path = require("path");
const check = require("../validate.js");

const dir = path.join(__dirname, "cases");
const expected = JSON.parse(fs.readFileSync(path.join(__dirname, "expected.json"), "utf8"));
let fails = 0;
const ok = (cond, what) => {
    console.log((cond ? "PASS " : "FAIL ") + what);
    if (!cond) fails++;
};

for (const name of Object.keys(expected)) {
    const text = fs.readFileSync(path.join(dir, name), "utf8");
    let errors;
    try {
        const ctx = {
            icon(v) {
                if (!/\.(png|jpe?g)$/i.test(v)) return null;
                return fs.existsSync(path.join(dir, v)) ? null : `找不到图片 ${v}`;
            },
        };
        errors = check.checkFile(check.parseJson(text.replace(/^﻿/, "")), name, ctx).errors.map(e => e.text);
    } catch (e) {
        errors = [`${name}：读不了`];
    }
    const want = expected[name];
    const same = errors.length === want.length && errors.every((e, i) => e === want[i]);
    ok(same, `${name}（${want.length} 条错误）`);
    if (!same) {
        for (const e of want.filter(e => !errors.includes(e))) console.log("    游戏有、工作台没有：" + e);
        for (const e of errors.filter(e => !want.includes(e))) console.log("    工作台有、游戏没有：" + e);
        if (want.every(e => errors.includes(e)) && errors.every(e => want.includes(e))) console.log("    （内容一样，顺序不同）");
    }
}

// 整个包：注册阶段的检查（游戏里要 Unity 才能跑，这里按 ContentPacks.Load 的写法直接断言）
const files = {
    "a.json": {
        activeSkills: [{ key: "nova", name: "雷暴" }, { key: "nova", name: "重复" }],
        equipEffects: [{ key: "fx", name: "特效" }, { key: "bad", name: "" }],
        equipment: [{ key: "q", name: "装备", effects: ["fx"] }, { key: "q2", name: "装备2", effects: ["bad"] }],
        heroVariants: [
            { key: "v", name: "变体", baseHero: 1, activeSkill: "nova" },
            { key: "v2", name: "变体2", baseHero: 1, passiveSkill: "nope" },
            { key: "v3", name: "变体3", baseHero: 1, modelFolder: "../x" },
        ],
    },
    "b.json": { activeSkills: [{ key: "nova", name: "另一个文件里重复" }] },
};
const r = check.checkPack({
    manifest: { Type: "content", Id: "t.t", Name: "t", Version: "1", Content: ["a.json", "./b.json", "c.json", "../d.json"] },
    files, exists: () => false,
});
const texts = r.errors.map(e => e.text);
ok(texts.includes("主动技能 nova：技能 nova 已经注册过") && texts.filter(t => t.includes("已经注册过")).length === 2, "同一个包里重复的键（跨文件也算）");
ok(texts.includes("a.json.equipment[1].effects：这个包里没有键为 \"bad\" 的装备特效"), "装备引用写错的特效");
ok(!texts.some(t => t.startsWith("a.json.heroVariants[0]")), "变体引用包里的主动技能");
ok(texts.includes("a.json.heroVariants[1].passiveSkill：这个包里没有键为 \"nope\" 的被动"), "变体引用不存在的被动");
ok(texts.includes("a.json.heroVariants[2].modelFolder：路径不在内容包文件夹里：../x"), "模型文件夹在包外面");
ok(texts.includes("找不到 c.json") && texts.includes("路径不在内容包文件夹里：../d.json"), "Content 里的文件找不到 / 在包外面");
const empty = check.checkPack({ manifest: { Content: [] }, files: {}, exists: () => false });
ok(empty.errors.some(e => e.text === "dbmod.json 的 Content 里没有列出 JSON 文件"), "Content 为空");
ok(empty.warnings.some(e => e.text.includes("没写 Id")) && empty.errors.some(e => e.text.includes("Type 要写")), "提醒没写 Id；Type 不对算错误");

// dbmod.json 字段名不区分大小写（Newtonsoft）
const lower = check.checkPack({ manifest: { type: "content", id: "x", name: "x", version: "1", content: ["a.json"] }, files: { "a.json": {} }, exists: () => false });
ok(lower.errors.length === 0, "dbmod.json 字段名写成小写也认");

// 装备：游戏注册时不认识的属性、特效整件拒绝；别名不分大小写
const catalog = { equipAttributes: ["attack", "hp", "normalAttackAddDamage"], equipAttributeAliases: { physicaldamage: "normalAttackAddDamage" },
    equipSkillTypes: ["Sputtering"], icons: new Set(), equipment: [], effects: [], activeSkills: [], passiveSkills: [] };
const eq = check.checkPack({
    manifest: { Type: "content", Id: "x", Name: "x", Version: "1", MinModKit: "0.24.0", Content: ["a.json"] }, catalog, exists: () => false,
    files: { "a.json": { equipment: [
        { key: "a", name: "a", attributes: { attack: 1, PhysicalDamage: 2 }, gameEffects: ["Sputtering"] },
        { key: "b", name: "b", attributes: { Attack: 1 } },
        { key: "c", name: "c", gameEffects: ["Nope"] },
        { key: "d", name: "d", gameEffects: ["0"] },
    ] } },
});
const eqt = eq.errors.map(e => e.text);
ok(eqt.length === 3, `装备属性 / 特效：${eqt.length} 条错误`);
ok(eqt.includes("装备 b：装备 b 的 Attributes 里有不认识的属性 \"Attack\"（可以用的见 CustomEquipment.AttributeNames）"), "属性名区分大小写");
ok(eqt.some(t => t.startsWith("装备 c：") && t.includes("Nope")) && eqt.some(t => t.startsWith("装备 d：")), "不认识的游戏特效、数字特效名");

const desc = check.checkPack({
    manifest: { Type: "content", Id: "x", Name: "x", Version: "1", Content: ["a.json"] },
    files: { "a.json": { passiveSkills: [{ key: "p", name: "p", description: "{0} {1}", values: [1], onHit: [{ heal: "{2}" }] }] } },
    exists: () => false,
});
ok(desc.warnings.some(w => w.text.includes("description：{1} 没有对应的数值")), "提醒：说明里的 {n} 超出 values");
ok(desc.warnings.some(w => w.text.includes("onHit[0].heal：{2} 没有对应的数值")), "提醒：表达式里的 {n} 超出 values");

console.log(fails === 0 ? "全部通过" : `${fails} 项失败`);
process.exit(fails === 0 ? 0 : 1);
