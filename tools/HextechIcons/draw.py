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



# ---------- 第二批 ----------
def excited():  # 兴奋：闪电（银）
    ic = Icon(109, "silver")
    bolt = [(0.56, 0.12), (0.3, 0.52), (0.48, 0.52), (0.38, 0.88), (0.72, 0.42), (0.54, 0.42), (0.66, 0.12)]
    shape(ic, bolt, (250, 210, 60))
    ic.sparkle(0.22, 0.24, 0.035, (240, 120, 40))
    ic.sparkle(0.8, 0.72, 0.035, (240, 120, 40))
    return ic


def fruitify():  # 果实化：苹果（银）
    ic = Icon(110, "silver")
    apple = heart(0.5, 0.6, 0.3)[::-1]
    apple = [(x, 1.12 - y) for x, y in apple]
    shape(ic, apple, (225, 60, 60))
    ic.line([(0.5, 0.36), (0.53, 0.2)], color=(110, 70, 40), width=0.03, passes=1)
    leaf = [(0.54, 0.26), (0.66, 0.18), (0.74, 0.24), (0.62, 0.3)]
    shape(ic, leaf, (90, 180, 80), 0.02)
    ic.line([(0.4, 0.5), (0.38, 0.6)], color=(255, 250, 245), width=0.025, passes=1)
    return ic


def tank():  # 坦克引擎：小坦克（金）
    ic = Icon(211, "gold")
    body = [(0.16, 0.58), (0.84, 0.58), (0.78, 0.76), (0.22, 0.76)]
    shape(ic, body, (110, 150, 90))
    turret = [(0.32, 0.58), (0.36, 0.42), (0.62, 0.42), (0.66, 0.58)]
    shape(ic, turret, (130, 170, 100))
    ic.line([(0.6, 0.48), (0.86, 0.38)], color=(80, 100, 70), width=0.05, passes=1)
    ic.line([(0.6, 0.48), (0.86, 0.38)], width=0.016, passes=1)
    for x in (0.3, 0.44, 0.58, 0.72):
        shape(ic, circle(x, 0.68, 0.04, 14), (70, 70, 80), 0.015)
    shape(ic, heart(0.22, 0.3, 0.1), (225, 60, 70), 0.02)
    return ic


def porcupine():  # 豪猪：刺球（金）
    ic = Icon(212, "gold")
    shape(ic, star(0.5, 0.52, 0.4, 0.22, 18, 3), (95, 70, 55))
    shape(ic, circle(0.5, 0.52, 0.17, 24), (210, 170, 130), 0.02)
    shape(ic, circle(0.44, 0.48, 0.025, 10), INK, 0.01)
    shape(ic, circle(0.56, 0.48, 0.025, 10), INK, 0.01)
    ic.line([(0.46, 0.58), (0.5, 0.6), (0.54, 0.58)], width=0.015, passes=1)
    return ic


def archmage():  # 大法师：巫师帽 + 星星（金）
    ic = Icon(213, "gold")
    hat = [(0.5, 0.12), (0.64, 0.42), (0.72, 0.66), (0.28, 0.66), (0.36, 0.42)]
    shape(ic, hat, (110, 80, 190))
    brim = [(0.14, 0.66), (0.86, 0.66), (0.8, 0.76), (0.2, 0.76)]
    shape(ic, brim, (90, 60, 170))
    shape(ic, star(0.5, 0.48, 0.07, 0.03, 5, -90), (250, 220, 70), 0.015)
    ic.sparkle(0.2, 0.26)
    ic.sparkle(0.8, 0.3)
    return ic


def dualwield():  # 双持：交叉的两把剑（棱彩）
    ic = Icon(305, "prism")
    for flip in (False, True):
        def m(x, y):
            return (1 - x, y) if flip else (x, y)
        blade = [m(0.72, 0.14), m(0.8, 0.2), m(0.42, 0.62), m(0.36, 0.56)]
        shape(ic, blade, (200, 210, 225) if flip else (150, 190, 240))
        ic.line([m(0.28, 0.5), m(0.48, 0.7)], color=(150, 100, 60), width=0.05, passes=1)
        ic.line([m(0.36, 0.62), m(0.22, 0.8)], color=(110, 70, 50), width=0.05, passes=1)
    return ic


def shell(ic, cx, cy, k=1.0):
    """一发霰弹（红色弹身 + 铜色底）。"""
    body = [(cx - 0.06 * k, cy - 0.16 * k), (cx + 0.06 * k, cy - 0.16 * k), (cx + 0.06 * k, cy + 0.1 * k), (cx - 0.06 * k, cy + 0.1 * k)]
    shape(ic, body, (220, 70, 50), 0.022)
    base = [(cx - 0.075 * k, cy + 0.1 * k), (cx + 0.075 * k, cy + 0.1 * k), (cx + 0.075 * k, cy + 0.17 * k), (cx - 0.075 * k, cy + 0.17 * k)]
    shape(ic, base, (235, 180, 70), 0.022)


def cannon1():  # 上膛：两发子弹（银）
    ic = Icon(214, "silver")
    shell(ic, 0.4, 0.5)
    shell(ic, 0.6, 0.5)
    return ic


def cannon():  # 大炮：三发子弹 + 火花（金）
    ic = Icon(215, "gold")
    for x in (0.3, 0.5, 0.7):
        shell(ic, x, 0.54, 0.95)
    impact(ic, 0.5, 0.5, 0.36, 0.44, 45)
    return ic


def cannon3():  # 大炮·极：一排子弹 + 爆炸（棱彩）
    ic = Icon(306, "prism")
    shape(ic, star(0.5, 0.36, 0.22, 0.11, 9, 5), (245, 140, 40), 0.02)
    for x in (0.22, 0.36, 0.5, 0.64, 0.78):
        shell(ic, x, 0.66, 0.75)
    return ic

# ---------- 队友卡（斗魂竞技场）：小人 = 圆脑袋 + 梯形身子 ----------
def buddy(ic, cx, cy, k=1.0, color=(90, 150, 220)):
    """一个小人（脑袋中心 cx, cy）。"""
    body = [(cx - 0.11 * k, cy + 0.32 * k), (cx - 0.07 * k, cy + 0.1 * k), (cx + 0.07 * k, cy + 0.1 * k), (cx + 0.11 * k, cy + 0.32 * k)]
    shape(ic, body, color, 0.02)
    shape(ic, circle(cx, cy, 0.08 * k, 20), (250, 215, 180), 0.02)


def sacrifice():  # 甘愿牺牲：小人捧出一颗心（银）
    ic = Icon(111, "silver")
    buddy(ic, 0.3, 0.36)
    buddy(ic, 0.72, 0.4, 0.85, (220, 120, 90))
    shape(ic, heart(0.51, 0.5, 0.11), (225, 60, 70), 0.02)
    ic.line([(0.38, 0.56), (0.45, 0.52)], width=0.02, passes=1)
    return ic


def vengeance():  # 复仇：红色怒火拳头（银）
    ic = Icon(112, "silver")
    flame = [(0.5, 0.1), (0.62, 0.3), (0.74, 0.22), (0.78, 0.5), (0.68, 0.84), (0.32, 0.84), (0.22, 0.5), (0.3, 0.26), (0.4, 0.34)]
    shape(ic, flame, (235, 90, 50))
    fist = [(0.36, 0.48), (0.64, 0.48), (0.66, 0.74), (0.34, 0.74)]
    shape(ic, fist, (250, 215, 180), 0.02)
    for x in (0.43, 0.5, 0.57):
        ic.line([(x, 0.48), (x, 0.58)], width=0.015, passes=1)
    return ic


def juicebox():  # 果汁盒：盒装果汁 + 吸管（银）
    ic = Icon(113, "silver")
    box = [(0.3, 0.32), (0.66, 0.32), (0.66, 0.84), (0.3, 0.84)]
    shape(ic, box, (250, 170, 60))
    top = [(0.3, 0.32), (0.4, 0.22), (0.76, 0.22), (0.66, 0.32)]
    shape(ic, top, (255, 205, 110), 0.02)
    side = [(0.66, 0.32), (0.76, 0.22), (0.76, 0.74), (0.66, 0.84)]
    shape(ic, side, (220, 140, 40), 0.02)
    ic.line([(0.56, 0.26), (0.62, 0.08), (0.72, 0.06)], color=(230, 70, 90), width=0.03, passes=1)
    shape(ic, heart(0.48, 0.58, 0.1), (225, 60, 70), 0.02)
    return ic


def maneuvers():  # 防御策略：大盾牌罩住两个小人（金）
    ic = Icon(214, "gold")
    sh = [(0.14, 0.16), (0.5, 0.1), (0.86, 0.16), (0.84, 0.52), (0.5, 0.9), (0.16, 0.52)]
    shape(ic, sh, (120, 180, 235))
    buddy(ic, 0.38, 0.42, 0.8)
    buddy(ic, 0.62, 0.42, 0.8, (220, 120, 90))
    return ic


def spiritlink():  # 灵魂连接：两颗心连着链子（金）
    ic = Icon(215, "gold")
    shape(ic, heart(0.26, 0.5, 0.15), (225, 60, 70), 0.02)
    shape(ic, heart(0.74, 0.5, 0.15), (150, 110, 230), 0.02)
    for x in (0.4, 0.5, 0.6):
        ic.line(circle(x, 0.48, 0.045, 14), closed=True, color=(170, 170, 185), width=0.02, passes=1)
    return ic


def banner():  # 号令之旗：旗杆 + 飘扬的旗子（金）
    ic = Icon(216, "gold")
    ic.line([(0.3, 0.1), (0.3, 0.9)], color=(140, 95, 55), width=0.04, passes=1)
    flag = [(0.32, 0.14), (0.82, 0.2), (0.7, 0.33), (0.84, 0.46), (0.32, 0.46)]
    shape(ic, flag, (225, 60, 60))
    shape(ic, star(0.54, 0.3, 0.07, 0.03, 5, -90), (250, 220, 70), 0.015)
    return ic


def sonicboom():  # 音爆：一圈圈声波 + 爆炸（金）
    ic = Icon(217, "gold")
    shape(ic, star(0.5, 0.52, 0.2, 0.1, 9, 10), (245, 140, 40), 0.02)
    for r in (0.28, 0.38):
        ic.line(arc(0.5, 0.52, r, 200, 340, 20), color=(90, 150, 220), width=0.025, passes=1)
        ic.line(arc(0.5, 0.52, r, 20, 160, 20), color=(90, 150, 220), width=0.025, passes=1)
    return ic


def divine():  # 神圣干预：光环 + 翅膀（棱彩）
    ic = Icon(307, "prism")
    for flip in (False, True):
        def m(x, y):
            return (1 - x, y) if flip else (x, y)
        wing = [m(0.46, 0.5), m(0.3, 0.3), m(0.12, 0.34), m(0.18, 0.46), m(0.1, 0.56), m(0.22, 0.64), m(0.18, 0.74), m(0.44, 0.66)]
        shape(ic, wing, (250, 250, 245), 0.02)
    ic.line(arc(0.5, 0.24, 0.14, 0, 360, 30, 0.35), closed=True, color=(245, 200, 60), width=0.035, passes=1)
    shape(ic, circle(0.5, 0.54, 0.09, 20), (250, 230, 120), 0.02)
    return ic


def clothesline():  # 晾衣绳：两个小人拉着一条闪电绳（棱彩）
    ic = Icon(308, "prism")
    buddy(ic, 0.18, 0.4, 0.8)
    buddy(ic, 0.82, 0.4, 0.8, (220, 120, 90))
    rope = [(0.26, 0.56), (0.38, 0.48), (0.46, 0.6), (0.54, 0.48), (0.62, 0.6), (0.74, 0.56)]
    ic.line(rope, color=(250, 210, 60), width=0.035, passes=1)
    ic.sparkle(0.5, 0.3)
    return ic


# ---------- 让经济 ----------
def coin(ic, cx, cy, r=0.09):
    shape(ic, circle(cx, cy, r, 22), GOLD, 0.02)
    ic.line([(cx, cy - r * 0.5), (cx, cy + r * 0.5)], color=(190, 130, 20), width=0.018, passes=1)


def patron():  # 慷慨供养：手递出金币（银）
    ic = Icon(114, "silver")
    hand = [(0.12, 0.62), (0.44, 0.58), (0.6, 0.64), (0.58, 0.72), (0.4, 0.72), (0.12, 0.76)]
    shape(ic, hand, (250, 215, 180), 0.02)
    coin(ic, 0.56, 0.44)
    coin(ic, 0.74, 0.32, 0.075)
    ic.line([(0.62, 0.52), (0.78, 0.5)], color=(240, 120, 40), width=0.02, passes=1)
    ic.sparkle(0.84, 0.2)
    return ic


def backline():  # 后排成长：小芽从土里长出来（银）
    ic = Icon(115, "silver")
    soil = [(0.16, 0.74), (0.84, 0.74), (0.8, 0.86), (0.2, 0.86)]
    shape(ic, soil, (140, 100, 70), 0.02)
    ic.line([(0.5, 0.74), (0.5, 0.36)], color=(80, 150, 70), width=0.035, passes=1)
    shape(ic, [(0.5, 0.48), (0.3, 0.36), (0.24, 0.44), (0.4, 0.54)], (110, 190, 90), 0.02)
    shape(ic, [(0.5, 0.4), (0.7, 0.26), (0.78, 0.34), (0.6, 0.46)], (110, 190, 90), 0.02)
    ic.line([(0.76, 0.66), (0.76, 0.5), (0.7, 0.56)], color=(240, 120, 40), width=0.02, passes=1)
    return ic


def oath():  # 守护之誓：盾牌 + 小星星（金）
    ic = Icon(218, "gold")
    sh = [(0.2, 0.18), (0.5, 0.12), (0.8, 0.18), (0.78, 0.5), (0.5, 0.86), (0.22, 0.5)]
    shape(ic, sh, (225, 80, 70))
    shape(ic, star(0.5, 0.44, 0.15, 0.07, 5, -90), (250, 220, 70), 0.02)
    ic.sparkle(0.86, 0.72)
    return ic


def allin():  # 举国之力：一堆金币 + 王冠（棱彩）
    ic = Icon(309, "prism")
    for x, y in ((0.3, 0.74), (0.5, 0.76), (0.7, 0.74), (0.4, 0.62), (0.6, 0.62), (0.5, 0.5)):
        coin(ic, x, y, 0.085)
    crown = [(0.3, 0.38), (0.34, 0.16), (0.42, 0.28), (0.5, 0.12), (0.58, 0.28), (0.66, 0.16), (0.7, 0.38)]
    shape(ic, crown, (245, 200, 60))
    ic.sparkle(0.16, 0.3)
    ic.sparkle(0.84, 0.3)
    return ic


# ---------- 第三批 ----------
def divebomb():  # 俯冲轰炸：往下冲的炸弹（银）
    ic = Icon(116, "silver")
    shape(ic, circle(0.48, 0.58, 0.22, 26), (60, 60, 70))
    ic.line([(0.62, 0.4), (0.74, 0.24), (0.8, 0.14)], color=(150, 110, 60), width=0.03, passes=1)
    ic.sparkle(0.82, 0.12, 0.05, (245, 140, 40))
    for x in (0.2, 0.3, 0.4):
        ic.line([(x, 0.12), (x + 0.06, 0.3)], color=(90, 150, 220), width=0.02, passes=1)
    ic.line([(0.4, 0.5), (0.36, 0.58)], color=(255, 255, 255), width=0.025, passes=1)
    return ic


def slowcook():  # 慢炖：冒热气的锅（银）
    ic = Icon(117, "silver")
    pot = [(0.18, 0.46), (0.82, 0.46), (0.74, 0.82), (0.26, 0.82)]
    shape(ic, pot, (110, 110, 125))
    ic.line([(0.12, 0.46), (0.88, 0.46)], width=0.04, passes=1)
    for x in (0.36, 0.5, 0.64):
        ic.line([(x, 0.4), (x - 0.04, 0.3), (x + 0.02, 0.2), (x - 0.02, 0.12)], color=(230, 120, 60), width=0.025, passes=1)
    return ic


def restless():  # 无休恢复：跑鞋 + 红心（银）
    ic = Icon(118, "silver")
    shoe = [(0.14, 0.7), (0.2, 0.5), (0.4, 0.52), (0.62, 0.62), (0.86, 0.66), (0.86, 0.78), (0.14, 0.78)]
    shape(ic, shoe, (90, 150, 220))
    ic.line([(0.14, 0.74), (0.86, 0.74)], color=(255, 255, 255), width=0.025, passes=1)
    shape(ic, heart(0.66, 0.3, 0.12), (225, 60, 70), 0.02)
    for y in (0.56, 0.64):
        ic.line([(0.04, y), (0.12, y)], color=(150, 155, 165), width=0.02, passes=1)
    return ic


def scopes():  # 瞄准镜：准星（银）
    ic = Icon(119, "silver")
    ic.line(circle(0.5, 0.5, 0.3, 30), closed=True, color=(215, 50, 50), width=0.04, passes=1)
    ic.line(circle(0.5, 0.5, 0.12, 20), closed=True, color=(215, 50, 50), width=0.03, passes=1)
    for a, b in (((0.5, 0.1), (0.5, 0.32)), ((0.5, 0.68), (0.5, 0.9)), ((0.1, 0.5), (0.32, 0.5)), ((0.68, 0.5), (0.9, 0.5))):
        ic.line([a, b], width=0.03, passes=1)
    return ic


def agi2str():  # 以巧补拙：羽毛 → 哑铃（银）
    ic = Icon(120, "silver")
    feather = [(0.14, 0.62), (0.3, 0.2), (0.36, 0.24), (0.22, 0.64)]
    shape(ic, feather, (120, 200, 170), 0.02)
    ic.line([(0.4, 0.46), (0.56, 0.46), (0.5, 0.4)], color=(240, 120, 40), width=0.025, passes=1)
    ic.line([(0.56, 0.46), (0.5, 0.52)], color=(240, 120, 40), width=0.025, passes=1)
    shape(ic, [(0.62, 0.34), (0.7, 0.34), (0.7, 0.62), (0.62, 0.62)], (70, 70, 80), 0.02)
    shape(ic, [(0.8, 0.34), (0.88, 0.34), (0.88, 0.62), (0.8, 0.62)], (70, 70, 80), 0.02)
    ic.line([(0.7, 0.48), (0.8, 0.48)], color=(160, 160, 170), width=0.03, passes=1)
    return ic


def str2agi():  # 以力带巧：哑铃 → 羽毛（银）
    ic = Icon(121, "silver")
    shape(ic, [(0.12, 0.34), (0.2, 0.34), (0.2, 0.62), (0.12, 0.62)], (70, 70, 80), 0.02)
    shape(ic, [(0.3, 0.34), (0.38, 0.34), (0.38, 0.62), (0.3, 0.62)], (70, 70, 80), 0.02)
    ic.line([(0.2, 0.48), (0.3, 0.48)], color=(160, 160, 170), width=0.03, passes=1)
    ic.line([(0.44, 0.46), (0.6, 0.46), (0.54, 0.4)], color=(240, 120, 40), width=0.025, passes=1)
    ic.line([(0.6, 0.46), (0.54, 0.52)], color=(240, 120, 40), width=0.025, passes=1)
    feather = [(0.66, 0.66), (0.8, 0.2), (0.88, 0.24), (0.74, 0.68)]
    shape(ic, feather, (120, 200, 170), 0.02)
    return ic


def egg():  # 棱彩蛋：带花纹的蛋（银）
    ic = Icon(122, "silver")
    oval = arc(0.5, 0.54, 0.26, 0, 360, 40, 1.3)
    shape(ic, oval, (235, 220, 250))
    ic.line([(0.26, 0.5), (0.36, 0.44), (0.46, 0.52), (0.56, 0.44), (0.66, 0.52), (0.74, 0.46)], color=(170, 110, 230), width=0.025, passes=1)
    ic.sparkle(0.42, 0.66, 0.03, (90, 180, 230))
    ic.sparkle(0.6, 0.7, 0.03, (240, 170, 60))
    return ic


def urf():  # 乌尔夫的勇士：锅铲（银）
    ic = Icon(123, "silver")
    ic.line([(0.24, 0.84), (0.58, 0.44)], color=(150, 100, 60), width=0.05, passes=1)
    blade = [(0.54, 0.4), (0.7, 0.16), (0.88, 0.3), (0.66, 0.52)]
    shape(ic, blade, (245, 200, 60))
    ic.sparkle(0.2, 0.26)
    return ic


def transmute():  # 嬗变：黄金：银卡变金卡（银）
    ic = Icon(124, "silver")
    shape(ic, [(0.12, 0.3), (0.38, 0.3), (0.38, 0.7), (0.12, 0.7)], (200, 205, 215), 0.025)
    ic.line([(0.42, 0.5), (0.58, 0.5), (0.52, 0.44)], color=(240, 120, 40), width=0.03, passes=1)
    ic.line([(0.58, 0.5), (0.52, 0.56)], color=(240, 120, 40), width=0.03, passes=1)
    shape(ic, [(0.62, 0.26), (0.9, 0.26), (0.9, 0.74), (0.62, 0.74)], GOLD, 0.025)
    ic.sparkle(0.76, 0.5)
    return ic


def mysticpunch():  # 神秘拳：拳头 + 漩涡（金）
    ic = Icon(219, "gold")
    fist = [(0.22, 0.4), (0.62, 0.4), (0.68, 0.5), (0.62, 0.72), (0.22, 0.72)]
    shape(ic, fist, (250, 215, 180))
    for y in (0.48, 0.56, 0.64):
        ic.line([(0.5, y), (0.64, y)], width=0.015, passes=1)
    ic.line(arc(0.78, 0.3, 0.12, 0, 300, 20), color=(150, 90, 220), width=0.03, passes=1)
    ic.line(arc(0.78, 0.3, 0.05, 0, 300, 14), color=(150, 90, 220), width=0.025, passes=1)
    return ic


def autocast():  # 自动施放：循环箭头 + 蓝滴（金）
    ic = Icon(220, "gold")
    ic.line(arc(0.5, 0.5, 0.3, -60, 230, 30), color=(240, 140, 40), width=0.045, passes=1)
    shape(ic, [(0.66, 0.2), (0.78, 0.26), (0.66, 0.34)], (240, 140, 40), 0.02)
    shape(ic, drop(0.5, 0.56, 0.12), (70, 120, 230), 0.02)
    return ic


def glasscannon():  # 玻璃大炮：透明的炮（棱彩）
    ic = Icon(310, "prism")
    barrel = [(0.2, 0.56), (0.62, 0.3), (0.7, 0.42), (0.28, 0.68)]
    shape(ic, barrel, (190, 225, 245))
    shape(ic, circle(0.3, 0.72, 0.1, 20), (150, 110, 70), 0.02)
    ic.line([(0.4, 0.5), (0.46, 0.46)], color=(255, 255, 255), width=0.025, passes=1)
    ic.sparkle(0.8, 0.26, 0.05, (245, 140, 40))
    return ic


def allornothing():  # 孤注一掷：骰子（棱彩）
    ic = Icon(311, "prism")
    shape(ic, [(0.22, 0.26), (0.74, 0.26), (0.74, 0.78), (0.22, 0.78)], (250, 250, 245))
    for x, y in ((0.34, 0.38), (0.62, 0.38), (0.48, 0.52), (0.34, 0.66), (0.62, 0.66)):
        shape(ic, circle(x, y, 0.045, 12), (215, 50, 50), 0.01)
    return ic


def heavy():  # 重装：铁头盔（棱彩）
    ic = Icon(312, "prism")
    helm = arc(0.5, 0.56, 0.3, 180, 360, 24) + [(0.8, 0.56), (0.8, 0.78), (0.2, 0.78), (0.2, 0.56)]
    shape(ic, helm, (150, 155, 170))
    shape(ic, [(0.3, 0.56), (0.7, 0.56), (0.7, 0.64), (0.3, 0.64)], (60, 60, 70), 0.02)
    ic.line([(0.5, 0.26), (0.5, 0.5)], color=(215, 50, 50), width=0.04, passes=1)
    return ic


ICONS = {f.__name__: f for f in (blunt, deft, goredrink, stats, goldrush, mind,
                                  recursion, critical, colossus, axiom, manamune, bonk1, bonk, bonk3,
                                  infinite, stats3, goliath, loop,
                                  excited, fruitify, tank, porcupine, archmage, dualwield, cannon1, cannon, cannon3,
                                  sacrifice, vengeance, juicebox, maneuvers, spiritlink, banner, sonicboom, divine, clothesline,
                                  patron, backline, oath, allin,
                                  divebomb, slowcook, restless, scopes, agi2str, str2agi, egg, urf, transmute,
                                  mysticpunch, autocast, glasscannon, allornothing, heavy)}

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
