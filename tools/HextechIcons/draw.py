"""画 Hextech 手写卡（通用卡、招牌卡）的图标：儿童画 / 蜡笔风格，纸片底色按品质区分，不写字。
用法：python draw.py   →  Hextech/assets/icons/<卡的 Id>.png，外加预览图 _sheet.png（不随插件发布）
要 Pillow（pip install pillow）。随机种子固定，重跑结果一样。"""
import math
import os
from PIL import Image
from crayon import Icon, circle

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "..", "Hextech", "assets", "icons")
INK = (45, 40, 45)
RED, BLUE, GOLD, GRAY = (215, 50, 50), (70, 120, 230), (240, 185, 40), (150, 155, 165)


def arc(cx, cy, r, a0, a1, n=24, sy=1.0):
    return [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * i / n)), cy + r * sy * math.sin(math.radians(a0 + (a1 - a0) * i / n)))
            for i in range(n + 1)]


def heart(cx, cy, s):
    pts = []
    for i in range(60):
        t = i / 60 * 2 * math.pi
        x = 16 * math.sin(t) ** 3
        y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        pts.append((cx + x * s / 17, cy - y * s / 17))
    return pts


def drop(cx, cy, r):
    return [(cx, cy - r * 1.9)] + [(cx + r * math.cos(math.radians(d)), cy + r * math.sin(math.radians(d))) for d in range(-30, 211, 15)]


def star(cx, cy, r1, r2, n=8, rot=0):
    return [(cx + (r1 if i % 2 == 0 else r2) * math.cos(math.radians(rot + i * 180 / n)),
             cy + (r1 if i % 2 == 0 else r2) * math.sin(math.radians(rot + i * 180 / n))) for i in range(2 * n)]


def shape(ic, poly, color, width=0.03):
    ic.solid(poly, color)
    ic.line(poly, closed=True, width=width)


def anvil(ic, color, dx=0.0, dy=0.0, k=1.0):
    p = [(0.18, 0.42), (0.72, 0.42), (0.86, 0.36), (0.84, 0.47), (0.66, 0.53), (0.6, 0.62), (0.68, 0.74),
         (0.32, 0.74), (0.4, 0.62), (0.34, 0.53), (0.2, 0.5)]
    p = [(0.5 + (x - 0.5) * k + dx, 0.5 + (y - 0.5) * k + dy) for x, y in p]
    shape(ic, p, color)


def foot(cx, cy, s, flip=False, rot=0.0):
    sole = [(0.0, -0.33), (0.14, -0.28), (0.18, -0.1), (0.12, 0.1), (0.13, 0.24), (0.02, 0.33), (-0.1, 0.28),
            (-0.11, 0.1), (-0.16, -0.1), (-0.13, -0.28)]
    toes = [(-0.1, -0.41, 0.06), (0.01, -0.44, 0.045), (0.08, -0.42, 0.04), (0.14, -0.38, 0.035), (0.18, -0.32, 0.03)]
    c, s_ = math.cos(math.radians(rot)), math.sin(math.radians(rot))

    def t(x, y):
        x = -x if flip else x
        return (cx + (x * c - y * s_) * s, cy + (x * s_ + y * c) * s)
    return [t(x, y) for x, y in sole], [(*t(x, y), r * s) for x, y, r in toes]


def draw_foot(ic, cx, cy, s, color, flip=False, rot=0.0):
    sole, toes = foot(cx, cy, s, flip, rot)
    shape(ic, sole, color, 0.025)
    for x, y, r in toes:
        c = circle(x, y, r, 20)
        shape(ic, c, color, 0.02)


# ---------- 银 ----------
def blunt():  # 大力：哑铃
    ic = Icon(101, "silver")
    ic.line([(0.3, 0.5), (0.7, 0.5)], color=GRAY, width=0.07, passes=1)
    ic.line([(0.3, 0.5), (0.7, 0.5)], width=0.02, passes=1)
    for x in (0.22, 0.78):
        p = [(x - 0.07, 0.28), (x + 0.07, 0.28), (x + 0.07, 0.72), (x - 0.07, 0.72)]
        shape(ic, p, (60, 60, 70))
    for x, y in ((0.5, 0.25), (0.42, 0.78), (0.6, 0.8)):
        ic.line([(x, y - 0.03), (x, y + 0.03)], color=RED, width=0.02, passes=1)
    return ic


def deft():  # 灵巧：羽毛 + 速度线
    ic = Icon(102, "silver")
    feather = [(0.72, 0.18), (0.8, 0.3), (0.72, 0.5), (0.52, 0.7), (0.34, 0.76), (0.36, 0.6), (0.5, 0.4)]
    shape(ic, feather, (120, 200, 170))
    ic.line([(0.76, 0.22), (0.26, 0.84)], width=0.022)
    for i, y in enumerate((0.36, 0.48, 0.6)):
        ic.line([(0.12, y + 0.1), (0.28 - i * 0.03, y + 0.1)], color=BLUE, width=0.018, passes=1)
    return ic


def goredrink():  # 嗜血：血滴
    ic = Icon(103, "silver")
    d = drop(0.5, 0.6, 0.24)
    shape(ic, d, (210, 40, 50))
    ic.line([(0.42, 0.55), (0.4, 0.66)], color=(255, 250, 245), width=0.025, passes=1)
    return ic


def stats():  # 属性！：铁砧 + 火星
    ic = Icon(104, "silver")
    anvil(ic, GRAY)
    for x, y in ((0.3, 0.26), (0.5, 0.2), (0.7, 0.26)):
        ic.sparkle(x, y, 0.04, (240, 120, 40))
    return ic


def goldrush():  # 淘金：镐子 + 金块
    ic = Icon(105, "silver")
    ic.line([(0.28, 0.82), (0.6, 0.22)], color=(150, 100, 60), width=0.06, passes=1)
    ic.line([(0.28, 0.82), (0.6, 0.22)], width=0.018, passes=1)
    head = arc(0.6, 0.44, 0.25, -155, -25, 16)
    ic.line(head, color=GRAY, width=0.07, passes=1)
    ic.line(head, width=0.02, passes=1)
    nug = [(0.62, 0.66), (0.74, 0.6), (0.84, 0.66), (0.82, 0.78), (0.68, 0.8), (0.6, 0.74)]
    shape(ic, nug, GOLD)
    ic.sparkle(0.86, 0.52, 0.035)
    return ic


def mind():  # 由心及物：红心里一滴蓝
    ic = Icon(106, "silver")
    shape(ic, heart(0.5, 0.52, 0.3), (230, 80, 90))
    shape(ic, drop(0.5, 0.58, 0.09), BLUE, 0.02)
    return ic


# ---------- 金 ----------
def recursion():  # 循环往复：转圈的箭头
    ic = Icon(201, "gold")
    a = arc(0.5, 0.52, 0.26, -60, 250, 30)
    ic.line(a, color=BLUE, width=0.07, passes=1)
    ic.line(a, width=0.02, passes=1)
    tip = a[-1]
    head = [(tip[0] - 0.1, tip[1] - 0.02), (tip[0] + 0.06, tip[1] - 0.1), (tip[0] + 0.04, tip[1] + 0.08)]
    shape(ic, head, BLUE, 0.02)
    return ic


def critical():  # 暴击：爆炸星
    ic = Icon(202, "gold")
    shape(ic, star(0.5, 0.5, 0.34, 0.16, 9, 10), (245, 140, 40))
    shape(ic, star(0.5, 0.5, 0.17, 0.08, 7, 30), (250, 225, 70), 0.02)
    return ic


def colossus():  # 巨像：大红心 + 加号
    ic = Icon(203, "gold")
    shape(ic, heart(0.48, 0.55, 0.32), (220, 60, 70))
    ic.line([(0.74, 0.18), (0.74, 0.34)], color=(60, 170, 80), width=0.04, passes=1)
    ic.line([(0.66, 0.26), (0.82, 0.26)], color=(60, 170, 80), width=0.04, passes=1)
    return ic


def axiom():  # 奥术公理：沙漏
    ic = Icon(204, "gold")
    glass = [(0.3, 0.2), (0.7, 0.2), (0.54, 0.5), (0.7, 0.8), (0.3, 0.8), (0.46, 0.5)]
    ic.solid(glass, (200, 225, 245))
    ic.fill([(0.38, 0.8), (0.62, 0.8), (0.52, 0.62), (0.48, 0.62)], (150, 90, 210), density=0.9)
    ic.fill([(0.36, 0.28), (0.64, 0.28), (0.52, 0.46), (0.48, 0.46)], (150, 90, 210), density=0.9)
    ic.line(glass, closed=True)
    for y in (0.17, 0.83):
        ic.line([(0.24, y), (0.76, y)], color=(150, 100, 60), width=0.05, passes=1)
    return ic


def manamune():  # 魔宗：蓝色的剑
    ic = Icon(205, "gold")
    blade = [(0.74, 0.16), (0.8, 0.22), (0.44, 0.62), (0.38, 0.56)]
    shape(ic, blade, (120, 170, 245))
    ic.line([(0.3, 0.5), (0.5, 0.7)], color=(150, 100, 60), width=0.05, passes=1)
    ic.line([(0.38, 0.62), (0.22, 0.8)], color=(110, 70, 50), width=0.05, passes=1)
    shape(ic, circle(0.2, 0.82, 0.04, 16), BLUE, 0.02)
    ic.sparkle(0.82, 0.48, 0.035, BLUE)
    return ic


def bone(ic, cx, cy, k=1.0, rot=0.0):
    """一根骨头（中心 cx, cy，k 缩放，rot 旋转角度）。"""
    base = [(0.3, 0.42), (0.68, 0.42)] + circle(0.74, 0.4, 0.07)[36:48] + circle(0.74, 0.4, 0.07)[0:6] +            circle(0.74, 0.56, 0.07)[0:18] + [(0.68, 0.54), (0.3, 0.54)] + circle(0.25, 0.56, 0.07)[12:30] + circle(0.25, 0.4, 0.07)[24:42]
    c, s_ = math.cos(math.radians(rot)), math.sin(math.radians(rot))
    pts = [(cx + ((x - 0.5) * c - (y - 0.48) * s_) * k, cy + ((x - 0.5) * s_ + (y - 0.48) * c) * k) for x, y in base]
    shape(ic, pts, (245, 235, 205), 0.03 * max(0.7, k))


def impact(ic, cx, cy, r0, r1, step=35, color=(230, 90, 40)):
    for a in range(0, 360, step):
        if 160 < a % 180 or a % 180 < 20:
            continue  # 骨头两头留空
        r = math.radians(a)
        ic.line([(cx + r0 * math.cos(r), cy + r0 * 0.8 * math.sin(r)), (cx + r1 * math.cos(r), cy + r1 * 0.8 * math.sin(r))],
                color=color, width=0.022, passes=1)


def bonk1():  # 邦（银）：一根小骨头
    ic = Icon(207, "silver")
    bone(ic, 0.5, 0.5, 0.8)
    impact(ic, 0.5, 0.5, 0.24, 0.32, 60)
    return ic


def bonk():  # 邦！（金）：骨头 + 冲击线（沙漠死神的招牌）
    ic = Icon(206, "gold")
    bone(ic, 0.5, 0.5)
    impact(ic, 0.5, 0.5, 0.27, 0.38)
    return ic


def bonk3():  # 邦邦邦！（棱彩）：三根骨头 + 一圈冲击线
    ic = Icon(208, "prism")
    impact(ic, 0.5, 0.5, 0.33, 0.43, 25, (200, 80, 220))
    bone(ic, 0.5, 0.33, 0.62, -12)
    bone(ic, 0.5, 0.5, 0.62, 8)
    bone(ic, 0.5, 0.67, 0.62, -6)
    return ic


# ---------- 棱彩 ----------
def infinite():  # 无限循环往复：∞
    ic = Icon(301, "prism")
    inf = [(0.5 + 0.32 * math.cos(t) / (1 + math.sin(t) ** 2), 0.5 + 0.32 * math.sin(t) * math.cos(t) / (1 + math.sin(t) ** 2))
           for t in (i / 64 * 6.283 for i in range(64))]
    ic.line(inf, color=BLUE, width=0.07, closed=True, passes=1, amp=0.008)
    ic.line(inf, width=0.025, closed=True, passes=1, amp=0.012)
    ic.sparkle(0.18, 0.25)
    ic.sparkle(0.82, 0.78)
    return ic


def stats3():  # 属性！属性！属性！：金铁砧 + 三颗星
    ic = Icon(302, "prism")
    anvil(ic, GOLD, dy=0.06)
    for x, y in ((0.26, 0.22), (0.5, 0.15), (0.74, 0.22)):
        ic.sparkle(x, y, 0.05, (240, 120, 40))
    return ic


def goliath():  # 巨人：大脚印
    ic = Icon(303, "prism")
    draw_foot(ic, 0.5, 0.56, 0.95, (200, 150, 110))
    ic.sparkle(0.2, 0.25)
    ic.sparkle(0.8, 0.8)
    return ic


def loop():  # 左脚踩右脚：两个脚印踩在一起 + 转圈箭头
    ic = Icon(304, "prism")
    draw_foot(ic, 0.4, 0.58, 0.55, (230, 170, 130), flip=True, rot=-12)
    draw_foot(ic, 0.6, 0.48, 0.55, (130, 170, 235), rot=12)
    # 首尾相接的两段弧：一圈一圈地踩
    for a0, a1 in ((195, 330), (15, 150)):
        a = arc(0.5, 0.52, 0.4, a0, a1, 16)
        ic.line(a, color=(150, 90, 210), width=0.035, passes=1)
        t, u = a[-1], a[-3]
        dx, dy = t[0] - u[0], t[1] - u[1]
        n = math.hypot(dx, dy)
        dx, dy = dx / n, dy / n
        tri = [(t[0] + dx * 0.07, t[1] + dy * 0.07), (t[0] - dy * 0.06, t[1] + dx * 0.06), (t[0] + dy * 0.06, t[1] - dx * 0.06)]
        shape(ic, tri, (150, 90, 210), 0.015)
    return ic


ICONS = {f.__name__: f for f in (blunt, deft, goredrink, stats, goldrush, mind,
                                  recursion, critical, colossus, axiom, manamune, bonk1, bonk, bonk3,
                                  infinite, stats3, goliath, loop)}

if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for name, f in ICONS.items():
        f().save(os.path.join(OUT, name + ".png"))
    cols, cell = 6, 200
    rows = (len(ICONS) + cols - 1) // cols
    sheet = Image.new("RGBA", (cols * cell, rows * (cell + 70)), (40, 40, 48, 255))
    for i, name in enumerate(ICONS):
        im = Image.open(os.path.join(OUT, name + ".png"))
        x, y = i % cols * cell, i // cols * (cell + 70)
        sheet.alpha_composite(im.resize((180, 180), Image.LANCZOS), (x + 10, y + 5))
        sheet.alpha_composite(im.resize((60, 60), Image.LANCZOS), (x + 70, y + 192))
    sheet.save(os.path.join(HERE, "_sheet.png"))
    print(f"{len(ICONS)} 张 → {os.path.normpath(OUT)}")
