# 길드 밖 거리 + 신전·치유소·건축 공방 실내 도트 그림 생성기 (정면 시점)
# 실행: python Tools/town_art_gen.py  → Assets/Art/Town/*.png
# 화면(1920x1080)에 정확히 4배로 보이도록 배경은 480x270, 건물 정면은 100x116
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(__file__))
from pixel_lib import P, Canvas, shade, mix, flame, hexc

OUT = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'Art', 'Town')
W, H = 480, 270
STREET_Y = 200   # 길이 시작되는 높이 (그림 좌표)


def save(cv, name):
    cv.save(os.path.join(OUT, name + '.png'))


def gradient(cv, x0, y0, x1, y1, top, bottom):
    for y in range(y0, y1 + 1):
        c = mix(top, bottom, (y - y0) / max(1, y1 - y0))
        for x in range(x0, x1 + 1):
            cv.p(x, y, c)


def disc(cv, cx, cy, r, c):
    for y in range(int(cy - r), int(cy + r) + 1):
        for x in range(int(cx - r), int(cx + r) + 1):
            if (x - cx) ** 2 + (y - cy) ** 2 <= r * r:
                cv.p(x, y, c)


def cloud(cv, x, y, s):
    parts = [(0, 0, 6), (7, -3, 8), (16, 0, 6), (9, 3, 6)]
    for dx, dy, r in parts:
        disc(cv, x + dx * s, y + dy * s + 2, r * s, hexc('b8d4e8'))
    for dx, dy, r in parts:
        disc(cv, x + dx * s, y + dy * s, r * s, hexc('ffffff'))


def bricks(cv, x0, y0, x1, y1, base, mortar, bw=8, bh=4, light=None):
    cv.rect(x0, y0, x1, y1, base)
    for y in range(y0, y1 + 1):
        row = (y - y0) // bh
        if (y - y0) % bh == 0:
            for x in range(x0, x1 + 1): cv.p(x, y, mortar)
            continue
        off = (bw // 2) * (row % 2)
        for x in range(x0, x1 + 1):
            if (x - x0 + off) % bw == 0: cv.p(x, y, mortar)
            elif light and (x - x0 + off) % bw == 1 and (y - y0) % bh == 1: cv.p(x, y, light)


def window_lit(cv, x, y, w, h, glow=False, arch=False, shutters=True):
    frame = P['dark_wood_d']
    cv.rect(x - 1, y - 1, x + w, y + h, frame)
    glass_top = hexc('ffd880') if glow else hexc('8ab8d8')
    glass_bot = hexc('e89a3a') if glow else hexc('d8eef8')
    gradient(cv, x, y, x + w - 1, y + h - 1, glass_top, glass_bot)
    if arch:
        for i in range(w // 2):
            for xx in (x + i, x + w - 1 - i):
                for yy in range(y, y + max(0, w // 2 - i - 1) // 1):
                    if (i - w / 2) ** 2 + (yy - y - w / 2) ** 2 > (w / 2) ** 2:
                        cv.p(xx, yy, (0, 0, 0, 0))
    cv.rect(x + w // 2, y, x + w // 2, y + h - 1, frame)
    cv.rect(x, y + h // 2, x + w - 1, y + h // 2, frame)
    cv.rect(x - 2, y + h, x + w + 1, y + h + 1, P['wood_l'])
    if shutters:
        cv.rect(x - 4, y - 1, x - 2, y + h, P['green'])
        cv.rect(x + w + 1, y - 1, x + w + 3, y + h, P['green'])
        cv.rect(x - 4, y + h // 2, x - 2, y + h // 2, P['green_d'])
        cv.rect(x + w + 1, y + h // 2, x + w + 3, y + h // 2, P['green_d'])


def roof_tiles(cv, x0, y0, x1, y1, base, dark, light):
    """기와 지붕 (사각 영역, 위에서 아래로 겹친 비늘)"""
    cv.rect(x0, y0, x1, y1, base)
    for y in range(y0, y1 + 1, 3):
        off = 2 if ((y - y0) // 3) % 2 else 0
        for x in range(x0, x1 + 1):
            if (x - x0 + off) % 4 == 0: cv.p(x, y + 1, dark); cv.p(x, y + 2, dark)
            cv.p(x, y, light if (x - x0 + off) % 4 == 2 else base)
        for x in range(x0, x1 + 1): cv.p(x, y + 2, shade(base, 0.85) if (x + off) % 4 else dark)


def gable_roof(cv, cx, top, base_y, half_w, base, dark, light, eave=None):
    """박공 지붕 (삼각형)"""
    for y in range(top, base_y + 1):
        t = (y - top) / max(1, base_y - top)
        w = int(half_w * t)
        for x in range(cx - w, cx + w + 1):
            row = (y - top) // 3
            off = 2 if row % 2 else 0
            c = base
            if (y - top) % 3 == 2: c = dark
            elif (x + off) % 4 == 0: c = dark
            elif (x + off) % 4 == 2 and (y - top) % 3 == 0: c = light
            cv.p(x, y, c)
    if eave:
        cv.rect(cx - half_w - 2, base_y + 1, cx + half_w + 2, base_y + 2, eave)


# ─── 거리 배경 ───────────────────────────────────────────────

def town_bg():
    random.seed(7)
    cv = Canvas(W, H)
    gradient(cv, 0, 0, W - 1, 150, hexc('4a8ad0'), hexc('cfe6f2'))
    # 해
    disc(cv, 400, 40, 14, hexc('fff4c8'))
    disc(cv, 400, 40, 10, hexc('fffbe8'))
    for x, y, s in ((40, 30, 1), (150, 55, 1), (300, 25, 1), (440, 80, 1)):
        cloud(cv, x, y, s)
    # 먼 산
    for x in range(W):
        h = 118 + int(10 * math.sin(x * 0.03) + 6 * math.sin(x * 0.071 + 1))
        for y in range(h, 160): cv.p(x, y, mix(hexc('8aa8c8'), hexc('a8c0d8'), (y - h) / 40))
    # 언덕 위 성
    for x in range(W):
        h = 140 + int(6 * math.sin(x * 0.02 + 2))
        for y in range(h, 170): cv.p(x, y, hexc('7a9a78'))
    cx = 120
    castle = hexc('8a94a8'); castle_d = hexc('6a7488')
    cv.rect(cx - 30, 100, cx + 30, 140, castle)
    for tx, th in ((-34, 80), (-12, 72), (12, 76), (34, 86)):
        cv.rect(cx + tx - 6, th, cx + tx + 6, 140, castle if tx < 0 else castle_d)
        for k in range(-6, 7, 4): cv.rect(cx + tx + k, th - 3, cx + tx + k + 1, th - 1, castle)
        # 뾰족 지붕
        for i in range(10):
            cv.rect(cx + tx - 6 + i * 6 // 10, th - 4 - i, cx + tx + 6 - i * 6 // 10, th - 4 - i, hexc('5a6a8a'))
        cv.rect(cx + tx, th - 18, cx + tx, th - 14, P['iron_d'])
        cv.rect(cx + tx + 1, th - 18, cx + tx + 4, th - 16, P['red'])
    for k in range(-26, 27, 6): cv.rect(cx + k, 97, cx + k + 2, 99, castle)
    cv.rect(cx - 4, 124, cx + 4, 140, castle_d)
    # 뒷줄 집들 (지붕 실루엣)
    x = -10
    while x < W + 10:
        w = random.randint(26, 40)
        h = random.randint(28, 44)
        base = 192
        wall = random.choice([hexc('d8c8a8'), hexc('c8b494'), hexc('e0d0b0')])
        roofc = random.choice([hexc('a8503a'), hexc('8a4a3a'), hexc('6a5a7a'), hexc('5a6a4a')])
        cv.rect(x, base - h, x + w, base, shade(wall, 0.92))
        # 나무 기둥
        for bx in range(x, x + w + 1, 10): cv.rect(bx, base - h, bx, base, shade(P['dark_wood'], 1.1))
        cv.rect(x, base - h + h // 2, x + w, base - h + h // 2, shade(P['dark_wood'], 1.1))
        # 작은 창
        for wx in range(x + 4, x + w - 4, 10):
            cv.rect(wx, base - h + 6, wx + 3, base - h + 10, hexc('5a6a8a'))
        gable_roof(cv, x + w // 2, base - h - w // 2, base - h, w // 2 + 3, roofc, shade(roofc, 0.75), shade(roofc, 1.2))
        x += w + random.randint(-2, 4)
    # 보도
    cv.rect(0, STREET_Y - 8, W, STREET_Y, hexc('a49a8a'))
    for x in range(0, W, 12): cv.rect(x, STREET_Y - 8, x, STREET_Y, hexc('8a8070'))
    cv.rect(0, STREET_Y - 8, W, STREET_Y - 8, hexc('c8bca8'))
    # 돌길 (아래로 갈수록 돌이 커짐)
    y = STREET_Y + 1
    row = 0
    while y < H:
        bh = 4 + row // 2
        bw = 10 + row * 2
        off = (bw // 2) * (row % 2)
        for yy in range(y, min(H, y + bh)):
            for x in range(W):
                edge = (yy == y) or ((x + off) % bw == 0)
                tone = ((x + off) // bw + row) % 3
                base = [hexc('8a7e70'), hexc('968a7a'), hexc('7e7466')][tone]
                cv.p(x, yy, hexc('5e564c') if edge else base)
                if not edge and (x + off) % bw == 1 and yy == y + 1: cv.p(x, yy, shade(base, 1.15))
        y += bh
        row += 1
    # 가로등 (건물 사이)
    for lx in (125, 240, 355):
        cv.rect(lx - 3, STREET_Y - 2, lx + 3, STREET_Y, P['iron_d'])
        cv.rect(lx - 1, STREET_Y - 46, lx, STREET_Y - 2, P['iron'])
        cv.rect(lx - 4, STREET_Y - 54, lx + 3, STREET_Y - 47, P['iron_d'])
        cv.rect(lx - 3, STREET_Y - 53, lx + 2, STREET_Y - 48, hexc('ffe0a0'))
        cv.rect(lx - 5, STREET_Y - 56, lx + 4, STREET_Y - 55, P['iron_d'])
    # 축제 깃발 줄
    for y0, ph in ((14, 0), (22, 1.5)):
        prev = None
        for x in range(0, W + 1):
            y = y0 + int(10 * (1 - math.cos((x / W) * math.pi * 4 + ph)) / 2)
            if prev: cv.line(prev, (x, y), hexc('5a4030'))
            prev = (x, y)
            if x % 16 == 8:
                col = [P['red'], P['gold'], P['blue_l'], P['green_l']][(x // 16 + int(ph)) % 4]
                for i in range(6):
                    cv.rect(x - 3 + i // 2, y + 1 + i, x + 3 - i // 2, y + 1 + i, col)
    save(cv, 'town_bg')


# ─── 건물 정면 (100x116) ─────────────────────────────────────

FW, FH = 100, 116


def workshop():
    """건축 공방: 돌 1층(대장간 입구, 화로 불빛) + 나무 2층 + 굴뚝 + 망치·톱 간판"""
    cv = Canvas(FW, FH)
    base = FH - 1
    # 굴뚝 (지붕보다 먼저)
    bricks(cv, 70, 10, 81, 40, hexc('8a4a3a'), hexc('5a2a20'), bw=6, bh=3)
    cv.rect(68, 8, 83, 10, hexc('5a2a20'))
    # 2층 (나무 골조)
    cv.rect(8, 38, 91, 70, P['plaster'])
    for x in (8, 30, 50, 70, 91): cv.rect(x, 38, x + 1, 70, P['dark_wood'])
    cv.rect(8, 38, 91, 39, P['dark_wood']); cv.rect(8, 54, 91, 55, P['dark_wood'])
    for x0 in (8, 70):
        cv.line((x0, 55), (x0 + 21, 70), P['dark_wood'])
    window_lit(cv, 36, 42, 10, 10)
    window_lit(cv, 56, 42, 10, 10)
    # 지붕
    gable_roof(cv, 50, 6, 38, 50, hexc('5a4a5a'), hexc('3a2e3a'), hexc('7a6a7a'), eave=P['dark_wood_d'])
    # 1층 (돌)
    bricks(cv, 6, 70, 93, base, P['stone'], P['stone_d'], bw=10, bh=5, light=P['stone_l'])
    # 대장간 입구 (불빛)
    cv.rect(26, 80, 62, base, hexc('2a140a'))
    for y in range(80, base + 1):
        t = (y - 80) / (base - 80)
        for x in range(28, 61):
            d = math.hypot(x - 44, (y - 104) * 1.4)
            if d < 22: cv.p(x, y, mix(hexc('ff9a3a'), hexc('3a1a0a'), d / 22))
    # 모루 실루엣
    cv.rect(38, 100, 50, 103, hexc('1a1010')); cv.rect(41, 104, 47, 110, hexc('1a1010')); cv.rect(36, 99, 40, 101, hexc('1a1010'))
    # 입구 위 나무 보
    cv.rect(24, 76, 64, 79, P['wood'])
    cv.rect(24, 76, 64, 76, P['wood_l'])
    # 간판 (망치와 톱)
    cv.rect(66, 74, 66, 78, P['iron_d']); cv.rect(84, 74, 84, 78, P['iron_d'])
    cv.rect(64, 78, 86, 92, P['wood_d']); cv.rect(65, 79, 85, 91, P['wood_l'])
    cv.rect(69, 82, 70, 89, P['wood_d']); cv.rect(66, 81, 73, 83, P['iron'])
    cv.rect(75, 86, 83, 87, P['iron_l']);
    for x in range(75, 84, 2): cv.p(x, 88, P['iron'])
    cv.rect(82, 83, 83, 86, P['wood_d'])
    # 목재 더미
    for i, y in enumerate((base - 3, base - 7)):
        for x0 in range(4 + i * 2, 22, 6):
            disc(cv, x0 + 2, y, 2, P['wood_l']); cv.p(x0 + 2, y, P['wood_d'])
    cv.outline()
    save(cv, 'facade_workshop')


def temple():
    """신전: 흰 돌, 기둥, 삼각 박공, 아치 문, 장미창(스테인드글라스), 종탑"""
    cv = Canvas(FW, FH)
    base = FH - 1
    stone, stone_d, stone_l = hexc('d8d0c0'), hexc('a89e8a'), hexc('f0eadc')
    # 종탑
    cv.rect(42, 4, 58, 30, stone)
    cv.rect(46, 10, 54, 22, hexc('3a3040'))
    disc(cv, 50, 17, 3, P['gold']); cv.rect(48, 19, 52, 20, P['gold_d'])
    for i in range(8): cv.rect(42 + i, 4 - i // 2, 58 - i, 4 - i // 2, hexc('5a6a8a'))
    cv.rect(49, -4 + 4, 51, 0 + 2, P['gold'])
    # 본당
    cv.rect(8, 40, 91, base, stone)
    for y in range(44, base, 6): cv.line((8, y), (91, y), shade(stone, 0.93))
    # 박공
    for y in range(26, 41):
        w = int((y - 26) / 14 * 46)
        cv.rect(50 - w, y, 50 + w, y, stone_l)
    cv.line((4, 41), (50, 24), stone_d); cv.line((96, 41), (50, 24), stone_d)
    cv.rect(4, 40, 96, 42, stone_d)
    # 장미창
    disc(cv, 50, 34, 5, P['gold_d'])
    cols = [P['red_l'], P['blue_l'], P['gold_l'], P['green_l'], P['purple'], P['blue_l']]
    for i in range(6):
        ang = i * math.pi / 3
        disc(cv, 50 + 2.4 * math.cos(ang), 34 + 2.4 * math.sin(ang), 1.6, cols[i])
    cv.p(50, 34, P['paper_l'])
    # 기둥
    for x in (12, 24, 70, 82):
        cv.rect(x, 46, x + 6, base - 4, stone_l)
        cv.rect(x + 5, 46, x + 6, base - 4, stone_d)
        cv.rect(x - 1, 44, x + 7, 46, stone_d); cv.rect(x - 1, base - 4, x + 7, base - 2, stone_d)
    # 아치 문
    cv.rect(36, 66, 64, base, hexc('6a4a3a'))
    for x in range(36, 65):
        for y in range(56, 66):
            if (x - 50) ** 2 + ((y - 66) * 1.4) ** 2 <= 14 ** 2: cv.p(x, y, hexc('6a4a3a'))
    for x in range(38, 63, 4): cv.rect(x, 62, x, base, hexc('4a3020'))
    cv.rect(49, 60, 50, base, hexc('3a2010'))
    cv.p(46, 86, P['gold']); cv.p(53, 86, P['gold'])
    # 문 위 금 태양
    for d in range(-3, 4): cv.p(50 + d, 52, P['gold']); cv.p(50, 52 + d, P['gold'])
    disc(cv, 50, 52, 1.5, P['gold_l'])
    # 계단
    cv.rect(30, base - 2, 70, base, stone_d); cv.rect(32, base - 4, 68, base - 3, stone)
    # 높은 창 (스테인드)
    for x in (31, 63):
        for y in range(52, 80):
            c = cols[(y // 4 + x) % 6]
            cv.rect(x, y, x + 4, y, c if y % 4 else P['iron_d'])
        cv.rect(x - 1, 50, x + 5, 51, stone_d)
    cv.outline()
    save(cv, 'facade_temple')


def clinic():
    """치유소: 흰 회벽 목조 주택, 녹색 십자 간판, 화분, 약초"""
    cv = Canvas(FW, FH)
    base = FH - 1
    # 지붕 (초록 기와)
    gable_roof(cv, 50, 8, 44, 52, hexc('4a7a4a'), hexc('2e5030'), hexc('6aa060'), eave=P['dark_wood_d'])
    # 굴뚝 작은 것
    cv.rect(24, 18, 30, 34, hexc('a89080'))
    # 벽
    cv.rect(8, 44, 91, base, hexc('f0e8d8'))
    for x in (8, 28, 50, 72, 91): cv.rect(x, 44, x + 1, base, P['dark_wood'])
    cv.rect(8, 44, 91, 45, P['dark_wood']); cv.rect(8, 74, 91, 75, P['dark_wood'])
    cv.line((28, 75), (50, 44), P['dark_wood']); cv.line((72, 75), (50, 44), P['dark_wood'])
    # 창 (꽃 화분)
    for x in (13, 76):
        window_lit(cv, x, 52, 10, 12, shutters=False)
        cv.rect(x - 2, 66, x + 11, 69, P['wood'])
        for k in range(0, 12, 3):
            cv.p(x + k, 65, [P['red_l'], hexc('f0a0c0'), P['gold_l'], P['red_l']][k // 3])
            cv.p(x + k + 1, 65, P['green'])
    # 문 (둥근 윗부분, 연두색)
    cv.rect(40, 84, 60, base, hexc('5a8a5a'))
    for x in range(40, 61):
        for y in range(78, 84):
            if (x - 50) ** 2 + ((y - 84) * 1.6) ** 2 <= 10 ** 2: cv.p(x, y, hexc('5a8a5a'))
    cv.rect(49, 80, 50, base, hexc('3a6a3a'))
    cv.p(46, 98, P['gold']); cv.p(53, 98, P['gold'])
    # 녹색 십자 간판
    cv.rect(41, 50, 59, 70, P['paper_l'])
    cv.rect(41, 50, 59, 50, P['wood_l']); cv.rect(41, 70, 59, 70, P['wood_d'])
    cv.rect(48, 53, 52, 67, P['green']); cv.rect(44, 58, 56, 62, P['green'])
    cv.rect(49, 54, 51, 66, P['green_l'])
    # 약초 다발 (처마)
    for x in (16, 26, 74, 84):
        cv.rect(x, 46, x, 48, P['wood_d'])
        for k in range(5): cv.p(x - 2 + k, 49 + (k % 2), P['green_l'] if k % 2 else P['green'])
    # 화분
    for x in (32, 66):
        cv.rect(x, base - 6, x + 6, base, hexc('b0603a'))
        cv.rect(x - 1, base - 7, x + 7, base - 6, hexc('8a4a2a'))
        for k in range(7): cv.p(x + k, base - 8 - (k % 3), P['green'] if k % 2 else P['green_l'])
    cv.outline()
    save(cv, 'facade_clinic')


def shop():
    """상점: 줄무늬 차양, 진열대(사과·물약·자루), 동전 간판, 상자"""
    cv = Canvas(FW, FH)
    base = FH - 1
    # 건물 뒤판 + 지붕
    gable_roof(cv, 50, 16, 46, 50, hexc('a8503a'), hexc('6a2a20'), hexc('d06a4a'), eave=P['dark_wood_d'])
    cv.rect(8, 46, 91, base, hexc('e8d8b8'))
    for x in (8, 91): cv.rect(x, 46, x + 1, base, P['dark_wood'])
    window_lit(cv, 44, 52, 12, 10, shutters=False)
    # 동전 간판
    disc(cv, 50, 36, 7, P['gold_d']); disc(cv, 50, 36, 6, P['gold']); disc(cv, 49, 35, 3, P['gold_l'])
    cv.rect(49, 33, 51, 39, P['gold_d'])
    # 차양 (빨강/흰 줄)
    for y in range(66, 78):
        sl = (y - 66) // 1
        for x in range(4 - sl // 2, 96 + sl // 2):
            c = P['red'] if ((x - 4) // 8) % 2 == 0 else P['paper_l']
            if y > 74: c = shade(c, 0.85)
            cv.p(x, y, c)
    for x in range(0, 101, 8):
        for k in range(3): cv.p(x + 3 - k + k, 78 + k, P['red_d'] if (x // 8) % 2 == 0 else P['paper_d'])
    # 진열대
    cv.rect(10, 92, 90, 100, P['wood'])
    cv.rect(10, 92, 90, 92, P['wood_l'])
    cv.rect(10, 100, 90, base, P['wood_d'])
    for x in range(10, 91, 12): cv.rect(x, 101, x, base, P['dark_wood_d'])
    # 물건: 사과 바구니, 물약, 자루, 빵
    for i in range(6):
        disc(cv, 18 + (i % 3) * 4, 89 - (i // 3) * 3, 2, P['red_l'] if i % 2 else P['red'])
    for i, c in enumerate((P['blue_l'], P['purple'], P['green_l'])):
        x = 40 + i * 6
        cv.rect(x, 84, x + 3, 91, c); cv.rect(x + 1, 82, x + 2, 83, P['paper']); cv.p(x, 85, shade(c, 1.4))
    cv.rect(62, 82, 72, 91, hexc('c8a870')); cv.rect(64, 80, 70, 82, hexc('a08850'))
    cv.rect(76, 87, 86, 91, hexc('d89a5a')); cv.rect(77, 86, 85, 86, hexc('e8b878'))
    # 기둥 (차양 받침)
    for x in (6, 92): cv.rect(x, 78, x + 1, base, P['dark_wood'])
    cv.outline()
    save(cv, 'facade_shop')


def smoke():
    """굴뚝 연기 (4프레임, 16x40)"""
    for f in range(4):
        cv = Canvas(16, 40)
        for k in range(4):
            t = (k + f / 4) / 4
            y = 36 - t * 34
            x = 8 + math.sin(t * 5 + 1) * 3 * t
            r = 2 + t * 4
            a = int(200 * (1 - t))
            disc(cv, x, y, r, (220, 220, 225, a))
        save(cv, f'smoke_{f}')


def forge_glow():
    """대장간 입구 불빛 (4프레임, 정면 입구 크기 37x35, 반투명)"""
    for f in range(4):
        cv = Canvas(37, 35)
        strength = [0.9, 1.0, 0.85, 0.95][f]
        for y in range(35):
            for x in range(37):
                d = math.hypot(x - 18, (y - 24) * 1.4)
                if d < 20:
                    cv.p(x, y, (255, 170, 70, int(110 * strength * (1 - d / 20))))
        for k in range(3):
            fx = 12 + k * 6
            flame(cv, fx, 30, f + k, size=2)
        save(cv, f'forge_glow_{f}')


# ─── 실내 배경 (480x270, 창 뒤에 어둡게 깔림) ────────────────────

def interior_temple():
    cv = Canvas(W, H)
    gradient(cv, 0, 0, W - 1, H - 1, hexc('3a3448'), hexc('1e1a28'))
    # 기둥 줄과 아치
    for i, x in enumerate(range(20, W, 80)):
        cv.rect(x, 30, x + 18, 210, hexc('8a8496'))
        cv.rect(x + 14, 30, x + 18, 210, hexc('6a6478'))
        cv.rect(x - 4, 26, x + 22, 30, hexc('a8a2b4'))
        cv.rect(x - 4, 206, x + 22, 212, hexc('6a6478'))
    # 스테인드글라스 높은 창 (기둥 사이)
    cols = [P['red_l'], P['blue_l'], P['gold_l'], P['green_l'], P['purple']]
    for x in range(48, W - 40, 80):
        for y in range(40, 140):
            for xx in range(x, x + 28):
                arch = y > 52 or (xx - x - 14) ** 2 + ((y - 52) * 1.2) ** 2 <= 14 ** 2
                if not arch: continue
                c = cols[((xx - x) // 7 + (y - 40) // 10) % 5]
                if (xx - x) % 7 == 0 or (y - 40) % 10 == 0: c = hexc('1a1420')
                cv.p(xx, y, shade(c, 0.85))
        # 빛줄기
        for y in range(140, 230):
            for xx in range(x + 2 + (y - 140) // 3, x + 26 + (y - 140) // 3):
                cv.p(xx, y, (255, 230, 170, 26))
    # 바닥과 붉은 길
    cv.rect(0, 212, W, H, hexc('4a4458'))
    for y in range(212, H, 6): cv.line((0, y), (W, y), hexc('3a3448'))
    for y in range(212, H):
        w = 30 + (y - 212) * 2
        cv.rect(240 - w, y, 240 + w, y, P['red_d'] if (y % 6) else P['red'])
    # 제단
    cv.rect(200, 170, 280, 212, hexc('d8d0c0'))
    cv.rect(196, 166, 284, 172, P['gold'])
    cv.rect(200, 190, 280, 192, P['gold_d'])
    for d in range(-10, 11):
        cv.p(240 + d, 140, P['gold']); cv.p(240, 140 + d, P['gold'])
    disc(cv, 240, 140, 5, P['gold_l'])
    for x in (206, 222, 258, 274):
        cv.rect(x, 152, x + 2, 166, P['paper_l'])
        flame(cv, x + 1, 150, x % 4, size=2)
    # 의자
    for row, y in enumerate((222, 240, 258)):
        for side in (-1, 1):
            x0 = 240 + side * (60 + row * 30)
            cv.rect(min(x0, x0 + side * 120), y, max(x0, x0 + side * 120), y + 6, P['wood'])
            cv.rect(min(x0, x0 + side * 120), y, max(x0, x0 + side * 120), y, P['wood_l'])
    save(cv, 'interior_temple')


def interior_clinic():
    cv = Canvas(W, H)
    # 나무 벽
    cv.rect(0, 0, W, 200, hexc('8a6a4a'))
    for x in range(0, W, 12): cv.rect(x, 0, x, 200, hexc('6a4a30'))
    cv.rect(0, 120, W, 124, hexc('5a3a20'))
    cv.rect(0, 200, W, H, hexc('a08060'))
    for y in range(204, H, 8): cv.line((0, y), (W, y), hexc('8a6a4a'))
    # 창 (햇빛)
    for x in (60, 380):
        cv.rect(x - 2, 30, x + 42, 92, P['dark_wood_d'])
        gradient(cv, x, 32, x + 40, 90, hexc('cfe8f8'), hexc('fff4dc'))
        cv.rect(x + 20, 32, x + 20, 90, P['dark_wood_d']); cv.rect(x, 60, x + 40, 60, P['dark_wood_d'])
        for y in range(92, 200):
            for xx in range(x + (y - 92) // 2, x + 40 + (y - 92) // 2):
                cv.p(xx, y, (255, 245, 210, 22))
    # 약 선반
    for sy in (40, 70, 100):
        cv.rect(170, sy, 310, sy + 3, P['wood_l'])
        for k in range(16):
            x = 174 + k * 8
            c = [P['red_l'], P['green_l'], P['blue_l'], P['purple'], P['gold']][(k + sy) % 5]
            cv.rect(x, sy - 9, x + 4, sy - 1, c); cv.rect(x + 1, sy - 11, x + 3, sy - 10, P['paper'])
            cv.p(x, sy - 8, shade(c, 1.4))
    # 매달린 약초
    for x in range(20, W, 34):
        cv.rect(x, 0, x, 14, P['wood_d'])
        for k in range(7): cv.p(x - 3 + k, 15 + (k % 3), P['green_l'] if k % 2 else P['green'])
    # 침대 세 개
    for bx in (40, 190, 340):
        cv.rect(bx, 180, bx + 100, 214, P['wood'])
        cv.rect(bx + 2, 174, bx + 98, 196, P['paper_l'])
        cv.rect(bx + 2, 186, bx + 98, 200, hexc('8ab0d8'))
        cv.rect(bx + 4, 170, bx + 26, 180, hexc('fff8f0'))
        cv.rect(bx, 160, bx + 4, 214, P['wood_d']); cv.rect(bx + 96, 176, bx + 100, 214, P['wood_d'])
    # 약 탁자와 절구
    cv.rect(150, 214, 180, 240, P['wood_d']); cv.rect(146, 210, 184, 214, P['wood_l'])
    cv.rect(158, 202, 172, 210, P['stone_l']); cv.rect(164, 196, 166, 204, P['wood_l'])
    save(cv, 'interior_clinic')


def interior_workshop():
    cv = Canvas(W, H)
    # 벽돌 벽
    bricks(cv, 0, 0, W - 1, 200, hexc('7a4a3a'), hexc('4a2a20'), bw=16, bh=8, light=hexc('9a6a5a'))
    cv.rect(0, 200, W, H, hexc('5a4a3a'))
    for y in range(204, H, 10): cv.line((0, y), (W, y), hexc('4a3a2a'))
    # 화로 (가운데 왼쪽)
    cv.rect(60, 90, 180, 200, hexc('4a4248'))
    bricks(cv, 60, 90, 180, 200, hexc('5a5258'), hexc('3a3238'), bw=12, bh=6)
    cv.rect(80, 130, 160, 200, hexc('1a0c06'))
    for y in range(130, 200):
        for x in range(80, 161):
            d = math.hypot(x - 120, (y - 190) * 1.2)
            if d < 50: cv.p(x, y, mix(hexc('ffb050'), hexc('3a1408'), d / 50))
    for k in range(5): flame(cv, 96 + k * 12, 196, k, size=3)
    cv.rect(100, 40, 140, 90, hexc('4a4248'))
    # 화로 불빛 번짐
    for y in range(80, H):
        for x in range(0, 300):
            d = math.hypot(x - 120, y - 180)
            if d < 140: cv.p(x, y, (255, 150, 60, int(40 * (1 - d / 140))))
    # 모루
    cv.rect(210, 196, 260, 204, hexc('2e3038')); cv.rect(222, 204, 248, 226, hexc('2e3038'))
    cv.rect(200, 192, 216, 198, hexc('3e4048')); cv.rect(212, 190, 262, 194, hexc('545866'))
    cv.rect(216, 226, 254, 232, hexc('3a2a20'))
    # 연장 걸이판
    cv.rect(300, 40, 460, 140, P['wood'])
    for x in range(300, 461, 10): cv.rect(x, 40, x, 140, P['wood_d'])
    tools = [(312, 'hammer'), (340, 'saw'), (372, 'hammer'), (400, 'tongs'), (430, 'saw')]
    for x, kind in tools:
        if kind == 'hammer':
            cv.rect(x + 3, 56, x + 4, 100, P['wood_l']); cv.rect(x - 2, 52, x + 9, 60, P['iron_l'])
        elif kind == 'saw':
            cv.rect(x, 60, x + 18, 72, P['iron_l']); cv.rect(x + 18, 58, x + 24, 74, P['wood_l'])
            for k in range(0, 18, 2): cv.p(x + k, 73, P['iron'])
        else:
            cv.line((x, 56), (x + 8, 110), P['iron']); cv.line((x + 8, 56), (x, 110), P['iron'])
    # 작업대와 설계도
    cv.rect(280, 196, 470, 204, P['wood_l']); cv.rect(290, 204, 300, 250, P['wood_d']); cv.rect(450, 204, 460, 250, P['wood_d'])
    cv.rect(310, 186, 380, 196, hexc('6a8ab8'));
    for k in range(3): cv.line((314, 188 + k * 3), (376, 188 + k * 3), hexc('d8e8f8'))
    cv.rect(400, 180, 440, 196, P['wood_ll']); cv.rect(396, 190, 446, 196, P['wood_l'])
    # 목재 더미
    for i in range(4):
        for k in range(5):
            disc(cv, 20 + k * 10, 250 - i * 9, 4, P['wood_l']); cv.p(20 + k * 10, 250 - i * 9, P['wood_d'])
    save(cv, 'interior_workshop')


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    town_bg()
    workshop(); temple(); clinic(); shop()
    smoke(); forge_glow()
    interior_temple(); interior_clinic(); interior_workshop()
    # 정면 그림은 가운데 아래 피벗
    with open(os.path.join(OUT, 'pivots.txt'), 'w', encoding='utf-8') as f:
        f.write('# 이름 피벗x 피벗y — Tools/town_art_gen.py가 만듦\n')
        for n in ('facade_workshop', 'facade_temple', 'facade_clinic', 'facade_shop'):
            f.write(f'{n} 0.50000 0.00000\n')
    print('ok')
