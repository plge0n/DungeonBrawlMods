"""儿童画 / 蜡笔风格的图标：抖动的粗线条、涂出边的斜线涂色、纸张颗粒。
坐标都用 0..1，画在 2 倍大的画布上再缩小。"""
import math
import random
from PIL import Image, ImageDraw, ImageFilter, ImageChops

S = 512          # 画布（最后缩到 256）
OUT = 256
PAPER = (250, 243, 226, 255)
# 纸片按品质换底色：银 / 金 / 棱彩
TIER_PAPER = {
    "silver": (236, 238, 242, 255),
    "gold": (252, 234, 172, 255),
    "prism": (232, 214, 252, 255),
}


class Pen:
    def __init__(self, seed):
        self.rng = random.Random(seed)

    def wobble(self, pts, amp=0.012, closed=False, step=0.01):
        """沿折线重新取点，叠加平滑的随机抖动。"""
        if closed:
            pts = pts + [pts[0]]
        dense = []
        for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
            n = max(2, int(math.hypot(x1 - x0, y1 - y0) / step))
            for i in range(n):
                t = i / n
                dense.append((x0 + (x1 - x0) * t, y0 + (y1 - y0) * t))
        dense.append(pts[-1])
        ph = [self.rng.uniform(0, 6.28) for _ in range(4)]
        fr = [self.rng.uniform(2, 5), self.rng.uniform(6, 11)]
        out = []
        L = len(dense)
        for i, (x, y) in enumerate(dense):
            u = i / max(1, L - 1)
            dx = amp * (math.sin(u * fr[0] * 6.28 + ph[0]) * 0.7 + math.sin(u * fr[1] * 6.28 + ph[1]) * 0.3)
            dy = amp * (math.sin(u * fr[0] * 6.28 + ph[2]) * 0.7 + math.sin(u * fr[1] * 6.28 + ph[3]) * 0.3)
            out.append((x + dx, y + dy))
        return out


def px(p):
    return [(x * S, y * S) for x, y in p]


def circle(cx, cy, r, n=48, sx=1.0, sy=1.0):
    return [(cx + r * sx * math.cos(a), cy + r * sy * math.sin(a)) for a in (i / n * 6.283 for i in range(n))]


class Icon:
    def __init__(self, seed, tier=None):
        self.pen = Pen(seed)
        self.rng = self.pen.rng
        self.img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        self.paper(TIER_PAPER.get(tier, PAPER))

    def paper(self, color):
        # 纸片：边缘不齐的圆角方块
        pts = []
        m, r = 0.05, 0.12
        for cx, cy, a0 in ((1 - m - r, m + r, -90), (1 - m - r, 1 - m - r, 0), (m + r, 1 - m - r, 90), (m + r, m + r, 180)):
            for k in range(9):
                a = math.radians(a0 + k * 90 / 8)
                pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
        pts = self.pen.wobble(pts, amp=0.006, closed=True)
        d = ImageDraw.Draw(self.img)
        d.polygon(px(pts), fill=color)

    def fill(self, poly, color, density=0.55, angle=None, over=0.015):
        """斜线涂色，涂出一点边，不涂满。"""
        mask = Image.new("L", (S, S), 0)
        grown = [(x, y) for x, y in poly]
        ImageDraw.Draw(mask).polygon(px(grown), fill=255)
        mask = mask.filter(ImageFilter.MaxFilter(int(over * S) | 1))
        layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        d = ImageDraw.Draw(layer)
        ang = math.radians(angle if angle is not None else self.rng.uniform(25, 65))
        dx, dy = math.cos(ang), math.sin(ang)
        nx, ny = -dy, dx
        gap = 0.018 / density
        t = -1.5
        while t < 1.5:
            off = t + self.rng.uniform(-gap * 0.3, gap * 0.3)
            a = (0.5 + nx * off - dx * 1.2, 0.5 + ny * off - dy * 1.2)
            b = (0.5 + nx * off + dx * 1.2, 0.5 + ny * off + dy * 1.2)
            w = int(S * self.rng.uniform(0.011, 0.02))
            c = tuple(max(0, min(255, v + self.rng.randint(-14, 14))) for v in color[:3]) + (self.rng.randint(190, 245),)
            d.line(px(self.pen.wobble([a, b], amp=0.004, step=0.05)), fill=c, width=w)
            t += gap
        self.img.alpha_composite(Image.composite(layer, Image.new("RGBA", (S, S), (0, 0, 0, 0)), mask))

    def solid(self, poly, color):
        """底色：先薄薄铺一层，再用斜线加深，有蜡笔的不均匀感。"""
        base = tuple(color[:3]) + (150,)
        layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        ImageDraw.Draw(layer).polygon(px(self.pen.wobble(poly, amp=0.008, closed=True)), fill=base)
        self.img.alpha_composite(layer)
        self.fill(poly, color, density=0.8)

    def line(self, pts, color=(45, 40, 45), width=0.03, closed=False, passes=2, amp=0.01):
        d = ImageDraw.Draw(self.img)
        for k in range(passes):
            p = self.pen.wobble(pts, amp=amp, closed=closed)
            w = int(S * width * (1.0 if k == 0 else 0.6))
            c = color + (255 if k == 0 else 200,)
            pp = px(p)
            d.line(pp, fill=c, width=w, joint="curve")
            r = w / 2
            for x, y in (pp[0], pp[-1]):
                d.ellipse((x - r, y - r, x + r, y + r), fill=c)

    def sparkle(self, x, y, r=0.035, color=(240, 190, 40)):
        self.line([(x - r, y), (x + r, y)], color=color, width=0.02, passes=1, amp=0.004)
        self.line([(x, y - r), (x, y + r)], color=color, width=0.02, passes=1, amp=0.004)

    def save(self, path):
        # 蜡笔颗粒：随机擦掉一点颜色
        grain = Image.effect_noise((S, S), 60).point(lambda v: 255 if v > 70 else 150)
        a = self.img.getchannel("A")
        self.img.putalpha(ImageChops.multiply(a, grain))
        self.img.resize((OUT, OUT), Image.LANCZOS).save(path)
