# 生成工坊条目的预览图（512x512，和 Runtime/preview.png 同一风格）：python workshop/previews.py
import os
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.abspath(__file__))
FONTS = "C:/Windows/Fonts/"
GOLD = (214, 178, 112)
WHITE = (236, 236, 236)
GRAY = (170, 170, 175)

# 条目名 → (大标题, 中文副标题, 英文副标题)
ITEMS = {
    "SmartSkill": ("SmartSkill", "技能自动释放条件", "Smarter auto-cast"),
    "SmartMove": ("SmartMove", "自动躲避预警", "Auto-dodge telegraphs"),
    "AutoPickup": ("AutoPickup", "自动拾取", "Auto pickup"),
    "DamageMeter": ("DamageMeter", "伤害统计", "Damage meter"),
    "Hextech": ("Hextech", "海克斯强化", "Hextech augments"),
    "DBStudio": ("DBStudio", "Mod 工作台", "In-game mod workbench"),
    "Frieren": ("Frieren", "芙莉莲 · 无限魔力", "Infinite Mana"),
}


def font(name, size):
    return ImageFont.truetype(FONTS + name, size)


def fit(draw, text, name, size, width):
    while size > 20:
        f = font(name, size)
        if draw.textlength(text, font=f) <= width:
            return f
        size -= 4
    return font(name, size)


def background():
    img = Image.new("RGB", (512, 512))
    px = img.load()
    for y in range(512):
        for x in range(512):
            t = (x + y) / 1022
            px[x, y] = (int(38 - 14 * t), int(40 - 14 * t), int(48 - 14 * t))
    d = ImageDraw.Draw(img)
    d.rectangle([14, 14, 497, 497], outline=GOLD, width=2)
    d.rectangle([26, 26, 485, 485], outline=(120, 100, 66), width=1)
    return img, d


def center(d, y, text, f, fill):
    w = d.textlength(text, font=f)
    d.text(((512 - w) / 2, y), text, font=f, fill=fill)


for key, (title, zh, en) in ITEMS.items():
    img, d = background()
    center(d, 118, title, fit(d, title, "msyhbd.ttc", 92, 430), GOLD)
    center(d, 238, zh, font("msyh.ttc", 44), WHITE)
    center(d, 298, en, font("msyh.ttc", 30), GRAY)
    d.line([120, 366, 392, 366], fill=(110, 110, 116), width=1)
    center(d, 396, "需要 DBModKit · 地下城竞技场", font("msyh.ttc", 24), GRAY)
    out = os.path.join(ROOT, key, "preview.png")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    img.save(out, optimize=True)
    print(out)
