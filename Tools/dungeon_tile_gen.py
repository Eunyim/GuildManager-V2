# 던전 지역별 바닥 타일 생성 (아이소메트릭 마름모 64x32, PPU 64 → 한 칸 = 1유닛)
#   python Tools/dungeon_tile_gen.py
# 출력: Assets/Art/Tiles/Dungeon/{theme}/floor_{n}.png  (+ preview_{theme}.png를 DUNGEON_PREVIEW 폴더에)
# Unity: Tools > 던전 지역 타일 적용 (타일·테마 에셋을 만들고 지역 이름으로 연결)
#
# 바탕 무늬는 칸 좌표(u, v)에 대해 주기적인 노이즈라 어느 칸끼리 붙어도 경계가 이어짐.
# 꽃·돌·뼈·균열 같은 장식은 칸 안쪽에만 그려서 무작위로 깔아도 잘린 자국이 없음.
# 앞쪽 번호(plain)는 수수한 바닥, 뒤쪽(detail)은 장식이 있는 바닥 — Unity에서 plain을 더 자주 깔도록 가중치를 줌
import math
import os
import random
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'Assets', 'Art', 'Tiles', 'Dungeon')
MASK_SRC = os.path.join(ROOT, 'Assets', 'Art', 'Tiles', 'LobbyTileset', 'IsoFloorTiles', 'iso_gray0.png')
W, H = 64, 32
PLAIN, DETAIL = 4, 4


def hexc(h):
    h = h.lstrip('#')
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], np.float32)


def load_mask():
    a = np.array(Image.open(MASK_SRC).convert('RGBA'))[..., 3] > 0
    assert a.shape == (H, W)
    return a


YS, XS = np.mgrid[0:H, 0:W]
PX, PY = XS + 0.5, YS + 0.5
U = (PY / 16.0 + (PX - 32.0) / 32.0) / 2.0     # 칸 좌표 (0~1)
V = (PY / 16.0 - (PX - 32.0) / 32.0) / 2.0
BAYER = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0
BY = BAYER[YS % 4, XS % 4]


def hash2(ix, iy, seed):
    n = (ix * 374761393 + iy * 668265263 + seed * 2147483647) & 0xffffffff
    n = ((n ^ (n >> 13)) * 1274126177) & 0xffffffff
    return ((n ^ (n >> 16)) & 0xffff) / 65535.0


def pnoise(n, seed):
    """칸 좌표에서 주기 n인 값 노이즈 (부드럽게 보간). 칸 경계에서 이어짐"""
    x, y = U * n, V * n
    x0, y0 = np.floor(x).astype(np.int64), np.floor(y).astype(np.int64)
    fx, fy = x - x0, y - y0
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    def h(ix, iy):
        return np.vectorize(lambda a, b: hash2(int(a) % n, int(b) % n, seed))(ix, iy)
    a, b, c, d = h(x0, y0), h(x0 + 1, y0), h(x0, y0 + 1), h(x0 + 1, y0 + 1)
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def band(val, cols, dither=0.06):
    """0~1 값을 색 띠로 (경계만 살짝 섞음)"""
    t = np.clip(val + (BY - 0.5) * dither, 0, 0.9999)
    idx = (t * len(cols)).astype(int)
    return np.array(cols)[idx]


class Tile:
    def __init__(self, base_rgb, mask):
        self.rgb = base_rgb.astype(np.float32)
        self.mask = mask

    def inside(self, x, y, margin=0.18):
        """칸 안쪽(가장자리에서 margin 이상 떨어진) 픽셀인가"""
        x, y = int(x), int(y)
        if not (0 <= x < W and 0 <= y < H) or not self.mask[y, x]:
            return False
        u, v = U[y, x], V[y, x]
        return margin < u < 1 - margin and margin < v < 1 - margin

    def px(self, x, y, c, margin=0.0):
        x, y = int(round(x)), int(round(y))
        if 0 <= x < W and 0 <= y < H and self.mask[y, x]:
            if margin and not self.inside(x, y, margin):
                return
            self.rgb[y, x] = c

    def blob(self, cx, cy, rx, ry, cols, margin=0.12):
        """빛이 왼쪽 위에서 오는 둥근 덩어리 (돌·흙더미)"""
        dx, dy = (PX - cx) / rx, (PY - cy) / ry
        d = dx * dx + dy * dy
        m = (d <= 1.0) & self.mask
        if margin:
            m &= (U > margin) & (U < 1 - margin) & (V > margin) & (V < 1 - margin)
        sh = -dx * 0.55 - dy * 0.8
        idx = np.where(sh > 0.35, 3, np.where(sh > -0.1, 2, 1))
        edge = m & (d > 0.62)
        idx = np.where(edge & (sh < 0.2), 0, idx)
        self.rgb[m] = np.array(cols)[idx[m]]
        return m

    def image(self):
        out = np.zeros((H, W, 4), np.uint8)
        out[..., :3] = np.clip(self.rgb, 0, 255).astype(np.uint8)
        out[..., 3] = np.where(self.mask, 255, 0)
        return Image.fromarray(out, 'RGBA')


def rnd_inside(t, rng, margin=0.22):
    for _ in range(200):
        x, y = rng.uniform(4, W - 4), rng.uniform(3, H - 3)
        if t.inside(x, y, margin):
            return x, y
    return W / 2, H / 2


# ════════════════════════════════════════════════════════════════
# 테마
# ════════════════════════════════════════════════════════════════
def grass_base(mask, seed, cols):
    n = pnoise(6, seed) * 0.6 + pnoise(12, seed + 1) * 0.4
    return band(n, cols)


def blades(t, rng, count, cols, margin=0.0):
    """풀잎: 짧은 세로 획 (위가 밝음)"""
    for _ in range(count):
        x, y = rng.uniform(0, W), rng.uniform(0, H)
        if not t.mask[int(y), int(x)]:
            continue
        h = rng.choice((2, 2, 3))
        lean = rng.choice((-1, 0, 0, 1))
        t.px(x, y + 1, cols[0], margin)
        for k in range(h):
            t.px(x + (lean if k == h - 1 else 0), y - k, cols[2] if k == h - 1 else cols[1], margin)


def prairie(i, mask, rng):
    cols = [hexc('#3e7a2e'), hexc('#4c8c34'), hexc('#5a9c3a'), hexc('#6aae44')]
    t = Tile(grass_base(mask, 11, cols), mask)
    blades(t, rng, 70, [hexc('#2e5e24'), hexc('#78c04c'), hexc('#a6dc66')])
    if i >= PLAIN:
        kind = (i - PLAIN) % 4
        if kind == 0:     # 꽃밭
            for _ in range(5):
                x, y = rnd_inside(t, rng)
                c = [hexc('#f4f0e0'), hexc('#f0d040'), hexc('#f08ab0'), hexc('#8ab0f0')][rng.randrange(4)]
                for dx, dy in ((0, -1), (-1, 0), (1, 0), (0, 1)):
                    t.px(x + dx, y + dy, c)
                t.px(x, y, hexc('#e0a020'))
        elif kind == 1:   # 흙길 조각
            m = t.blob(32 + rng.uniform(-6, 6), 16 + rng.uniform(-3, 3), 11, 5.5,
                       [hexc('#5a4228'), hexc('#7a5a36'), hexc('#8e6c42'), hexc('#a8845a')], margin=0.1)
            blades(t, rng, 10, [hexc('#2e5e24'), hexc('#78c04c'), hexc('#a6dc66')], margin=0.2)
        elif kind == 2:   # 돌 몇 개
            for _ in range(3):
                x, y = rnd_inside(t, rng, 0.25)
                t.blob(x, y, rng.uniform(2.2, 3.4), rng.uniform(1.5, 2.2), [hexc('#3a3a40'), hexc('#6a6a72'), hexc('#8a8a94'), hexc('#b0b0ba')])
        else:             # 키 큰 풀 덤불
            for _ in range(3):
                x, y = rnd_inside(t, rng, 0.25)
                for k in range(-2, 3):
                    for j in range(2 + (2 - abs(k))):
                        t.px(x + k, y - j, hexc('#8acc52') if j == 1 + (2 - abs(k)) else hexc('#4c8c34'))
                    t.px(x + k, y + 1, hexc('#2e5e24'))
    return t


def forest(i, mask, rng):
    cols = [hexc('#26381e'), hexc('#2e4424'), hexc('#3a5229'), hexc('#46602e')]
    n = pnoise(5, 21) * 0.55 + pnoise(14, 22) * 0.45
    t = Tile(band(n, cols), mask)
    # 흙 드러난 곳: 칸 안쪽에 무작위 덩어리로 (모든 칸이 같은 무늬로 반복되지 않게)
    soil_cols = [hexc('#2e2218'), hexc('#3a2a1c'), hexc('#4a3624'), hexc('#5a4430')]
    for _ in range(rng.randrange(0, 3)):
        x, y = rnd_inside(t, rng, 0.25)
        t.blob(x, y, rng.uniform(4, 7), rng.uniform(2, 3.2), soil_cols)
    # 떨어진 잎 (경계를 넘어도 되는 작은 점들)
    for _ in range(26):
        x, y = rng.uniform(0, W), rng.uniform(0, H)
        c = [hexc('#a0622a'), hexc('#c88a3a'), hexc('#7a4a22'), hexc('#5a7a2e')][rng.randrange(4)]
        t.px(x, y, c)
        t.px(x + 1, y, c * 0.8)
    if i >= PLAIN:
        kind = (i - PLAIN) % 4
        if kind == 0:     # 버섯
            for _ in range(3):
                x, y = rnd_inside(t, rng, 0.25)
                t.px(x, y + 1, hexc('#e8dcc0'))
                t.px(x, y, hexc('#e8dcc0'))
                t.blob(x, y - 1.5, 2.2, 1.4, [hexc('#5a1414'), hexc('#a02a22'), hexc('#d03a2a'), hexc('#f06a50')], margin=0)
                t.px(x - 0.6, y - 2, hexc('#f8f0e0'))
        elif kind == 1:   # 뿌리
            x, y = rnd_inside(t, rng, 0.3)
            for k in range(14):
                yy = y + math.sin(k * 0.5) * 1.5
                t.px(x - 7 + k, yy, hexc('#6a4a2a'), 0.08)
                t.px(x - 7 + k, yy - 1, hexc('#8a6a3a'), 0.08)
                t.px(x - 7 + k, yy + 1, hexc('#2a1c12'), 0.08)
        elif kind == 2:   # 이끼 덮인 돌
            x, y = rnd_inside(t, rng, 0.3)
            t.blob(x, y, 5, 3, [hexc('#2a2a2e'), hexc('#4a4a52'), hexc('#626270'), hexc('#7a7a88')])
            t.blob(x - 1, y - 1.2, 3.4, 1.6, [hexc('#2e4a22'), hexc('#4a7a30'), hexc('#5e9a3a'), hexc('#7ab84a')], margin=0)
        else:             # 고사리
            for _ in range(2):
                x, y = rnd_inside(t, rng, 0.3)
                for k in range(-3, 4):
                    t.px(x + k, y - abs(k) * 0.4 - 2, hexc('#6aa040'))
                    t.px(x + k, y - abs(k) * 0.4 - 1, hexc('#3e6a28'))
                t.px(x, y, hexc('#2e4a1e'))
    return t


def crypt(i, mask, rng):
    # 한 칸 = 판석 하나 (위쪽 두 변에 줄눈)
    shade = rng.uniform(-0.06, 0.06)
    cols = [hexc('#4a4a58'), hexc('#565666'), hexc('#626274'), hexc('#6e6e80')]
    n = pnoise(4, 31 + i) * 0.5 + pnoise(10, 32 + i) * 0.5 + shade
    t = Tile(band(n, cols, 0.08), mask)
    # 줄눈 (u, v가 0에 가까운 쪽 = 위쪽 두 변) + 아래쪽 두 변에 밝은 모서리
    mortar = mask & ((U < 0.045) | (V < 0.045))
    lit = mask & (((U > 0.045) & (U < 0.09)) | ((V > 0.045) & (V < 0.09))) & ~mortar
    dark = mask & ((U > 0.955) | (V > 0.955))
    t.rgb[lit] = hexc('#7e7e92')
    t.rgb[dark] = hexc('#3e3e4a')
    t.rgb[mortar] = hexc('#24222c')
    if i >= PLAIN:
        kind = (i - PLAIN) % 4
        if kind == 0:     # 금
            x, y = rnd_inside(t, rng, 0.25)
            for k in range(10):
                x += rng.choice((-1, 1))
                y += rng.choice((0, 0.5, 1))
                t.px(x, y, hexc('#2a2834'), 0.1)
                t.px(x + 1, y, hexc('#7a7a8c'), 0.1)
        elif kind == 1:   # 뼈
            x, y = rnd_inside(t, rng, 0.3)
            for k in range(-4, 5):
                t.px(x + k, y + k * 0.25, hexc('#e8e0c8'))
                t.px(x + k, y + k * 0.25 + 1, hexc('#a89a7a'))
            for dx in (-5, 5):
                t.blob(x + dx, y + dx * 0.25, 1.4, 1.2, [hexc('#8a8068'), hexc('#c8bea0'), hexc('#e8e0c8'), hexc('#fff8e8')], margin=0)
        elif kind == 2:   # 해골
            x, y = rnd_inside(t, rng, 0.3)
            t.blob(x, y, 3.2, 2.6, [hexc('#7a7058'), hexc('#c8bea0'), hexc('#e8e0c8'), hexc('#fff8e8')])
            t.px(x - 1, y, hexc('#2a2026'))
            t.px(x + 1, y, hexc('#2a2026'))
            t.px(x, y + 1.5, hexc('#5a5040'))
        else:             # 이끼·핏자국
            m = t.blob(*rnd_inside(t, rng, 0.3), 6, 3, [hexc('#2a3a2e'), hexc('#3e5a40'), hexc('#4a6a4a'), hexc('#5a7a56')])
    return t


def orc(i, mask, rng):
    cols = [hexc('#5e4228'), hexc('#6e4e30'), hexc('#7c5a38'), hexc('#8a6842')]
    n = pnoise(5, 41) * 0.55 + pnoise(13, 42) * 0.45
    t = Tile(band(n, cols), mask)
    # 자갈 (작은 점)
    for _ in range(22):
        x, y = rng.uniform(0, W), rng.uniform(0, H)
        t.px(x, y, hexc('#a08a6a'))
        t.px(x + 1, y + 1, hexc('#4a3420'))
    if i >= PLAIN:
        kind = (i - PLAIN) % 4
        if kind == 0:     # 바퀴·발자국 자국
            x, y = rnd_inside(t, rng, 0.25)
            for k in range(-9, 10):
                t.px(x + k, y + k * 0.5, hexc('#4a321e'), 0.08)
                t.px(x + k, y + k * 0.5 + 3, hexc('#4a321e'), 0.08)
        elif kind == 1:   # 짚더미
            x, y = rnd_inside(t, rng, 0.3)
            for _ in range(28):
                a = rng.uniform(0, math.tau)
                r = rng.uniform(0, 5)
                px_, py_ = x + math.cos(a) * r, y + math.sin(a) * r * 0.5
                t.px(px_, py_, [hexc('#c8a858'), hexc('#e0c470'), hexc('#a08440')][rng.randrange(3)])
        elif kind == 2:   # 재·그을음
            m = t.blob(*rnd_inside(t, rng, 0.3), 6, 3, [hexc('#2a2420'), hexc('#3a322c'), hexc('#4a4038'), hexc('#5a5048')])
            for _ in range(4):
                x, y = rnd_inside(t, rng, 0.38)
                t.px(x, y, hexc('#e0602a'))
        else:             # 뼈다귀
            x, y = rnd_inside(t, rng, 0.3)
            for k in range(-3, 4):
                t.px(x + k, y - k * 0.3, hexc('#e8e0c8'))
                t.px(x + k, y - k * 0.3 + 1, hexc('#9a8a6a'))
            t.px(x - 4, y + 1, hexc('#e8e0c8'))
            t.px(x + 4, y - 1.5, hexc('#e8e0c8'))
    return t


def dragon(i, mask, rng):
    cols = [hexc('#1e1416'), hexc('#2a1c1e'), hexc('#382628'), hexc('#463232')]
    n = pnoise(5, 51) * 0.5 + pnoise(12, 52) * 0.5
    t = Tile(band(n, cols), mask)
    # 현무암 결 (밝은 점)
    for _ in range(18):
        x, y = rng.uniform(0, W), rng.uniform(0, H)
        t.px(x, y, hexc('#5a4440'))
    # 수수한 칸에도 가끔 작은 불씨 균열
    if i < PLAIN and i % 2 == 1:
        x, y = rnd_inside(t, rng, 0.3)
        for k in range(5):
            x += rng.choice((-1, 1))
            t.px(x, y, hexc('#ff8a2a'), 0.15)
            t.px(x, y + 1, hexc('#8a2a14'), 0.15)
    if i >= PLAIN:
        kind = (i - PLAIN) % 4
        if kind in (0, 1):    # 용암 균열 (빛남): 갈라진 줄기 + 주변이 달아오름
            x0, y0 = rnd_inside(t, rng, 0.3)
            for branch in range(3 if kind == 0 else 2):
                x, y = x0, y0
                dx = rng.choice((-1, 1))
                for k in range(rng.randrange(12, 19)):
                    x += dx if rng.random() < 0.8 else -dx
                    y += rng.choice((-0.5, 0, 0.5, 1)) * (1 if branch % 2 else -1)
                    for hx, hy in ((0, -2), (0, 2), (-1, 0), (1, 0)):
                        t.px(x + hx, y + hy, hexc('#5a2418'), 0.1)
                    t.px(x, y - 1, hexc('#ff7a1a'), 0.1)
                    t.px(x, y + 1, hexc('#c83a10'), 0.1)
                    t.px(x, y, hexc('#ffd060'), 0.1)
        elif kind == 2:       # 금화
            for _ in range(6):
                x, y = rnd_inside(t, rng, 0.25)
                t.px(x, y, hexc('#f0c850'))
                t.px(x + 1, y, hexc('#c89030'))
                t.px(x, y - 1, hexc('#fff0a0'))
        else:                 # 그을린 뼈
            x, y = rnd_inside(t, rng, 0.3)
            for k in range(-4, 5):
                t.px(x + k, y + k * 0.3, hexc('#8a7a68'))
                t.px(x + k, y + k * 0.3 + 1, hexc('#4a3a30'))
    return t



# ════════════════════════════════════════════════════════════════
# 절벽 단면 (바닥 가장자리 아래로 늘어지는 면). 64x48: 위 32줄은 바닥 자리(비움), 아래로 면이 내려감
#   edge_L: 왼쪽 아래 변, edge_R: 오른쪽 아래 변, edge_B: 둘 다. Unity 기준점 = (0.5, 32/48) → 마름모 중심이 칸 중심
# ════════════════════════════════════════════════════════════════
EH = 48
DEPTH = 14
EDGE_STYLE = {
    #            윗입술(풀·이끼)   면 색 (밝→어둠)                                            결 무늬
    'Prairie':   ('#6aae44', ['#a8845a', '#8e6c42', '#7a5a36', '#5a4228', '#3a2a1c'], 'strata'),
    'Forest':    ('#46602e', ['#5a4430', '#4a3624', '#3a2a1c', '#2e2218', '#1e160e'], 'roots'),
    'Crypt':     ('#7e7e92', ['#6e6e80', '#626274', '#565666', '#4a4a58', '#2a2a34'], 'bricks'),
    'OrcVillage': ('#8a6842', ['#9a7448', '#8a6842', '#7c5a38', '#6e4e30', '#4a321e'], 'strata'),
    'DragonNest': ('#463232', ['#463232', '#382628', '#2a1c1e', '#1e1416', '#140c0e'], 'lava'),
}


def edge_tile(key, side, k):
    lip, cols, style = EDGE_STYLE[key]
    rng = random.Random(f'edge-{key}-{side}-{k}')
    cols = [hexc(c) for c in cols]
    img = np.zeros((EH, W, 4), np.uint8)
    ys, xs = np.mgrid[0:EH, 0:W]
    faces = []
    if side in ('L', 'B'):
        faces.append('L')
    if side in ('R', 'B'):
        faces.append('R')
    # 열마다 바닥 끝이 조금씩 달라 삐죽한 아랫단
    jag = [DEPTH - (rng.random() < 0.35) * rng.randint(1, 4) for _ in range(W)]
    for f in faces:
        for x in range(W):
            if f == 'L' and x >= 32:
                continue
            if f == 'R' and x < 32:
                continue
            top = 16 + x * 0.5 if f == 'L' else 32 - (x - 32) * 0.5      # 마름모 아래 변의 y
            top = int(math.floor(top + 0.5))
            depth = jag[x]
            for d in range(depth):
                y = top + d
                if y >= EH:
                    break
                t = d / max(1, depth - 1)
                # 왼쪽 면은 빛을 받아 밝고, 오른쪽 면은 그늘
                base = 0.15 + t * 0.75 + (0.0 if f == 'L' else 0.22)
                # 결 무늬
                if style == 'strata':
                    if (d + (x // 6) % 2) % 5 == 0:
                        base += 0.18
                elif style == 'bricks':
                    row = d // 4
                    if d % 4 == 0 or ((x + row * 4) % 10 == 0):
                        base += 0.35
                elif style == 'roots':
                    if (x * 7 + d * 3) % 23 == 0:
                        base -= 0.25
                base += (hash2(x, d, k + 3) - 0.5) * 0.18
                idx = int(min(len(cols) - 1, max(0, base * len(cols))))
                c = cols[idx]
                if d == 0:
                    c = hexc(lip)              # 윗입술 (풀·이끼·모서리 빛)
                elif d == 1 and style in ('strata', 'roots') and key in ('Prairie', 'Forest'):
                    c = hexc(lip) * 0.7
                if style == 'lava' and d > 2 and (x * 13 + k * 7) % 29 == 0 and d < depth - 1:
                    c = hexc('#ff7a1a') if d % 3 else hexc('#ffd060')
                img[y, x, :3] = c
                img[y, x, 3] = 255
            # 매달린 뿌리·풀 (몇 칸만)
            if style == 'roots' and rng.random() < 0.12:
                for d in range(depth, min(EH - top, depth + 3)):
                    img[top + d, x] = (90, 64, 40, 255)
            if key == 'Prairie' and rng.random() < 0.18:
                img[top + 1, x] = (74, 130, 52, 255)
    return Image.fromarray(img, 'RGBA')


def edges(key):
    folder = os.path.join(OUT, key)
    for side in ('L', 'R', 'B'):
        for k in range(2):
            edge_tile(key, side, k).save(os.path.join(folder, f'edge_{side}_{k}.png'))


THEMES = {
    'Prairie': ('초원', prairie),
    'Forest': ('숲', forest),
    'Crypt': ('지하 묘지', crypt),
    'OrcVillage': ('오크 부락', orc),
    'DragonNest': ('드래곤 둥지', dragon),
}


def preview(tiles, path, cols=9, rows=9, scale=3):
    rnd = random.Random(7)
    weighted = [t for i, t in enumerate(tiles) for _ in range(3 if i < PLAIN else 1)]
    pw, ph = (cols + rows) * 32 + 64, (cols + rows) * 16 + 32
    im = Image.new('RGBA', (pw, ph), (12, 10, 14, 255))
    for r in range(rows):
        for c in range(cols):
            im.alpha_composite(rnd.choice(weighted), ((c - r) * 32 + rows * 32, (c + r) * 16))
    im.resize((pw * scale, ph * scale), Image.NEAREST).save(path)


def main():
    mask = load_mask()
    prev = os.environ.get('DUNGEON_PREVIEW')
    for key, (region, fn) in THEMES.items():
        folder = os.path.join(OUT, key)
        os.makedirs(folder, exist_ok=True)
        tiles = []
        for i in range(PLAIN + DETAIL):
            rng = random.Random(f'{key}-{i}')   # 문자열 씨앗: 매번 같은 결과
            img = fn(i, mask, rng).image()
            img.save(os.path.join(folder, f'floor_{i}.png'))
            tiles.append(img)
        edges(key)
        if prev:
            os.makedirs(prev, exist_ok=True)
            preview(tiles, os.path.join(prev, f'preview_{key}.png'))
        print(key, region, len(tiles))


if __name__ == '__main__':
    main()
