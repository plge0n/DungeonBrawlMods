# 生成 catalog.js：工作台下拉框里的游戏数据（英雄、技能、遗物、装备、图标名、特效路径）
# 用法：python gen-catalog.py --excel <导出的配置表文件夹> --decompiled <反编译源码文件夹>
# 需要 openpyxl。游戏更新后重新跑一次。
import argparse
import json
import os
import re

import openpyxl

ap = argparse.ArgumentParser()
ap.add_argument("--excel", required=True)
ap.add_argument("--decompiled", required=True)
ap.add_argument("--out", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "catalog.js"))
args = ap.parse_args()


# 表的前 5 行是字段名、类型、默认值、空行、中文说明，第 6 行起是数据
def table(name):
    ws = openpyxl.load_workbook(os.path.join(args.excel, name + ".xlsx"), read_only=True).active
    rows = list(ws.iter_rows(values_only=True))
    head = rows[0]
    out = []
    for r in rows[5:]:
        if r[0] is None:
            continue
        out.append({h: v for h, v in zip(head, r) if h})
    return out


def text(v):
    return "" if v is None else str(v).strip()


def num(v):
    if v is None or v == "":
        return None
    f = float(v)
    return int(f) if f.is_integer() else f


heroes = []
for r in table("hero"):
    heroes.append({
        "id": int(r["id"]), "name": text(r["name"]), "active": bool(r.get("zhuDong")), "skill": num(r.get("skill")),
        # 变体英雄表单里当占位符显示原英雄的数值
        "stats": {k: num(r.get(src)) for k, src in [
            ("strength", "STR"), ("agility", "AGI"), ("stamina", "STA"),
            ("strengthGrowth", "STRadd"), ("agilityGrowth", "AGIadd"), ("staminaGrowth", "STAadd"),
            ("attack", "attack"), ("armor", "Armor"), ("hp", "hp"), ("mp", "mp"),
            ("hpRecover", "hpRecover"), ("mpRecover", "mpRecover"), ("moveSpeed", "moveSpeed"), ("attackSpeed", "attackSpeed")]},
    })

actives = [{"id": int(r["id"]), "name": text(r["name"]), "icon": text(r.get("icon")), "quality": text(r.get("quality"))}
           for r in table("activeSkill")]
passives = [{"id": int(r["id"]), "name": text(r["name"]), "icon": text(r.get("icon")), "quality": text(r.get("quality"))}
            for r in table("passsiveSkill") if re.fullmatch(r"\d+", text(r["id"]))]
relics = [{"id": int(r["id"]), "name": text(r["name"]), "icon": text(r.get("icon")), "quality": text(r.get("grade"))}
          for r in table("remains")]
equipment = [{"id": int(r["id"]), "name": text(r["name"]), "icon": text(r.get("equipmentIcon")),
              "artifact": bool(r.get("isArtifact")), "effect": text(r.get("skill"))}
             for r in table("equipment")]

# 装备属性名：jy 里的属性名列表（游戏认的全部键）
jy = open(os.path.join(args.decompiled, "jy.cs"), encoding="utf-8").read()
m = re.search(r"string\[\] \w+ = new string\[\d+\]\s*\{([^}]*)\}", jy)
attributes = re.findall(r'"(\w+)"', m.group(1))
# 属性名的别名（不分大小写，游戏读表和 DBModKit 注册装备时都会先换成正式名字）
m = re.search(r"Dictionary<string, string> \w+ = new Dictionary<string, string>\(StringComparer\.OrdinalIgnoreCase\)\s*\{(.*?)\n\t\};", jy, re.S)
aliases = {a.lower(): b for a, b in re.findall(r'\{\s*"(\w+)",\s*"(\w+)"\s*\}', m.group(1))}
# 装备特效：EquipSkillType 的全部名字（DBModKit 只认这些）
skill_types = [n for n in re.findall(r"^\s*(\w+)\s*(?:=\s*-?\d+)?\s*,?\s*$",
               open(os.path.join(args.decompiled, "EquipSkillType.cs"), encoding="utf-8").read(), re.M) if n != "None"]

effects = set()
for root, _, files in os.walk(args.decompiled):
    for f in files:
        if f.endswith(".cs"):
            effects.update(re.findall(r'"(Effect/[A-Za-z0-9_/]+)"', open(os.path.join(root, f), encoding="utf-8", errors="ignore").read()))

catalog = {
    "heroes": heroes,
    "activeSkills": actives,
    "passiveSkills": passives,
    "relics": relics,
    "equipment": equipment,
    "equipAttributes": attributes,
    "equipAttributeAliases": aliases,
    "equipSkillTypes": skill_types,
    "gameEffects": sorted({e["effect"] for e in equipment if e["effect"]}),
    "effects": sorted(effects),
}
with open(args.out, "w", encoding="utf-8", newline="\n") as f:
    f.write("// 由 gen-catalog.py 从游戏配置表生成，不要手改\n")
    f.write("window.CATALOG = ")
    json.dump(catalog, f, ensure_ascii=False, separators=(",", ":"))
    f.write(";\n")
print(f"{len(heroes)} 英雄、{len(actives)} 主动、{len(passives)} 被动、{len(relics)} 遗物、{len(equipment)} 装备、"
      f"{len(attributes)} 属性、{len(aliases)} 别名、{len(skill_types)} 装备特效类型、{len(effects)} 特效 → {args.out}")
