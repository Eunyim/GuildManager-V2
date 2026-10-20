# 건물 안 배경 (접수처·신전·치유소·건축 공방) — 한 점 원근 3D 방을 픽셀마다 광선으로 칠하는 도트 그림
#   python Tools/interior_art_gen.py [reception temple clinic workshop]
# 480x270 → 화면에서 정확히 4배 (1920x1080). 출력: Assets/Art/Town/{reception_bg, interior_*}.png
#
# 방 좌표: x = 좌우(가운데 0), y = 높이(바닥 0), z = 깊이(카메라 0 → 안쪽 벽 D)
# 벽·바닥·천장은 광선이 처음 닿는 면을 찾아 그 면의 무늬(판자·벽돌·목골조)로 칠하고,
# 빛(횃불·창·화덕)은 거리에 따라 퍼지게, 구석은 어둡게(그늘) 한 뒤 색을 단계로 끊어 도트 느낌을 냄.
# 가구는 3D 상자(면 셋)로, 벽에 붙은 장식은 그 벽 좌표에 직접 그림
import math
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'Assets', 'Art', 'Town')
W, H = 480, 270
BAYER = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0


def hexc(h):
    h = h.lstrip('#')
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], np.float32)


def hash2(x, y, s=0):
    n = (np.asarray(x, np.int64) * 374761393 + np.asarray(y, np.int64) * 668265263 + s * 2147483647) & 0xffffffff
    n = ((n ^ (n >> 13)) * 1274126177) & 0xffffffff
    return ((n ^ (n >> 16)) & 0xffff) / 65535.0


class Room:
    def __init__(self, Wd=4.0, Hc=5.0, D=9.0, eye=1.6, f=262.0, cy=116.0):
        self.Wd, self.Hc, self.D, self.eye, self.f, self.cx, self.cy = Wd, Hc, D, eye, f, W / 2, cy
        ys, xs = np.mgrid[0:H, 0:W]
        self.XS, self.YS = xs, ys
        dx = (xs + 0.5 - self.cx) / f
        dy = -(ys + 0.5 - cy) / f
        big = 1e9
        t_floor = np.where(dy < 0, -eye / np.minimum(dy, -1e-9), big)
        t_ceil = np.where(dy > 0, (Hc - eye) / np.maximum(dy, 1e-9), big)
        t_left = np.where(dx < 0, -Wd / np.minimum(dx, -1e-9), big)
        t_right = np.where(dx > 0, Wd / np.maximum(dx, 1e-9), big)
        t_back = np.full(dx.shape, D)
        T = np.stack([t_floor, t_ceil, t_left, t_right, t_back])
        self.surf = np.argmin(T, axis=0)            # 0 바닥 1 천장 2 왼벽 3 오른벽 4 안벽
        t = np.min(T, axis=0)
        self.t = t
        self.PX, self.PY, self.PZ = dx * t, eye + dy * t, t
        self.rgb = np.zeros((H, W, 3), np.float32)
        self.light = np.zeros((H, W, 3), np.float32)
        self.add = np.zeros((H, W, 3), np.float32)   # 더하는 빛 (창 빛줄기·불빛 번짐)

    # ─── 투영 ───
    def proj(self, x, y, z):
        return (self.cx + self.f * x / z, self.cy - self.f * (y - self.eye) / z)

    def mask(self, s):
        return self.surf == s

    # ─── 조명 ───
    def ambient(self, color, k=1.0):
        self.light += hexc(color) / 255.0 * k

    def point(self, pos, color, power, radius):
        x, y, z = pos
        d2 = (self.PX - x) ** 2 + (self.PY - y) ** 2 + (self.PZ - z) ** 2
        fall = power / (1.0 + d2 / (radius * radius))
        self.light += fall[..., None] * (hexc(color) / 255.0)

    def corner_shadow(self, k=0.5, width=0.9):
        """벽과 벽, 벽과 바닥이 만나는 구석을 어둡게"""
        Wd, D, Hc = self.Wd, self.D, self.Hc
        dist = np.full((H, W), 99.0)
        s = self.surf
        dist = np.where(s == 0, np.minimum(np.minimum(Wd - np.abs(self.PX), D - self.PZ), 99), dist)
        dist = np.where((s == 2) | (s == 3), np.minimum(np.minimum(self.PY, D - self.PZ), Hc - self.PY), dist)
        dist = np.where(s == 4, np.minimum(np.minimum(self.PY, Wd - np.abs(self.PX)), Hc - self.PY), dist)
        dist = np.where(s == 1, np.minimum(Wd - np.abs(self.PX), D - self.PZ), dist)
        ao = 1.0 - k * np.clip(1.0 - dist / width, 0, 1) ** 2
        self.light *= ao[..., None]

    # ─── 마무리: 빛 적용 + 색 단계 끊기 ───
    def finish(self, steps=20, dither=0.55):
        L = 1.3 * (1.0 - np.exp(-self.light * 1.05))     # 부드러운 톤 매핑 (밝은 곳이 하얗게 날아가지 않게)
        c = self.rgb * L + self.add
        c = np.clip(c, 0, 255) / 255.0
        q = np.floor(c * steps + (BAYER[self.YS % 4, self.XS % 4][..., None] - 0.5) * dither + 0.5) / steps
        return np.clip(q * 255, 0, 255).astype(np.uint8)

    # ─── 그리기 도우미 (화면 좌표) ───
    def poly(self, pts, color, target='rgb'):
        from PIL import ImageDraw
        im = Image.new('L', (W, H), 0)
        ImageDraw.Draw(im).polygon([tuple(p) for p in pts], fill=255)
        m = np.array(im) > 0
        arr = getattr(self, target)
        arr[m] = color if np.ndim(color) == 1 else color[m]
        return m

    def box(self, x0, x1, y0, y1, z0, z1, top, front, side, light_mul=None):
        """3D 상자 (보이는 면: 윗면·앞면·안쪽 옆면). 면마다 색. 그 면 픽셀을 바닥보다 앞에 칠함"""
        P = self.proj
        faces = []
        # 윗면 (y1) — 눈보다 낮을 때만 보임
        if y1 < self.eye:
            faces.append(([P(x0, y1, z0), P(x1, y1, z0), P(x1, y1, z1), P(x0, y1, z1)], top))
        else:
            faces.append(([P(x0, y0, z0), P(x1, y0, z0), P(x1, y0, z1), P(x0, y0, z1)], top * 0.6))
        # 옆면: 가운데 쪽 면
        if x1 < 0:
            faces.append(([P(x1, y0, z0), P(x1, y1, z0), P(x1, y1, z1), P(x1, y0, z1)], side))
        elif x0 > 0:
            faces.append(([P(x0, y0, z0), P(x0, y1, z0), P(x0, y1, z1), P(x0, y0, z1)], side))
        faces.append(([P(x0, y0, z0), P(x1, y0, z0), P(x1, y1, z0), P(x0, y1, z0)], front))
        masks = []
        for pts, col in faces:
            masks.append(self.poly(pts, col))
        return masks

    def disc(self, cx, cy, r, color, target='rgb', soft=False):
        d = np.sqrt((self.XS + 0.5 - cx) ** 2 + (self.YS + 0.5 - cy) ** 2)
        arr = getattr(self, target)
        if soft:
            k = np.clip(1 - d / r, 0, 1) ** 2
            arr += k[..., None] * hexc(color) if isinstance(color, str) else k[..., None] * color
        else:
            m = d <= r
            arr[m] = hexc(color) if isinstance(color, str) else color
            return m


# ════════════════════════════════════════════════════════════════
# 무늬 (면 좌표 → 색)
# ════════════════════════════════════════════════════════════════
def planks(u, v, base, w=0.32, length=2.4, seed=1):
    """나무 판자: u 방향으로 판자 폭, v 방향으로 길이 (이음매 엇갈림)"""
    k = np.floor(u / w)
    off = hash2(k, 3, seed) * length
    seg = np.floor((v + off) / length)
    tone = (hash2(k, seg, seed + 1) - 0.5) * 0.22
    grain = (hash2(np.floor(u * 40), np.floor(v * 6), seed + 2) - 0.5) * 0.08
    fu = u / w - k
    fv = (v + off) / length - seg
    c = base[None, None] * (1.0 + tone + grain)[..., None]
    seam = (fu < 0.06) | (fv < 0.012)
    c = np.where(seam[..., None], base * 0.55, c)
    edge = (fu > 0.92)
    c = np.where(edge[..., None], base * 1.12, c)
    return c


def bricks(u, v, base, mortar, bw=0.55, bh=0.24, seed=2, rough=0.16):
    row = np.floor(v / bh)
    uu = u + (row % 2) * bw * 0.5
    col = np.floor(uu / bw)
    tone = (hash2(col, row, seed) - 0.5) * rough
    c = base[None, None] * (1.0 + tone)[..., None]
    fu, fv = uu / bw - col, v / bh - row
    m = (fu < 0.07) | (fv < 0.12)
    c = np.where(m[..., None], mortar, c)
    c = np.where(((fv > 0.85) & ~m)[..., None], c * 0.85, c)
    return c


def plaster(u, v, base, seed=3):
    n = hash2(np.floor(u * 9), np.floor(v * 9), seed) * 0.06 + hash2(np.floor(u * 3), np.floor(v * 3), seed + 1) * 0.06
    return base[None, None] * (0.95 + n)[..., None]


# ════════════════════════════════════════════════════════════════
# 접수처: 로비와 같은 반목조 회벽 + 돌 벽판 + 나무 바닥, 안쪽 벽 가운데 열린 문
# ════════════════════════════════════════════════════════════════
def reception():
    R = Room(Wd=4.2, Hc=4.8, D=9.5, eye=1.55)
    s = R.surf
    wood_d, wood = hexc('#4a2e1c'), hexc('#6e4628')
    # 바닥
    m = s == 0
    R.rgb[m] = planks(R.PX, R.PZ, hexc('#8a5a32'))[m]
    # 천장: 어두운 판자 + 들보
    m = s == 1
    c = planks(R.PX, R.PZ, hexc('#3a2618'), w=0.5)
    beam = (R.PZ % 1.6) < 0.32
    c = np.where(beam[..., None], wood_d * 0.8, c)
    R.rgb[m] = c[m]
    # 벽 공통: 아래 돌 벽판(0~1.0), 위 회벽 + 목골조
    for wall in (2, 3, 4):
        m = s == wall
        u = R.PZ if wall in (2, 3) else R.PX + 10
        v = R.PY
        c = plaster(u, v, hexc('#e6d6b8'))
        stone = bricks(u, v, hexc('#7a7680'), hexc('#4a4650'))
        c = np.where((v < 1.0)[..., None], stone, c)
        cap = (v >= 1.0) & (v < 1.14)
        c = np.where(cap[..., None], hexc('#5a3a24'), c)
        post = ((u % 1.8) < 0.2) & (v >= 1.0)
        rail = (np.abs(v - 2.7) < 0.09) | (v > R.Hc - 0.22)
        brace = (np.abs(((u % 1.8) - 0.2) - (v - 1.14) * 0.6) < 0.07) & (v > 1.14) & (v < 2.7) & ((u % 3.6) < 1.8)
        tim = (post | rail | brace) & (v >= 1.0)
        c = np.where(tim[..., None], wood * (0.9 + 0.1 * hash2(np.floor(u * 20), np.floor(v * 20), 7))[..., None], c)
        R.rgb[m] = c[m]
    # 안쪽 벽 가운데 문 (아치, 열려서 바깥 햇빛)
    door_w, door_h = 0.95, 2.55
    inside = (s == 4) & (np.abs(R.PX) < door_w) & ((R.PY < door_h - 0.5) | ((R.PX / door_w) ** 2 + ((R.PY - (door_h - 0.5)) / 0.62) ** 2 < 1))
    frame = (s == 4) & (np.abs(R.PX) < door_w + 0.16) & ((R.PY < door_h - 0.5) | ((R.PX / (door_w + 0.16)) ** 2 + ((R.PY - (door_h - 0.5)) / 0.78) ** 2 < 1)) & ~inside
    R.rgb[frame] = hexc('#8a8690') * (0.9 + 0.2 * hash2(np.floor(R.PX[frame] * 8), np.floor(R.PY[frame] * 8), 9))[..., None]
    sky = hexc('#9ad0f0') * (1 - (R.PY / door_h) * 0.25)[..., None] + hexc('#ffffff') * ((R.PY / door_h) * 0.25)[..., None]
    street = hexc('#c8b08a') * np.ones_like(sky)
    R.rgb[inside] = np.where((R.PY < 0.7)[..., None], street, sky)[inside]
    # 열린 문짝 (양쪽으로 젖힘)
    for sg in (-1, 1):
        x0 = sg * door_w
        pts = [R.proj(x0, 0, R.D), R.proj(x0, door_h - 0.2, R.D), R.proj(x0 + sg * 0.05, door_h - 0.25, R.D - 0.9), R.proj(x0 + sg * 0.05, 0, R.D - 0.9)]
        R.poly(pts, hexc('#6a4226'))
    # 깃발 (안쪽 벽 문 양옆)
    for sg in (-1, 1):
        x = sg * 2.3
        top, bot = 3.9, 2.0
        pts = [R.proj(x - 0.42, top, R.D - 0.01), R.proj(x + 0.42, top, R.D - 0.01), R.proj(x + 0.42, bot + 0.3, R.D - 0.01),
               R.proj(x, bot, R.D - 0.01), R.proj(x - 0.42, bot + 0.3, R.D - 0.01)]
        R.poly(pts, hexc('#a82a2a') if sg < 0 else hexc('#2a4a9a'))
        cxp, cyp = R.proj(x, 3.0, R.D)
        R.disc(cxp, cyp, 3.2, '#e0b040')
        R.disc(cxp, cyp, 1.6, '#a82a2a' if sg < 0 else '#2a4a9a')
    # 옆벽 창 (하늘빛)
    for sg in (-1, 1):
        for z in (3.2, 6.6):
            xw = sg * R.Wd
            pts = [R.proj(xw, 1.6, z - 0.55), R.proj(xw, 1.6, z + 0.55), R.proj(xw, 2.55, z + 0.55), R.proj(xw, 2.55, z - 0.55)]
            m = R.poly(pts, hexc('#8ac0e8'))
            R.add[m] += hexc('#203040')
            # 창살
            for t in (0.0,):
                a, b = R.proj(xw, 1.6, z), R.proj(xw, 2.55, z)
                R.poly([(a[0] - 0.6, a[1]), (a[0] + 0.6, a[1]), (b[0] + 0.6, b[1]), (b[0] - 0.6, b[1])], wood_d)
            a, b = R.proj(xw, 2.07, z - 0.55), R.proj(xw, 2.07, z + 0.55)
            R.poly([(a[0], a[1] - 0.6), (b[0], b[1] - 0.6), (b[0], b[1] + 0.6), (a[0], a[1] + 0.6)], wood_d)
    # 가구: 왼쪽 앞 통·상자, 오른쪽 의뢰 게시판 같은 선반
    # 벽 쪽 작은 통 두 개 (안쪽 구석)
    R.box(3.3, 3.95, 0, 0.75, 7.6, 8.3, hexc('#9a6a3a'), hexc('#7a5230'), hexc('#5a3a22'))
    R.box(-3.95, -3.3, 0, 0.75, 7.6, 8.3, hexc('#9a6a3a'), hexc('#7a5230'), hexc('#5a3a22'))
    # 샹들리에 (어두운 원 + 촛불)
    cxp, cyp = R.proj(0, 3.8, 5.0)
    R.poly([(cxp - 34, cyp), (cxp + 34, cyp), (cxp + 26, cyp + 4), (cxp - 26, cyp + 4)], hexc('#2a1c14'))
    top = R.proj(0, R.Hc, 5.0)
    R.poly([(cxp - 0.6, top[1]), (cxp + 0.6, top[1]), (cxp + 0.6, cyp), (cxp - 0.6, cyp)], hexc('#2a1c14'))
    for k in range(-3, 4):
        x = cxp + k * 10
        R.disc(x, cyp - 3, 1.6, '#ffe8a0')
        R.disc(x, cyp - 3, 9, hexc('#ff9a40') * 0.35, target='add', soft=True)
    # 조명
    R.ambient('#4a4258', 0.42)
    R.point((0, 1.4, R.D + 0.6), '#fff0d0', 1.1, 2.4)          # 문으로 들어오는 햇빛
    R.point((0, 3.7, 5.0), '#ffb060', 0.55, 2.4)                # 샹들리에
    for sg in (-1, 1):
        R.point((sg * (R.Wd - 0.3), 2.2, 4.8), '#ff9a40', 0.75, 1.3)  # 횃불
    R.corner_shadow(0.55)
    # 횃불 (옆벽)
    for sg in (-1, 1):
        x, y = R.proj(sg * (R.Wd - 0.05), 2.1, 4.8)
        R.poly([(x - 1, y), (x + 1, y), (x + 1, y + 8), (x - 1, y + 8)], hexc('#3a2a1c'))
        R.disc(x, y - 2, 2.4, '#ffb040')
        R.disc(x, y - 3, 1.2, '#fff0b0')
        R.disc(x, y - 2, 18, hexc('#ff8a30') * 0.35, target='add', soft=True)
    # 문 빛 번짐
    cxp, cyp = R.proj(0, 1.2, R.D)
    R.disc(cxp, cyp, 60, hexc('#fff0d0') * 0.18, target='add', soft=True)
    save(R.finish(), 'reception_bg')


# ════════════════════════════════════════════════════════════════
# 신전: 대리석 바닥, 기둥, 스테인드글라스, 제단, 붉은 융단, 빛줄기
# ════════════════════════════════════════════════════════════════
def temple():
    R = Room(Wd=4.4, Hc=6.0, D=10.0, eye=1.6, cy=120)
    s = R.surf
    # 바닥: 체크 대리석 + 붉은 융단
    m = s == 0
    chk = ((np.floor(R.PX / 0.8) + np.floor(R.PZ / 0.8)) % 2 == 0)
    c = np.where(chk[..., None], hexc('#d8d0c4'), hexc('#8a8496'))
    c = c * (0.94 + 0.08 * hash2(np.floor(R.PX * 6), np.floor(R.PZ * 6), 4))[..., None]
    rug = np.abs(R.PX) < 0.95
    rugc = np.where((np.abs(R.PX) > 0.82)[..., None], hexc('#e0b040'), hexc('#9a2228'))
    c = np.where(rug[..., None], rugc, c)
    R.rgb[m] = c[m]
    # 천장: 궁륭 늑재
    m = s == 1
    c = plaster(R.PX, R.PZ, hexc('#4a4458'))
    rib = (R.PZ % 2.0) < 0.18
    c = np.where(rib[..., None], hexc('#6a6478'), c)
    R.rgb[m] = c[m]
    # 벽: 큰 돌
    for wall in (2, 3, 4):
        m = s == wall
        u = R.PZ if wall in (2, 3) else R.PX + 10
        R.rgb[m] = bricks(u, R.PY, hexc('#9a94a6'), hexc('#5a5468'), bw=0.9, bh=0.45, rough=0.1)[m]
    # 안벽 장미창 (스테인드글라스)
    rx, ry = R.proj(0, 4.2, R.D)
    rr = R.f * 1.1 / R.D
    d = np.sqrt((R.XS + 0.5 - rx) ** 2 + (R.YS + 0.5 - ry) ** 2)
    ang = np.arctan2(R.YS + 0.5 - ry, R.XS + 0.5 - rx)
    glass = (d < rr) & (s == 4)
    pal = [hexc('#d83a3a'), hexc('#3a6ad8'), hexc('#e0b040'), hexc('#3aa860'), hexc('#8a4ad0')]
    seg = (np.floor((ang + math.pi) / (math.pi / 8)) + np.floor(d / (rr / 3))).astype(int) % 5
    gc = np.stack([pal[i] for i in range(5)])[seg]
    lead = ((((ang + math.pi) % (math.pi / 8)) < 0.06) | ((d % (rr / 3)) < 1.0)) & glass
    R.rgb[glass] = gc[glass]
    R.rgb[lead] = hexc('#1a1420')
    R.add[glass] += 40
    ring = (d >= rr) & (d < rr + 3) & (s == 4)
    R.rgb[ring] = hexc('#c8c0d0')
    # 옆벽 높은 아치창
    for sg in (-1, 1):
        for z in (2.6, 5.2, 7.8):
            xw = sg * R.Wd
            pts = [R.proj(xw, 2.0, z - 0.45), R.proj(xw, 2.0, z + 0.45), R.proj(xw, 4.2, z + 0.45), R.proj(xw, 4.6, z), R.proj(xw, 4.2, z - 0.45)]
            m = R.poly(pts, hexc('#6a9ad8'))
            R.rgb[m] = np.where(((R.YS[m] // 3) % 2 == 0)[..., None], hexc('#6a9ad8'), hexc('#d8c070'))
            R.add[m] += 30
    # 기둥 (옆벽 앞)
    for sg in (-1, 1):
        for z in (9.1, 6.5, 3.9, 1.4):      # 먼 것부터 (가까운 것이 위에 그려지게)
            x = sg * (R.Wd - 0.35)
            R.box(x - 0.28, x + 0.28, 0, R.Hc, z - 0.28, z + 0.28, hexc('#c8c2d4'), hexc('#aaa4b8'), hexc('#7a748a'))
    # 제단 + 계단
    R.box(-1.6, 1.6, 0, 0.25, 7.6, 8.8, hexc('#d8d0c4'), hexc('#a8a0b0'), hexc('#8a8496'))
    R.box(-1.0, 1.0, 0.25, 1.15, 8.0, 8.6, hexc('#f0e8d8'), hexc('#d8d0c0'), hexc('#b0a898'))
    gx, gy = R.proj(0, 1.15, 8.0)
    R.poly([(gx - 30, gy), (gx + 30, gy), (gx + 30, gy + 2), (gx - 30, gy + 2)], hexc('#e0b040'))
    for k in (-0.7, -0.35, 0.35, 0.7):
        x, y = R.proj(k, 1.15, 8.2)
        R.poly([(x - 0.8, y - 7), (x + 0.8, y - 7), (x + 0.8, y), (x - 0.8, y)], hexc('#f4f0e0'))
        R.disc(x, y - 9, 1.3, '#ffd060')
        R.disc(x, y - 9, 8, hexc('#ffb040') * 0.4, target='add', soft=True)
    # 성물 (금빛 원)
    x, y = R.proj(0, 2.2, 8.4)
    R.disc(x, y, 7, '#e0b040')
    R.disc(x, y, 4.5, '#fff0a0')
    R.disc(x, y, 28, hexc('#fff0b0') * 0.35, target='add', soft=True)
    # 긴 의자
    for z in (6.9, 5.9, 4.9):
        for sg in (-1, 1):
            x0, x1 = (1.25, 3.4) if sg > 0 else (-3.4, -1.25)
            R.box(x0, x1, 0, 0.42, z, z + 0.4, hexc('#8a5a32'), hexc('#6a4226'), hexc('#4a2e1c'))
            R.box(x0, x1, 0.42, 0.85, z + 0.32, z + 0.4, hexc('#7a4e2c'), hexc('#6a4226'), hexc('#4a2e1c'))
    # 조명 + 빛줄기
    R.ambient('#4a4860', 0.45)
    R.point((0, 4.0, R.D), '#ffe8c0', 0.8, 3.0)
    R.point((0, 1.8, 8.3), '#ffd080', 0.7, 1.8)
    for sg in (-1, 1):
        R.point((sg * (R.Wd - 0.5), 3.0, 5.2), '#a0c0ff', 0.4, 2.2)
    R.corner_shadow(0.5)
    for sg in (-1, 1):
        for z in (2.6, 5.2, 7.8):
            a = R.proj(sg * R.Wd, 3.6, z)
            b = R.proj(sg * (R.Wd - 2.8), 0.0, z + 0.4)
            pts = [(a[0], a[1] - 18), (a[0], a[1] + 14), (b[0] + 10 * sg, b[1]), (b[0] - 14 * sg, b[1])]
            from PIL import ImageDraw
            im = Image.new('L', (W, H), 0)
            ImageDraw.Draw(im).polygon(pts, fill=255)
            m = np.array(im) > 0
            R.add[m] += hexc('#fff0c0') * 0.045
    save(R.finish(), 'interior_temple')


# ════════════════════════════════════════════════════════════════
# 치유소: 따뜻한 나무 벽, 약초, 약병 선반, 침대, 창 햇빛
# ════════════════════════════════════════════════════════════════
def clinic():
    R = Room(Wd=4.2, Hc=4.4, D=8.5, eye=1.6)
    s = R.surf
    m = s == 0
    R.rgb[m] = planks(R.PX, R.PZ, hexc('#b08458'), w=0.36)[m]
    m = s == 1
    c = planks(R.PZ, R.PX, hexc('#7a5634'), w=0.45)
    c = np.where(((R.PZ % 1.5) < 0.25)[..., None], hexc('#5a3a22'), c)
    R.rgb[m] = c[m]
    for wall in (2, 3, 4):
        m = s == wall
        u = R.PZ if wall in (2, 3) else R.PX + 10
        c = planks(R.PY, u, hexc('#a07a52'), w=0.3, length=9.0, seed=5)     # 세로 판자
        c = np.where((R.PY < 0.9)[..., None], planks(R.PY, u, hexc('#7a5634'), w=0.3, length=9, seed=6), c)
        c = np.where((np.abs(R.PY - 0.9) < 0.06)[..., None], hexc('#5a3a22'), c)
        R.rgb[m] = c[m]
    # 안벽 큰 창 + 커튼
    for sg in (-1, 1):
        x0 = sg * 1.9
        pts = [R.proj(x0 - 0.75, 1.5, R.D), R.proj(x0 + 0.75, 1.5, R.D), R.proj(x0 + 0.75, 3.1, R.D), R.proj(x0 - 0.75, 3.1, R.D)]
        m = R.poly(pts, hexc('#a8d4f0'))
        R.rgb[m] = hexc('#a8d4f0') * (1 - (R.YS[m] - R.YS[m].min())[..., None] / 120.0) + hexc('#ffffff') * ((R.YS[m] - R.YS[m].min())[..., None] / 120.0)
        R.add[m] += 25
        a, b = R.proj(x0, 1.5, R.D), R.proj(x0, 3.1, R.D)
        R.poly([(a[0] - 0.7, a[1]), (a[0] + 0.7, a[1]), (b[0] + 0.7, b[1]), (b[0] - 0.7, b[1])], hexc('#5a3a22'))
        for cs in (-1, 1):
            cx0 = x0 + cs * 0.75
            pts = [R.proj(cx0, 3.25, R.D - 0.02), R.proj(cx0 - cs * 0.38, 3.25, R.D - 0.02), R.proj(cx0 - cs * 0.18, 1.4, R.D - 0.02), R.proj(cx0 + cs * 0.05, 1.3, R.D - 0.02)]
            R.poly(pts, hexc('#f0ece0'))
    # 안벽 가운데 약병 선반
    for k, y in enumerate((1.3, 1.95, 2.6)):
        a, b = R.proj(-0.85, y, R.D), R.proj(0.85, y, R.D)
        R.poly([(a[0], a[1]), (b[0], b[1]), (b[0], b[1] + 2), (a[0], a[1] + 2)], hexc('#5a3a22'))
        cols = ['#d84a4a', '#4aa860', '#4a7ad8', '#e0b040', '#a050d0', '#e07a30']
        x = a[0] + 2
        i = 0
        while x < b[0] - 3:
            h = 5 + (i * 7 % 3)
            col = hexc(cols[(i + k) % len(cols)])
            R.poly([(x, a[1] - h), (x + 3, a[1] - h), (x + 3, a[1]), (x, a[1])], col)
            R.poly([(x + 1, a[1] - h - 2), (x + 2, a[1] - h - 2), (x + 2, a[1] - h), (x + 1, a[1] - h)], hexc('#d8c8a8'))
            R.poly([(x, a[1] - h + 1), (x + 1, a[1] - h + 1), (x + 1, a[1] - 1), (x, a[1] - 1)], col * 1.35)
            x += 5
            i += 1
    # 천장에 매달린 약초 다발
    for x in np.linspace(-3.2, 3.2, 9):
        for z in (3.0, 5.5):
            tx, ty = R.proj(x, R.Hc, z)
            bx, by = R.proj(x, R.Hc - 0.55, z)
            R.poly([(tx - 0.4, ty), (tx + 0.4, ty), (bx + 0.4, by), (bx - 0.4, by)], hexc('#4a3a28'))
            R.disc(bx, by + 2, 3.2 * 5.5 / z, '#5a8a3a')
            R.disc(bx - 1, by + 1, 1.6 * 5.5 / z, '#7aae4a')
    # 침대 (양 옆벽을 따라)
    for sg in (-1, 1):
        for z in (6.4, 4.6):
            x0, x1 = (sg * R.Wd - 1.9 * sg, sg * R.Wd - 0.15 * sg)
            x0, x1 = min(x0, x1), max(x0, x1)
            R.box(x0, x1, 0, 0.45, z, z + 1.0, hexc('#7a5230'), hexc('#5a3a22'), hexc('#4a2e1c'))
            R.box(x0 + 0.05, x1 - 0.05, 0.45, 0.62, z + 0.03, z + 0.97, hexc('#e8eef4'), hexc('#c8d0dc'), hexc('#a8b0bc'))
            R.box(x0 + 0.1, x1 - 0.1, 0.62, 0.7, z + 0.35, z + 0.97, hexc('#7aa0d0'), hexc('#5a80b0'), hexc('#4a6a98'))
            px = x1 - 0.5 if sg < 0 else x0 + 0.5
            R.box(min(px, px + 0.0) - 0.35, max(px, px) + 0.35, 0.62, 0.78, z + 0.06, z + 0.32, hexc('#ffffff'), hexc('#e8e8f0'), hexc('#c8c8d8'))
    # 가운데 작은 탁자 + 대야
    R.box(-0.4, 0.4, 0, 0.65, 5.4, 5.9, hexc('#9a6a3a'), hexc('#7a5230'), hexc('#5a3a22'))
    x, y = R.proj(0, 0.7, 5.65)
    R.disc(x, y, 5, '#c8ccd8')
    R.disc(x, y, 3.4, '#8ac0e8')
    R.ambient('#5a4a44', 0.45)
    for sg in (-1, 1):
        R.point((sg * 1.9, 2.3, R.D + 0.4), '#fff4e0', 0.85, 2.4)
    R.point((0, 3.8, 4.0), '#ffd8a0', 0.35, 3.0)
    R.corner_shadow(0.5)
    # 창 빛줄기
    from PIL import ImageDraw
    for sg in (-1, 1):
        a = R.proj(sg * 1.9, 2.3, R.D)
        b = R.proj(sg * 1.2, 0.0, R.D - 3.2)
        im = Image.new('L', (W, H), 0)
        ImageDraw.Draw(im).polygon([(a[0] - 14, a[1] - 18), (a[0] + 14, a[1] - 18), (b[0] + 40, b[1]), (b[0] - 40, b[1])], fill=255)
        m = np.array(im) > 0
        R.add[m] += hexc('#fff4d0') * 0.045
    save(R.finish(), 'interior_clinic')


# ════════════════════════════════════════════════════════════════
# 건축 공방: 벽돌 벽, 화덕(강한 주황빛), 연장 걸이, 모루, 작업대, 목재
# ════════════════════════════════════════════════════════════════
def workshop():
    R = Room(Wd=4.4, Hc=4.6, D=8.8, eye=1.6)
    s = R.surf
    m = s == 0
    c = bricks(R.PX, R.PZ, hexc('#6a5a4e'), hexc('#3a302a'), bw=0.8, bh=0.8, rough=0.2)   # 돌바닥
    R.rgb[m] = c[m]
    m = s == 1
    c = planks(R.PX, R.PZ, hexc('#3a2a1e'), w=0.5)
    c = np.where(((R.PZ % 1.8) < 0.3)[..., None], hexc('#24180e'), c)
    R.rgb[m] = c[m]
    for wall in (2, 3, 4):
        m = s == wall
        u = R.PZ if wall in (2, 3) else R.PX + 10
        R.rgb[m] = bricks(u, R.PY, hexc('#9a4a34'), hexc('#4a2a20'), bw=0.5, bh=0.22, rough=0.22)[m]
    # 화덕 (안벽 가운데)
    hood = [R.proj(-1.3, 2.4, R.D - 0.02), R.proj(1.3, 2.4, R.D - 0.02), R.proj(0.55, 3.6, R.D - 0.02), R.proj(-0.55, 3.6, R.D - 0.02)]
    R.poly(hood, hexc('#5a5660'))
    R.poly([R.proj(-0.55, 3.6, R.D - 0.02), R.proj(0.55, 3.6, R.D - 0.02), R.proj(0.55, R.Hc, R.D - 0.02), R.proj(-0.55, R.Hc, R.D - 0.02)], hexc('#4a4650'))
    R.box(-1.4, 1.4, 0, 1.1, R.D - 1.0, R.D - 0.02, hexc('#7a7680'), hexc('#6a6670'), hexc('#4a4650'))
    mouth = [R.proj(-0.85, 0.2, R.D - 1.0), R.proj(0.85, 0.2, R.D - 1.0), R.proj(0.85, 0.85, R.D - 1.0), R.proj(-0.85, 0.85, R.D - 1.0)]
    m = R.poly(mouth, hexc('#ff8a2a'))
    yy = (R.YS[m] - R.YS[m].min()) / max(1, (R.YS[m].max() - R.YS[m].min()))
    R.rgb[m] = hexc('#ffe070') * yy[..., None] + hexc('#c83a10') * (1 - yy)[..., None]
    R.add[m] += 60
    # 연장 걸이 (왼벽)
    for k, z in enumerate(np.linspace(2.0, 6.5, 7)):
        tx, ty = R.proj(-R.Wd + 0.02, 2.9, z)
        bx, by = R.proj(-R.Wd + 0.02, 1.7, z)
        R.poly([(tx - 0.7, ty), (tx + 0.7, ty), (bx + 0.7, by), (bx - 0.7, by)], hexc('#6a4226'))
        hx, hy = R.proj(-R.Wd + 0.02, 2.75, z)
        if k % 3 == 0:
            R.poly([(hx - 3, hy - 2), (hx + 3, hy - 2), (hx + 3, hy + 2), (hx - 3, hy + 2)], hexc('#a8acb8'))
        elif k % 3 == 1:
            R.poly([(hx - 1, hy - 4), (hx + 4, hy - 1), (hx - 1, hy + 2)], hexc('#c8ccd8'))
        else:
            R.poly([(hx - 3, hy - 1), (hx + 1, hy - 1), (hx + 1, hy + 1), (hx - 3, hy + 1)], hexc('#8a8e9a'))
    # 설계도 (오른벽)
    pts = [R.proj(R.Wd - 0.02, 1.6, 3.4), R.proj(R.Wd - 0.02, 1.6, 5.6), R.proj(R.Wd - 0.02, 3.0, 5.6), R.proj(R.Wd - 0.02, 3.0, 3.4)]
    m = R.poly(pts, hexc('#3a5a9a'))
    grid = ((R.XS % 4 == 0) | (R.YS % 4 == 0)) & m
    R.rgb[grid] = hexc('#6a8ac8')
    # 모루
    R.box(-0.45, 0.15, 0, 0.5, 5.4, 5.85, hexc('#5a5660'), hexc('#4a4650'), hexc('#3a3640'))
    R.box(-0.8, 0.5, 0.5, 0.78, 5.35, 5.9, hexc('#7a7886'), hexc('#5a5864'), hexc('#4a4650'))
    # 작업대 (오른쪽)
    R.box(2.0, 4.0, 0, 0.85, 4.4, 5.3, hexc('#9a6a3a'), hexc('#6a4226'), hexc('#4a2e1c'))
    R.box(2.4, 3.2, 0.85, 1.0, 4.6, 5.0, hexc('#d8c8a0'), hexc('#b8a880'), hexc('#988860'))
    # 목재 더미 (왼쪽 앞)
    for k in range(4):
        R.box(-3.9, -2.4, k * 0.2, k * 0.2 + 0.2, 3.4 + k * 0.05, 4.1, hexc('#c8a070'), hexc('#a87c4a'), hexc('#8a6238'))
    R.ambient('#3a3036', 0.4)
    R.point((0, 0.6, R.D - 1.1), '#ff7a2a', 1.6, 2.4)
    R.point((0, 3.4, 4.2), '#ffb060', 0.3, 2.8)
    R.corner_shadow(0.55)
    x, y = R.proj(0, 0.55, R.D - 1.0)
    R.disc(x, y, 70, hexc('#ff7a2a') * 0.22, target='add', soft=True)
    # 불티
    rng = np.random.default_rng(3)
    for _ in range(26):
        sx = x + rng.normal(0, 18)
        sy = y - abs(rng.normal(20, 16))
        R.disc(sx, sy, 0.8, '#ffd060')
    save(R.finish(), 'interior_workshop')


def save(arr, name):
    Image.fromarray(arr, 'RGB').save(os.path.join(OUT, name + '.png'))
    print('saved', name)


ALL = {'reception': reception, 'temple': temple, 'clinic': clinic, 'workshop': workshop}

if __name__ == '__main__':
    for k in (sys.argv[1:] or ALL.keys()):
        ALL[k]()
