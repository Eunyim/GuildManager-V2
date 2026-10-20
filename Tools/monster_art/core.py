# 몬스터 도트 그림 공통 렌더러
# 도형(타원·캡슐·다각형)을 셀 셰이딩으로 칠하고, 부위가 겹치는 경계에 안쪽 선, 맨 바깥에 색이 섞인 외곽선을 그음.
# 좌표: 캐릭터 공간 (발 밑 = 원점, 위쪽 = -y). Xf가 캔버스 좌표로 옮기고 사망 때 몸 전체를 회전시킴.
import colorsys
import math

import numpy as np
from PIL import Image, ImageDraw

LIGHT = np.array([-0.45, -0.65, 0.62])
LIGHT = LIGHT / np.linalg.norm(LIGHT)

BAYER = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0


def hexc(h):
    h = h.lstrip('#')
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16))


def _hsv(c):
    return colorsys.rgb_to_hsv(c[0] / 255.0, c[1] / 255.0, c[2] / 255.0)


def _rgb(h, s, v):
    r, g, b = colorsys.hsv_to_rgb(h % 1.0, max(0.0, min(1.0, s)), max(0.0, min(1.0, v)))
    return (int(round(r * 255)), int(round(g * 255)), int(round(b * 255)))


def _hue_toward(h, target, amt):
    d = ((target - h + 0.5) % 1.0) - 0.5
    return h + d * amt


def ramp(base, shadow_hue=0.70, light_hue=0.14):
    """기본색 하나로 5단 색 띠를 만듦: [선, 그늘, 기본, 밝음, 반짝임]
    그늘은 푸른 쪽, 밝은 쪽은 노란 쪽으로 색상을 살짝 돌려 도트 애니 느낌을 냄"""
    if isinstance(base, str):
        base = hexc(base)
    h, s, v = _hsv(base)
    line = _rgb(_hue_toward(h, shadow_hue, 0.18), s * 1.05 + 0.08, v * 0.38)
    shadow = _rgb(_hue_toward(h, shadow_hue, 0.10), s * 1.08 + 0.04, v * 0.72)
    light = _rgb(_hue_toward(h, light_hue, 0.08), s * 0.85, v * 1.18 + 0.04)
    spec = _rgb(_hue_toward(h, light_hue, 0.14), s * 0.45, min(1.0, v * 1.35 + 0.15))
    return [line, shadow, base, light, spec]


def darker(c, f=0.6):
    return tuple(int(v * f) for v in c[:3])


def mixc(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


class Xf:
    """캐릭터 공간 → 캔버스. 발 밑 기준점 (ox, oy), 회전(도, 시계 방향 +)은 pivot을 중심으로"""

    def __init__(self, ox, oy, rot=0.0, pivot=(0.0, 0.0), flip=False, sx=1.0, sy=1.0):
        self.ox, self.oy = ox, oy
        self.rot = rot
        self.pivot = pivot
        self.flip = flip
        self.sx, self.sy = sx, sy
        r = math.radians(rot)
        self.c, self.s = math.cos(r), math.sin(r)

    def __call__(self, p):
        x, y = p[0], p[1]
        if self.flip:
            x = -x
        x *= self.sx
        y *= self.sy
        px, py = self.pivot
        dx, dy = x - px, y - py
        rx = px + dx * self.c - dy * self.s
        ry = py + dx * self.s + dy * self.c
        return (self.ox + rx, self.oy + ry)

    def ang(self, a):
        """캐릭터 공간의 각도(도) → 캔버스 각도"""
        if self.flip:
            a = 180.0 - a
        return a + self.rot

    def scale(self):
        return (abs(self.sx) + abs(self.sy)) * 0.5


class Canvas:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.rgb = np.zeros((h, w, 3), np.float32)
        self.a = np.zeros((h, w), bool)
        self.noline = np.zeros((h, w), bool)   # 바깥 외곽선을 긋지 않을 픽셀 (빛, 연기 등)
        ys, xs = np.mgrid[0:h, 0:w]
        self.X = xs + 0.5
        self.Y = ys + 0.5
        self.bayer = BAYER[ys % 4, xs % 4]

    # ─── 칠하기 ───────────────────────────────────────────
    def fill(self, mask, shade, rmp, line=True, dither=True, levels=(0.38, 0.74, 0.97), glow=False, alpha=None):
        """mask 영역을 shade(-1~1) 값에 따라 색 띠로 칠함. line: 이미 칠해진 부위와 맞닿은 테두리에 선"""
        if not mask.any():
            return
        t = shade * 0.5 + 0.5
        if dither:
            t = t + (self.bayer - 0.5) * 0.045
        idx = np.full(mask.shape, 2, np.int8)
        idx[t < levels[0]] = 1
        idx[t >= levels[1]] = 3
        if len(rmp) > 4 and levels[2] is not None:
            idx[t >= levels[2]] = 4
        if line:
            prev = self.a
            edge = np.zeros_like(mask)
            shadow_edge = np.zeros_like(mask)
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nb = np.roll(mask, (dy, dx), axis=(0, 1))
                nprev = np.roll(prev, (dy, dx), axis=(0, 1))
                e = mask & ~nb & nprev
                edge |= e
                if (dy, dx) in ((-1, 0), (0, -1)):   # 오른쪽·아래가 바깥인 픽셀 (그늘 쪽)
                    shadow_edge |= e
            # 가는 부위(지팡이 등)는 테두리만 남지 않도록 그늘 쪽에만 선
            if edge.sum() > mask.sum() * 0.55:
                edge = shadow_edge if shadow_edge.sum() < mask.sum() * 0.5 else np.zeros_like(mask)
            idx[edge] = 0
        cols = np.array(rmp[:5] + [rmp[-1]] * (5 - len(rmp)), np.float32)
        self.rgb[mask] = cols[idx[mask]]
        self.a[mask] = True
        if glow:
            self.noline[mask] = True
        else:
            self.noline[mask] = False

    def flat(self, mask, color, glow=False):
        self.rgb[mask] = np.array(color[:3], np.float32)
        self.a[mask] = True
        self.noline[mask] = glow

    def px(self, x, y, color, glow=False):
        x, y = int(math.floor(x)), int(math.floor(y))
        if 0 <= x < self.w and 0 <= y < self.h:
            self.rgb[y, x] = color[:3]
            self.a[y, x] = True
            self.noline[y, x] = glow

    def line(self, p0, p1, color, glow=False):
        x0, y0 = p0
        x1, y1 = p1
        n = int(max(abs(x1 - x0), abs(y1 - y0)) * 1.5) + 1
        for i in range(n + 1):
            t = i / n
            self.px(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, color, glow)

    # ─── 도형 ────────────────────────────────────────────
    def ellipse(self, c, rx, ry, ang=0.0):
        """회전 타원: mask와 법선 기반 음영(-1~1)"""
        r = math.radians(ang)
        cs, sn = math.cos(r), math.sin(r)
        dx, dy = self.X - c[0], self.Y - c[1]
        lx = (dx * cs + dy * sn) / max(rx, 0.3)
        ly = (-dx * sn + dy * cs) / max(ry, 0.3)
        d = lx * lx + ly * ly
        mask = d <= 1.0
        nz = np.sqrt(np.clip(1.0 - d, 0, 1))
        nx = lx * cs - ly * sn
        ny = lx * sn + ly * cs
        shade = nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2]
        return mask, shade

    def capsule(self, p0, p1, r0, r1=None):
        """두 점을 잇는 둥근 막대 (반지름이 r0→r1로 변함)"""
        if r1 is None:
            r1 = r0
        ax, ay = p0
        bx, by = p1
        vx, vy = bx - ax, by - ay
        L2 = vx * vx + vy * vy
        if L2 < 1e-6:
            return self.ellipse(p0, r0, r0)
        t = ((self.X - ax) * vx + (self.Y - ay) * vy) / L2
        t = np.clip(t, 0, 1)
        cx, cy = ax + vx * t, ay + vy * t
        dx, dy = self.X - cx, self.Y - cy
        r = r0 + (r1 - r0) * t
        d = np.sqrt(dx * dx + dy * dy)
        mask = d <= r
        nx, ny = dx / np.maximum(r, 0.3), dy / np.maximum(r, 0.3)
        nz = np.sqrt(np.clip(1 - nx * nx - ny * ny, 0, 1))
        shade = nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2]
        return mask, shade

    def poly(self, pts, shade=0.15, grad=None):
        """다각형. grad=(방향 벡터, 세기)면 그 방향으로 밝아짐"""
        im = Image.new('L', (self.w, self.h), 0)
        ImageDraw.Draw(im).polygon([(p[0], p[1]) for p in pts], fill=255)
        mask = np.array(im) > 0
        if grad is None:
            sh = np.full(mask.shape, shade, np.float32)
        else:
            (gx, gy), k = grad
            cx = sum(p[0] for p in pts) / len(pts)
            cy = sum(p[1] for p in pts) / len(pts)
            span = max(1.0, max(math.hypot(p[0] - cx, p[1] - cy) for p in pts))
            sh = shade + ((self.X - cx) * gx + (self.Y - cy) * gy) / span * k
        return mask, sh

    def ring(self, c, r_out, r_in, a0, a1):
        """호 모양 띠 (휘두르기 잔상, 활대 등). 각도는 도, 시계 방향"""
        dx, dy = self.X - c[0], self.Y - c[1]
        d = np.sqrt(dx * dx + dy * dy)
        ang = np.degrees(np.arctan2(dy, dx))
        a0n = a0 % 360
        span = (a1 - a0) % 360
        rel = (ang - a0n) % 360
        mask = (d <= r_out) & (d >= r_in) & (rel <= span)
        return mask, rel / max(span, 1e-3)

    # ─── 마무리 ──────────────────────────────────────────
    def outline(self, color=(22, 12, 26)):
        """투명 픽셀 중 그림과 맞닿은 곳에 외곽선. 옆 픽셀 색을 어둡게 섞어 딱딱하지 않게"""
        a = self.a & ~self.noline
        out = np.zeros_like(self.a)
        acc = np.zeros_like(self.rgb)
        cnt = np.zeros(self.a.shape, np.float32)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nb = np.roll(a, (dy, dx), axis=(0, 1))
            nrgb = np.roll(self.rgb, (dy, dx), axis=(0, 1))
            hit = nb & ~self.a
            out |= hit
            acc[hit] += nrgb[hit]
            cnt[hit] += 1
        avg = acc[out] / cnt[out][:, None]
        base = np.array(color, np.float32)
        self.rgb[out] = base * 0.72 + avg * 0.28 * 0.5
        self.a[out] = True

    def image(self):
        out = np.zeros((self.h, self.w, 4), np.uint8)
        out[..., :3] = np.clip(self.rgb, 0, 255).astype(np.uint8)
        out[..., 3] = np.where(self.a, 255, 0)
        return Image.fromarray(out, 'RGBA')


# ─── 캐릭터 공간에서 바로 그리는 도우미 ─────────────────────────
class Painter:
    """Xf를 거쳐 캔버스에 부위를 칠함"""

    def __init__(self, cv, xf):
        self.cv, self.xf = cv, xf

    def ell(self, c, rx, ry, rmp, ang=0.0, **kw):
        k = self.xf.scale()
        m, s = self.cv.ellipse(self.xf(c), rx * abs(self.xf.sx), ry * abs(self.xf.sy), self.xf.ang(ang))
        self.cv.fill(m, s, rmp, **kw)
        return m

    def cap(self, p0, p1, r0, rmp, r1=None, **kw):
        k = self.xf.scale()
        m, s = self.cv.capsule(self.xf(p0), self.xf(p1), r0 * k, (r1 if r1 is not None else r0) * k)
        self.cv.fill(m, s, rmp, **kw)
        return m

    def poly(self, pts, rmp, shade=0.15, grad=None, **kw):
        m, s = self.cv.poly([self.xf(p) for p in pts], shade, grad)
        self.cv.fill(m, s, rmp, **kw)
        return m

    def px(self, p, color, glow=False):
        q = self.xf(p)
        self.cv.px(q[0], q[1], color, glow)

    def line(self, p0, p1, color, glow=False):
        self.cv.line(self.xf(p0), self.xf(p1), color, glow)


def rot(v, deg):
    r = math.radians(deg)
    c, s = math.cos(r), math.sin(r)
    return (v[0] * c - v[1] * s, v[0] * s + v[1] * c)


def add(a, b, k=1.0):
    return (a[0] + b[0] * k, a[1] + b[1] * k)


def lerp(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)


def dirv(deg, length=1.0):
    r = math.radians(deg)
    return (math.cos(r) * length, math.sin(r) * length)


def ik(root, target, l1, l2, bend=1):
    """두 마디 IK: 관절 위치를 돌려줌. bend=+1/-1로 굽는 방향"""
    dx, dy = target[0] - root[0], target[1] - root[1]
    d = math.hypot(dx, dy)
    d = max(1e-4, min(d, l1 + l2 - 1e-3))
    a = math.atan2(dy, dx)
    cosb = (l1 * l1 + d * d - l2 * l2) / (2 * l1 * d)
    b = math.acos(max(-1.0, min(1.0, cosb)))
    j = a + b * bend
    return (root[0] + math.cos(j) * l1, root[1] + math.sin(j) * l1)


def reach(root, target, length):
    """목표가 팔 길이보다 멀면 팔 길이까지만"""
    dx, dy = target[0] - root[0], target[1] - root[1]
    d = math.hypot(dx, dy)
    if d <= length:
        return target
    return (root[0] + dx / d * length, root[1] + dy / d * length)
