# 의뢰 게시판 지역 카드 도트 그림 생성기 (144x180, 화면에 2배 = 288x360)
# 실행: python Tools/region_art_gen.py  → Assets/Resources/UI/Regions/*.png
#   {지역}_gray.png   : 평소 (흑백)
#   {지역}_0~7.png    : 마우스를 올렸을 때 (컬러, 움직임)
#   lock_0~7.png      : 잠긴 지역에 마우스를 올렸을 때 (쇠사슬 X + 자물쇠, 한 번 재생 후 마지막 장 유지)
# 도트 애니메 배경 기법: 단계 그라데이션(디더링), 명암 구름, 대기 원근(먼 것은 흐리고 푸르게), 빛줄기·빛 번짐, 앞쪽 실루엣
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(__file__))
from pixel_lib import P, Canvas, shade, mix, hexc

OUT = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'Resources', 'UI', 'Regions')
W, H = 144, 180
FRAMES = 8
BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]


# ─── 기본 도구 ──────────────────────────────────────────────────

def dither(x, y, t):
    """t(0~1) 비율만큼 참 (4x4 베이어)"""
    return t * 16 > BAYER[y % 4][x % 4] + 0.5


def bands(cv, y0, y1, colors, x0=0, x1=W - 1):
    """여러 색 단계 그라데이션 (경계는 디더링)"""
    n = len(colors) - 1
    for y in range(y0, y1 + 1):
        t = (y - y0) / max(1, y1 - y0) * n
        i = min(n - 1, int(t))
        f = t - i
        for x in range(x0, x1 + 1):
            cv.p(x, y, colors[i + 1] if dither(x, y, f) else colors[i])


def disc(cv, cx, cy, r, c):
    for y in range(int(cy - r - 1), int(cy + r) + 2):
        for x in range(int(cx - r - 1), int(cx + r) + 2):
            if (x - cx) ** 2 + (y - cy) ** 2 <= r * r:
                cv.p(x, y, c)


def ellipse_fill(cv, cx, cy, rx, ry, fn):
    """타원 안의 각 칸에 fn(x, y, u, v) 색 (u, v = -1~1)"""
    for y in range(int(cy - ry), int(cy + ry) + 1):
        for x in range(int(cx - rx), int(cx + rx) + 1):
            u, v = (x - cx) / rx, (y - cy) / ry
            if u * u + v * v <= 1:
                c = fn(x, y, u, v)
                if c: cv.p(x, y, c)


def glow(cv, cx, cy, r, rgb, strength=0.5):
    """빛 번짐 (가운데가 진함)"""
    for y in range(int(cy - r), int(cy + r) + 1):
        for x in range(int(cx - r), int(cx + r) + 1):
            d = math.hypot(x - cx, y - cy) / r
            if d < 1:
                cv.p(x, y, rgb + (int(255 * strength * (1 - d) ** 2),))


def cloud(cv, x, y, w, h, light, mid, dark):
    """뭉게구름: 윗부분은 밝게, 아래는 그늘 (디더링)"""
    random.seed(int(x * 7 + y * 13))
    puffs = []
    for k in range(5):
        px = x + w * (k + 0.5) / 5 + random.uniform(-2, 2)
        r = h * random.uniform(0.45, 0.75) * (1.1 if k in (1, 2, 3) else 0.85)
        puffs.append((px, y + h - r * 0.9, r))
    top = min(py - r for px, py, r in puffs)
    for yy in range(int(top) - 1, int(y + h) + 1):
        for xx in range(int(x) - 2, int(x + w) + 3):
            inside = any((xx - px) ** 2 + (yy - py) ** 2 <= r * r for px, py, r in puffs) and yy <= y + h
            if not inside: continue
            t = (yy - top) / max(1, (y + h - top))
            c = light if t < 0.45 else (mid if dither(xx, yy, (t - 0.45) / 0.25) or t < 0.55 else mid)
            if t > 0.7: c = dark if dither(xx, yy, (t - 0.7) / 0.3) else mid
            cv.p(xx, yy, c)


def noise(x, y, p):
    """규칙 없는 점 질감 (같은 칸은 항상 같은 값) — 비스듬한 줄무늬가 생기지 않도록"""
    h = (x * 374761393 + y * 668265263) & 0xffffffff
    h = ((h ^ (h >> 13)) * 1274126177) & 0xffffffff
    return (h & 0xffff) / 0xffff < p


def wave(f, k=0.0, amp=1.0):
    return math.sin(2 * math.pi * f / FRAMES + k) * amp


def frame_border(cv):
    for x in range(W):
        cv.p(x, 0, P['outline']); cv.p(x, H - 1, P['outline'])
    for y in range(H):
        cv.p(0, y, P['outline']); cv.p(W - 1, y, P['outline'])


def leaf_cluster(cv, cx, cy, r, tones, seed, sway=0.0):
    """나뭇잎 덩어리: 작은 잎 뭉치를 겹쳐 3단 명암 (왼쪽 위가 밝음)"""
    random.seed(seed)
    blobs = [(cx + random.uniform(-r, r) * 0.8, cy + random.uniform(-r, r) * 0.6, r * random.uniform(0.35, 0.6)) for _ in range(9)]
    blobs.sort(key=lambda b: b[1])
    for bx, by, br in blobs:
        bx += sway
        def fn(x, y, u, v):
            light = -u * 0.6 - v * 0.8
            if light > 0.55: return tones[3]
            if light > 0.1: return tones[2] if not dither(x, y, 0.2) else tones[3]
            if light > -0.4: return tones[1]
            return tones[0]
        ellipse_fill(cv, bx, by, br, br * 0.85, fn)


# ─── 초원 ──────────────────────────────────────────────────────

def prairie(f):
    cv = Canvas(W, H)
    bands(cv, 0, 100, [hexc('2a5ab8'), hexc('3a7ad0'), hexc('5aa0e0'), hexc('8ac8f0'), hexc('d8f0f8')])
    # 해와 빛 번짐
    glow(cv, 118, 22, 30, (255, 250, 220), 0.55)
    disc(cv, 118, 22, 7, hexc('fffbe8'))
    # 큰 뭉게구름 (천천히 흐름)
    drift = f * 0.75
    cloud(cv, 6 - drift, 22, 50, 18, hexc('ffffff'), hexc('e0eef8'), hexc('a8c4e0'))
    cloud(cv, 70 - drift, 44, 40, 12, hexc('ffffff'), hexc('e0eef8'), hexc('a8c4e0'))
    cloud(cv, 150 - drift, 30, 36, 12, hexc('ffffff'), hexc('e0eef8'), hexc('a8c4e0'))
    # 먼 산 (눈 덮인 봉우리, 푸르게 흐림)
    for x in range(W):
        h = 74 + int(10 * math.sin(x * 0.045 + 0.5) + 6 * math.sin(x * 0.11))
        for y in range(h, 104):
            c = hexc('6a8ab0') if noise(x, y, 0.1) else hexc('7a9ac0')
            if y < h + 4 and h < 72: c = hexc('e8f0f8')
            cv.p(x, y, c)
        if h < 70:
            for y in range(h, h + 3): cv.p(x, y, hexc('f0f6fc'))
    # 중간 언덕 (빛·그늘)
    for x in range(W):
        h = 96 + int(6 * math.sin(x * 0.04 + 2.2))
        for y in range(h, H):
            t = (y - h) / 30
            c = hexc('9ad070') if t < 0.3 else hexc('7aba58') if t < 0.7 else hexc('5aa044')
            if t < 0.15 and dither(x, y, 0.5): c = hexc('b8e488')
            cv.p(x, y, c)
    # 언덕 위 큰 나무
    tx, ty = 104, 98
    for y in range(ty - 22, ty + 2):
        for x in range(tx - 2, tx + 2):
            cv.p(x, y, P['wood_d'] if x == tx - 2 else P['wood'] if x < tx + 1 else P['dark_wood'])
    tones = [hexc('2e6a30'), hexc('3e8a3a'), hexc('5aaa4a'), hexc('8ad06a')]
    leaf_cluster(cv, tx, ty - 28, 14, tones, 11, sway=wave(f, 0, 0.8))
    # 굽이진 흙길
    for y in range(104, H):
        t = (y - 104) / (H - 104)
        cx = 60 + math.sin(t * 3.2) * 18 * (1 - t * 0.3)
        hw = 1 + t * 14
        for x in range(int(cx - hw), int(cx + hw) + 1):
            c = hexc('d8b880') if abs(x - cx) < hw * 0.6 else hexc('b89860')
            cv.p(x, y, c)
    # 앞쪽 들풀 (바람에 흔들림, 큼직한 실루엣)
    random.seed(21)
    for k in range(120):
        x = random.randint(0, W - 1)
        base = random.randint(124, H)
        hgt = random.randint(5, 16) * (1 + (base - 124) / 60)
        lean = wave(f, x * 0.15, 2.2) + 1
        col = random.choice([hexc('4a9a34'), hexc('6aba44'), hexc('8ad060'), hexc('3a7a2a')])
        for i in range(int(hgt)):
            t = i / hgt
            cv.p(x + int(round(lean * t * t * 1.5)), base - i, col if t < 0.8 else shade(col, 1.25))
    # 들꽃
    random.seed(22)
    for k in range(26):
        x = random.randint(2, W - 3); y = random.randint(118, H - 4)
        dx = int(round(wave(f, x * 0.15, 1.2)))
        c = random.choice([hexc('ffffff'), hexc('ffe070'), hexc('ff8aa8'), hexc('a8c8ff')])
        cv.p(x + dx, y, c); cv.p(x + dx + 1, y, c); cv.p(x + dx, y - 1, c); cv.p(x + dx + 1, y - 1, shade(c, 0.85))
    # 바람에 날리는 꽃잎·빛가루
    for k in range(7):
        t = ((f / FRAMES) + k / 7) % 1
        x = int((k * 41 + t * 120) % W)
        y = int(80 + k * 9 + math.sin(t * 6.28 + k) * 6)
        cv.p(x, y, hexc('fff0f8')); cv.p(x + 1, y, (255, 200, 220, 160))
    frame_border(cv)
    return cv


# ─── 숲 ────────────────────────────────────────────────────────

def forest(f):
    cv = Canvas(W, H)
    bands(cv, 0, H - 1, [hexc('1a3a2a'), hexc('2a5a3a'), hexc('4a7a4a'), hexc('8aaa6a'), hexc('5a7a3a')])
    # 먼 나무 (안개 속, 푸르게)
    for layer, (col, step, top) in enumerate(((hexc('5a8a6a'), 9, 30), (hexc('3e6e4e'), 13, 20))):
        for x0 in range(-4, W, step):
            w = 3 + layer * 2
            for y in range(top + (x0 * 7) % 20, 150):
                for x in range(x0, x0 + w): cv.p(x, y, col)
        # 안개 띠
        for y in range(110 + layer * 12, 126 + layer * 12):
            for x in range(W):
                cv.p(x, y, (200, 230, 210, 40))
    # 빛줄기 (일렁임)
    for k, x0 in enumerate((30, 70, 108)):
        a = 32 + 14 * wave(f, k * 1.7, 1)
        for y in range(0, 150):
            xs = x0 + y // 3
            for x in range(xs, xs + 9):
                cv.p(x, y, (255, 250, 200, int(a * (1 - y / 170))))
    # 가까운 큰 줄기 (이끼)
    for tx, tw in ((14, 14), (124, 16)):
        for y in range(0, 160):
            for x in range(tx - tw // 2, tx + tw // 2):
                u = (x - (tx - tw // 2)) / tw
                c = hexc('6a4a30') if u < 0.25 else hexc('4a3020') if u < 0.7 else hexc('2e1e14')
                if noise(x, y, 0.08): c = shade(c, 0.8)
                if u < 0.35 and (y // 6) % 3 == 0 and dither(x, y, 0.5): c = hexc('5a8a3a')
                cv.p(x, y, c)
    # 땅·이끼 낀 길
    for y in range(146, H):
        for x in range(W):
            c = hexc('2e4a1e') if noise(x, y, 0.15) else hexc('3a5a24')
            cv.p(x, y, c)
    for y in range(146, H):
        t = (y - 146) / (H - 146)
        hw = 6 + t * 26
        for x in range(int(72 - hw), int(72 + hw)):
            c = hexc('a88a5a') if dither(x, y, 0.7) else hexc('8a6e44')
            cv.p(x, y, c)
    # 고사리·버섯
    random.seed(31)
    for k in range(14):
        x = random.randint(4, W - 6); y = random.randint(150, H - 4)
        for i in range(6):
            dx = int(round(wave(f, k, 0.8) * i / 6))
            cv.p(x - i + dx, y - i // 2, hexc('6aaa4a')); cv.p(x + i + dx, y - i // 2, hexc('4a8a34'))
    for x in (38, 98):
        cv.rect(x, 150, x + 1, 154, hexc('f0e8d8'))
        ellipse_fill(cv, x + 0.5, 149, 4, 2.5, lambda xx, yy, u, v: hexc('e8483a') if v < 0.3 else hexc('a82a20'))
        cv.p(x - 1, 148, hexc('ffffff')); cv.p(x + 2, 149, hexc('ffffff'))
    # 위쪽을 덮은 잎 (흔들림, 앞쪽 실루엣)
    tones = [hexc('1a4a24'), hexc('2a6a30'), hexc('4a8a3a'), hexc('7ab85a')]
    for k, (cx, cy, r) in enumerate(((10, 6, 26), (50, -2, 24), (92, 4, 26), (138, 8, 24), (30, 24, 16), (118, 26, 16))):
        leaf_cluster(cv, cx, cy, r, tones, 40 + k, sway=wave(f, k * 0.9, 1.6))
    # 반딧불·꽃가루
    for k in range(10):
        t = ((f / FRAMES) + k / 10) % 1
        x = (k * 37 + int(t * 20)) % W
        y = int(70 + (k * 23) % 70 + math.sin(t * 6.28 + k) * 4)
        glow(cv, x, y, 3, (220, 255, 160), 0.6)
        cv.p(x, y, hexc('f8ffd0'))
    # 떨어지는 잎
    for k in range(3):
        t = ((f / FRAMES) + k / 3) % 1
        x = int(30 + k * 40 + t * 16 + math.sin(t * 9) * 4)
        y = int(30 + t * 110)
        cv.p(x, y, hexc('e8c040')); cv.p(x + 1, y, hexc('c8902a')); cv.p(x, y + 1, hexc('c8902a'))
    frame_border(cv)
    return cv


# ─── 공동묘지 (지하 묘지) ───────────────────────────────────────

def crypt(f):
    cv = Canvas(W, H)
    bands(cv, 0, 130, [hexc('0e0a20'), hexc('1a1638'), hexc('2a2450'), hexc('3e3a68'), hexc('5a5a80')])
    random.seed(41)
    for k in range(40):
        x, y = random.randint(1, W - 2), random.randint(1, 90)
        tw = (k + f) % 6
        cv.p(x, y, hexc('ffffff') if tw < 2 else hexc('8a8ab8'))
    # 달 + 후광 + 지나가는 구름
    glow(cv, 104, 34, 34, (220, 220, 255), 0.45)
    ellipse_fill(cv, 104, 34, 15, 15, lambda x, y, u, v: hexc('fffbe8') if u + v * 0.3 < 0.4 else hexc('d8d4c0'))
    for cx, cy, r in ((99, 30, 3), (110, 40, 2), (102, 41, 1.5)):
        disc(cv, cx, cy, r, hexc('c8c4b0'))
    cx = 60 + f * 3
    cloud(cv, cx, 36, 44, 9, hexc('5a5a88'), hexc('3e3e6a'), hexc('2a2a50'))
    # 고딕 납골당
    st, st_d, st_l = hexc('6a6880'), hexc('4a4860'), hexc('9a98ae')
    for y in range(64, 130):
        for x in range(40, 104):
            c = st
            if (y - 64) % 7 == 0 or (x - 40 + ((y - 64) // 7 % 2) * 6) % 12 == 0: c = st_d
            elif x < 46: c = st_l
            cv.p(x, y, c)
    for i in range(30):
        w = 34 - int(i * 1.1)
        for x in range(72 - w, 72 + w + 1): cv.p(x, 64 - i, st_l if x < 72 else st)
    cv.rect(70, 22, 74, 36, st_l); cv.rect(66, 26, 78, 28, st_l)
    # 스테인드글라스 창 (빛남)
    glow(cv, 72, 52, 12, (255, 200, 120), 0.35 + 0.1 * wave(f, 0, 1))
    ellipse_fill(cv, 72, 52, 6, 6, lambda x, y, u, v: [hexc('ff6a5a'), hexc('5a8aff'), hexc('ffd860'), hexc('8aff8a')][(int((math.atan2(v, u) + 3.2) / 1.6)) % 4])
    disc(cv, 72, 52, 1.5, hexc('fff4d0'))
    # 문
    for y in range(96, 130):
        for x in range(60, 85):
            if y > 104 or (x - 72) ** 2 + ((y - 104) * 1.2) ** 2 <= 12 ** 2:
                cv.p(x, y, hexc('0c0818'))
    # 철 울타리
    for x in range(4, W, 6):
        for y in range(118, 140): cv.p(x, y, hexc('1a1828'))
        cv.p(x, 117, hexc('3a3850')); cv.p(x - 1, 118, hexc('1a1828')); cv.p(x + 1, 118, hexc('1a1828'))
    for x in range(W): cv.p(x, 124, hexc('1a1828')); cv.p(x, 134, hexc('1a1828'))
    # 땅
    bands(cv, 138, H - 1, [hexc('2a3424'), hexc('1e281a'), hexc('141c12')])
    # 묘비 (이끼)
    for x, y, kind in ((20, 158, 'r'), (46, 166, 'c'), (96, 162, 'r'), (122, 156, 'c'), (70, 174, 'r')):
        if kind == 'r':
            ellipse_fill(cv, x, y - 14, 7, 6, lambda xx, yy, u, v: st_l if u < -0.3 else st)
            for yy in range(y - 14, y + 1):
                for xx in range(x - 7, x + 8): cv.p(xx, yy, st_l if xx < x - 4 else st if xx < x + 5 else st_d)
            cv.rect(x - 3, y - 10, x + 3, y - 10, st_d); cv.rect(x - 2, y - 7, x + 2, y - 7, st_d)
            for k in range(4): cv.p(x - 6 + k * 3, y - (k % 2), hexc('4a7a3a'))
        else:
            cv.rect(x - 1, y - 18, x + 2, y, st_l); cv.rect(x - 6, y - 13, x + 7, y - 10, st_l)
            cv.rect(x + 2, y - 18, x + 2, y, st_d)
        cv.rect(x - 8, y + 1, x + 8, y + 2, hexc('0e1410'))
    # 앞쪽 죽은 나무 실루엣
    sil = hexc('0a0812')
    for y in range(40, H):
        w = 3 + (y - 40) // 40
        for x in range(4, 4 + w): cv.p(x, y, sil)
    for (x0, y0), (x1, y1) in (((6, 70), (26, 52)), ((24, 54), (34, 48)), ((5, 56), (-2, 44)), ((6, 90), (22, 82))):
        cv.line((x0, y0), (x1, y1), sil); cv.line((x0, y0 + 1), (x1, y1 + 1), sil)
    # 땅 안개 (흐름)
    for k in range(8):
        fx = (k * 26 + f * 4) % (W + 40) - 20
        fy = 146 + (k % 4) * 8
        ellipse_fill(cv, fx, fy, 22, 4, lambda x, y, u, v: (200, 210, 240, int(70 * (1 - u * u - v * v))))
    # 도깨비불
    for k, (wx, wy) in enumerate(((28, 120), (112, 128), (88, 100))):
        y = wy + wave(f, k * 2.1, 4)
        x = wx + wave(f, k * 1.3 + 1, 2)
        glow(cv, x, y, 9, (120, 230, 255), 0.55)
        disc(cv, x, y, 2, hexc('d8faff'))
    frame_border(cv)
    return cv


# ─── 오크 부락 ──────────────────────────────────────────────────

def orc(f):
    cv = Canvas(W, H)
    bands(cv, 0, 110, [hexc('3a1a4a'), hexc('8a2a4a'), hexc('d8483a'), hexc('f08a3a'), hexc('ffd080')])
    glow(cv, 72, 96, 40, (255, 220, 140), 0.5)
    disc(cv, 72, 96, 12, hexc('fff0c0'))
    cloud(cv, 8 - f * 0.5, 30, 46, 10, hexc('f0a070'), hexc('c86048'), hexc('8a3a48'))
    cloud(cv, 90 - f * 0.5, 50, 40, 8, hexc('ffb080'), hexc('d87050'), hexc('9a4048'))
    # 먼 산 실루엣 두 겹
    for col, base, amp, k in ((hexc('6a2a4a'), 92, 12, 0.05), (hexc('4a1a34'), 104, 8, 0.08)):
        for x in range(W):
            h = base + int(amp * math.sin(x * k + base))
            for y in range(h, 120): cv.p(x, y, col)
    # 땅
    bands(cv, 118, H - 1, [hexc('6a3a24'), hexc('5a3020'), hexc('3e2016')])
    # 뾰족 말뚝 울타리
    for x in range(0, W, 7):
        top = 100 + (x * 3) % 6
        for y in range(top, 126):
            for xx in range(x, x + 5):
                cv.p(xx, y, hexc('8a5a34') if xx < x + 2 else hexc('6a4024') if xx < x + 4 else hexc('3a2010'))
        for i in range(4): cv.rect(x + i // 2, top - i, x + 4 - i // 2, top - i, hexc('a8784a'))
    for x in range(W): cv.p(x, 112, hexc('2a1408')); cv.p(x, 120, hexc('2a1408'))
    # 가죽 천막 두 채
    for cx, base, s in ((30, 150, 1.0), (116, 146, 0.85)):
        hgt = int(46 * s)
        for i in range(hgt):
            w = int(i * 0.8 * s) + 1
            y = base - hgt + i
            for x in range(cx - w, cx + w + 1):
                u = (x - cx) / max(1, w)
                c = hexc('c08a50') if u < -0.3 else hexc('a06a3a') if u < 0.4 else hexc('7a4a28')
                if (i // 6) % 2 == 0 and abs(u) < 0.9 and noise(x, i, 0.12): c = hexc('d8b070')
                if i % 12 == 11: c = hexc('5a3018')
                cv.p(x, y, c)
        cv.rect(cx - 5, base - 14, cx + 5, base, hexc('1a0c06'))
        for k in (-1, 1): cv.line((cx, base - hgt - 4), (cx + k * 5, base - hgt + 4), hexc('3a2010'))
    # 해골 토템
    tx = 72
    cv.rect(tx - 1, 104, tx + 1, 150, hexc('5a3018'))
    ellipse_fill(cv, tx, 102, 5, 4, lambda x, y, u, v: hexc('f0e8d0') if u < 0.2 else hexc('c8bca0'))
    cv.p(tx - 2, 102, hexc('1a0c06')); cv.p(tx + 2, 102, hexc('1a0c06')); cv.rect(tx - 1, 105, tx + 1, 105, hexc('1a0c06'))
    cv.line((tx - 9, 96), (tx - 4, 100), hexc('f0e8d0')); cv.line((tx + 9, 96), (tx + 4, 100), hexc('f0e8d0'))
    # 전투 깃발 (펄럭임)
    cv.rect(100, 70, 101, 150, hexc('3a2010'))
    for y in range(72, 92):
        for x in range(102, 126):
            dy = int(round(wave(f, (x - 102) * 0.35, 2) * (x - 102) / 24))
            c = hexc('b82a2a') if (y - 72) % 6 else hexc('8a1a1a')
            if 110 <= x <= 118 and 77 <= y <= 86: c = hexc('f0e8d0') if (x + y) % 3 else hexc('c8bca0')
            cv.p(x, y + dy, c)
    # 모닥불 (크게, 불꽃과 불티)
    bx, by = 58, 166
    glow(cv, bx, by - 10, 34, (255, 160, 60), 0.45 + 0.08 * wave(f, 0, 1))
    for k in range(6):
        cv.line((bx - 12 + k * 4, by + 2), (bx - 4 + k * 2, by - 6), hexc('3a2010'))
    for k in range(5):
        fx = bx - 8 + k * 4
        hgt = 12 + int(6 * abs(wave(f, k * 1.3, 1))) + (4 if k == 2 else 0)
        for i in range(hgt):
            t = i / hgt
            w = int((1 - t) * 3.2)
            sway = int(round(wave(f, k + i * 0.3, 1.2) * t))
            col = hexc('fff6c0') if t < 0.25 else hexc('ffd860') if t < 0.55 else hexc('f08a24') if t < 0.8 else hexc('c8401a')
            for dx in range(-w, w + 1): cv.p(fx + dx + sway, by - i, col)
    for k in range(8):
        t = ((f / FRAMES) + k / 8) % 1
        x = int(bx + math.sin(t * 7 + k) * 8)
        y = int(by - 14 - t * 60)
        cv.p(x, y, hexc('ffd060') if k % 2 else hexc('ff8030'))
    # 연기
    for k in range(3):
        t = ((f / FRAMES) + k / 3) % 1
        ellipse_fill(cv, bx + math.sin(t * 4) * 4, by - 40 - t * 50, 4 + t * 6, 3 + t * 5,
                     lambda x, y, u, v, t=t: (70, 50, 60, int(110 * (1 - t))))
    frame_border(cv)
    return cv


# ─── 드래곤 둥지 ───────────────────────────────────────────────

def dragon(f):
    cv = Canvas(W, H)
    bands(cv, 0, 120, [hexc('120408'), hexc('2a0810'), hexc('5a1418'), hexc('a02a1a'), hexc('d8582a')])
    pulse = 0.5 + 0.5 * math.sin(2 * math.pi * f / FRAMES)
    # 화산
    for x in range(W):
        h = int(52 + abs(x - 72) * 1.15)
        for y in range(h, 140):
            u = (x - 72) / 70
            c = hexc('4a2a2a') if u < -0.1 else hexc('3a2020') if u < 0.3 else hexc('2a1618')
            if noise(x, y, 0.08): c = shade(c, 0.8)
            cv.p(x, y, c)
    glow(cv, 72, 50, 40, (255, 120, 40), 0.4 + 0.2 * pulse)
    for x in range(62, 83): cv.p(x, 51, hexc('ffd060')); cv.p(x, 52, hexc('ff8a2a'))
    # 화산재 구름
    for k in range(3):
        t = ((f / FRAMES) + k / 3) % 1
        ellipse_fill(cv, 72 + math.sin(t * 3 + k) * 6, 44 - t * 40, 8 + t * 10, 5 + t * 6,
                     lambda x, y, u, v, t=t: (60, 40, 40, int(150 * (1 - t))))
    # 용암 줄기 (흐름)
    for side in (-1, 1):
        x = 72 + side * 4
        for y in range(52, 140):
            x += side * (1 if (y * 7) % 10 < 3 else 0)
            hot = ((y - f * 3) // 4) % 3
            c = [hexc('fff0a0'), hexc('ffb040'), hexc('e8581a')][hot]
            cv.p(x, y, c); cv.p(x + side, y, shade(c, 0.75))
            if y % 5 == 0: cv.p(x - side, y, (255, 140, 40, 120))
    # 앞쪽 바위 기둥 (실루엣 프레이밍)
    sil = hexc('1a0a0c')
    for x0, top, w in ((0, 60, 22), (126, 76, 18)):
        for y in range(top, H):
            ww = w + (y - top) // 12
            for x in range(x0 if x0 == 0 else W - ww, (x0 + ww) if x0 == 0 else W):
                cv.p(x, y, hexc('2a1214') if noise(x, y, 0.1) else sil)
            for x in range(x0 if x0 == 0 else W - ww, (x0 + ww) if x0 == 0 else W):
                if y == top: cv.p(x, y, hexc('5a2a20'))
    # 둥지 (뼈·알)
    bands(cv, 140, H - 1, [hexc('2a1416'), hexc('1e0e10')], 0, W - 1)
    for k in range(14):
        x = 36 + k * 5
        cv.line((x, 166), (x + 8, 156 + (k % 3)), hexc('e0d0b0'))
        cv.line((x, 167), (x + 8, 157 + (k % 3)), hexc('a89880'))
    for k, (ex, ey, col) in enumerate(((60, 158, hexc('8ab060')), (74, 155, hexc('c86a5a')), (86, 158, hexc('6a8ab8')))):
        glow(cv, ex, ey, 8, (255, 200, 120), 0.25 + 0.15 * pulse)
        ellipse_fill(cv, ex, ey, 5, 6, lambda x, y, u, v, col=col: shade(col, 1.3) if u + v < -0.6 else col if u + v < 0.5 else shade(col, 0.7))
    # 용 (날갯짓하며 하늘을 가로지름)
    t = f / FRAMES
    dx = 20 + t * 100
    dy = 28 + math.sin(t * math.pi * 2) * 6
    body = hexc('0a0204')
    flap = math.sin(t * math.pi * 2 * 2)
    ellipse_fill(cv, dx, dy, 9, 2.5, lambda x, y, u, v: body)
    ellipse_fill(cv, dx + 10, dy - 2, 3, 2, lambda x, y, u, v: body)
    cv.line((dx + 12, dy - 3), (dx + 15, dy - 5), body)
    cv.line((dx - 9, dy), (dx - 20, dy + 4), body); cv.line((dx - 20, dy + 4), (dx - 24, dy + 2), body)
    for i in range(12):
        wy = dy - 1 - flap * (10 - abs(i - 4) * 1.2)
        cv.line((dx - 4 + i, dy - 1), (dx - 8 + i * 1.6, wy), body)
    # 불씨
    for k in range(14):
        tt = ((f / FRAMES) + k / 14) % 1
        x = int((k * 29 + math.sin(tt * 6 + k) * 6) % W)
        y = int(H - 8 - tt * 140)
        cv.p(x, y, hexc('ffd060') if k % 3 else hexc('ff7030'))
    frame_border(cv)
    return cv


# ─── 흑백 ──────────────────────────────────────────────────────

def grayscale(cv):
    g = Canvas(W, H)
    px = cv.im.load()
    for y in range(H):
        for x in range(W):
            r, gg, b, a = px[x, y]
            l = int((0.3 * r + 0.59 * gg + 0.11 * b) * 0.8)
            g.px[x, y] = (l, l, l, a)
    return g


# ─── 쇠사슬 X + 자물쇠 ────────────────────────────────────────

def lock_frames():
    frames = []
    total = 8
    for f in range(total):
        cv = Canvas(W, H)
        dark = min(1.0, f / 3)
        for y in range(H):
            for x in range(W):
                cv.p(x, y, (10, 6, 12, int(120 * dark)))
        reach = min(1.0, (f + 1) / 4)
        for (x0, y0), (x1, y1) in (((5, 5), (W - 6, H - 6)), ((W - 6, 5), (5, H - 6))):
            length = math.hypot(x1 - x0, y1 - y0)
            n = int(length / 7)
            for i in range(n + 1):
                t = i / n
                if not (t <= reach / 2 or t >= 1 - reach / 2):
                    continue
                x = x0 + (x1 - x0) * t
                y = y0 + (y1 - y0) * t
                horizontal = i % 2 == 0
                for dy in range(-3, 4):
                    for dx in range(-4, 5):
                        e = (dx / 4) ** 2 + (dy / 2.6) ** 2 if horizontal else (dx / 2.6) ** 2 + (dy / 4) ** 2
                        if 0.35 <= e <= 1.05:
                            c = P['iron_l'] if (dy < 0 or dx < 0) else P['iron']
                            if e < 0.5 and dy < 0: c = hexc('d8dce8')
                            cv.p(int(x + dx), int(y + dy), c)
                        elif 1.05 < e < 1.6:
                            cv.p(int(x + dx), int(y + dy), P['iron_d'])
        if f >= 4:
            cx = W // 2
            cy = {4: 36, 5: 86, 6: 98, 7: 92}[f]
            for a in range(0, 181, 6):
                rad = math.radians(a)
                x = cx + int(math.cos(rad) * 10)
                y = cy - 9 - int(math.sin(rad) * 11)
                cv.rect(x - 1, y - 1, x + 2, y + 1, P['iron_l'])
            cv.rect(cx - 16, cy - 9, cx + 16, cy + 17, P['outline'])
            cv.rect(cx - 15, cy - 8, cx + 15, cy + 16, P['gold_d'])
            cv.rect(cx - 15, cy - 8, cx + 13, cy + 13, P['gold'])
            cv.rect(cx - 13, cy - 7, cx + 10, cy - 5, P['gold_l'])
            cv.rect(cx - 14, cy - 7, cx - 13, cy + 10, P['gold_l'])
            disc(cv, cx, cy + 2, 3, P['outline'])
            cv.rect(cx - 1, cy + 3, cx + 1, cy + 9, P['outline'])
            if f == 6:
                for d in (-22, 22):
                    for k in range(3):
                        cv.p(cx + d + (k if d > 0 else -k), cy - 4 + k * 3, P['paper_l'])
        frames.append(cv)
    return frames


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    for name in os.listdir(OUT):  # 예전 프레임(개수가 달라질 수 있음) 정리
        if name.endswith('.png'):
            os.remove(os.path.join(OUT, name))
    regions = {'prairie': prairie, 'forest': forest, 'crypt': crypt, 'orc': orc, 'dragon': dragon}
    for key, fn in regions.items():
        frames = [fn(f) for f in range(FRAMES)]
        for i, cv in enumerate(frames):
            cv.save(os.path.join(OUT, f'{key}_{i}.png'))
        grayscale(frames[0]).save(os.path.join(OUT, f'{key}_gray.png'))
    for i, cv in enumerate(lock_frames()):
        cv.save(os.path.join(OUT, f'lock_{i}.png'))
    print('ok')
