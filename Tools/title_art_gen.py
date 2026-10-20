# 타이틀 화면 도트 그림 생성기 (황혼의 길드 마을)
# 실행: python Tools/title_art_gen.py  → Assets/Art/Title/*.png
# 화면(1920x1080)에 정확히 4배로 보이도록 화면 크기는 480x270.
# 시차(패럴랙스) 레이어는 마우스·인트로 이동 여유분을 두고 512x288 (가운데 정렬, 좌우 16·위아래 9px 여유)
import math
import os
import random
import sys

from PIL import Image, ImageFont

sys.path.insert(0, os.path.dirname(__file__))
from pixel_lib import P, Canvas, shade, mix, hexc

ROOT = os.path.join(os.path.dirname(__file__), '..')
OUT = os.path.join(ROOT, 'Assets', 'Art', 'Title')
FONT = os.path.join(ROOT, 'Assets', 'Fonts', 'DungGeunMo.ttf')
LW, LH = 512, 288
OX, OY = 16, 9            # 레이어 좌표 = 화면 좌표 + (OX, OY)

BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]

# 황혼 팔레트
SKY = [(0.00, hexc('15122e')), (0.30, hexc('2c2352')), (0.55, hexc('5a3368')),
       (0.75, hexc('a5486a')), (0.90, hexc('e2784e')), (1.00, hexc('f7b866'))]
RIM = hexc('ffb070')       # 노을빛 테두리
WIN = hexc('ffd27a')       # 불 켜진 창
WIN_D = hexc('e08a32')


def save(cv, name):
    cv.save(os.path.join(OUT, name + '.png'))


def lerp_stops(stops, t):
    for i in range(len(stops) - 1):
        t0, c0 = stops[i]
        t1, c1 = stops[i + 1]
        if t <= t1:
            return mix(c0, c1, (t - t0) / max(1e-6, t1 - t0))
    return stops[-1][1]


def dither_gradient(cv, x0, y0, x1, y1, stops, bands):
    """세로 그라데이션을 bands단계 색으로 나누고 4x4 베이어 디더로 경계를 섞음 (도트 느낌)"""
    pal = [lerp_stops(stops, i / (bands - 1)) for i in range(bands)]
    for y in range(y0, y1 + 1):
        u = (y - y0) / max(1, y1 - y0) * (bands - 1)
        i = min(int(u), bands - 2)
        f = u - i
        for x in range(x0, x1 + 1):
            c = pal[i + 1] if f * 16 > BAYER[y % 4][x % 4] + 0.5 else pal[i]
            cv.p(x, y, c)


def disc(cv, cx, cy, r, c):
    for y in range(int(cy - r) - 1, int(cy + r) + 2):
        for x in range(int(cx - r) - 1, int(cx + r) + 2):
            if (x - cx) ** 2 + (y - cy) ** 2 <= r * r:
                cv.p(x, y, c)


def ridge(seed, n, rough, base, amp):
    """중점 변위로 만든 능선 높이 배열 (길이 n)"""
    rnd = random.Random(seed)
    size = 1
    while size < n: size *= 2
    h = [0.0] * (size + 1)
    h[0], h[size] = rnd.uniform(-1, 1), rnd.uniform(-1, 1)
    step, scale = size, 1.0
    while step > 1:
        half = step // 2
        for i in range(half, size, step):
            h[i] = (h[i - half] + h[i + half]) / 2 + rnd.uniform(-1, 1) * scale
        step, scale = half, scale * rough
    return [base - h[i] * amp for i in range(n)]


def fill_below(cv, heights, color, rim=None, rim_side=None, snow=None):
    for x, top in enumerate(heights):
        top = int(round(top))
        for y in range(top, cv.h):
            cv.p(x, y, color)
        if rim is not None:
            # 왼쪽(노을 쪽)을 향한 비탈에만 테두리 빛
            left = heights[x - 1] if x > 0 else top
            if left >= top or rim_side == 'all':
                cv.p(x, top, rim)
        if snow is not None and top < snow[0]:
            depth = int((snow[0] - top) * 0.35) + 1
            for y in range(top, top + depth):
                cv.p(x, y, snow[1] if (x + y) % 5 else shade(snow[1], 0.85))


# ─── 1. 하늘 ────────────────────────────────────────────────

def sky():
    rnd = random.Random(11)
    cv = Canvas(LW, LH)
    dither_gradient(cv, 0, 0, LW - 1, 200, SKY, 14)
    cv.rect(0, 201, LW - 1, LH - 1, SKY[-1][1])

    # 별 (위쪽만, 밝기 여러 단계)
    for _ in range(170):
        x, y = rnd.randrange(LW), rnd.randrange(0, 120)
        b = rnd.random()
        fade = 1 - y / 130
        a = int(255 * fade * (0.35 + 0.65 * b))
        cv.p(x, y, (255, 240, 220, a))
        if b > 0.93 and y < 90:
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                cv.p(x + dx, y + dy, (255, 230, 200, a // 3))

    # 달 + 은은한 빛무리 (디더)
    mx, my, mr = 418, 52, 15
    for r in range(mr + 22, mr, -1):
        a = int(70 * (1 - (r - mr) / 22) ** 1.6)
        for y in range(my - r, my + r + 1):
            for x in range(mx - r, mx + r + 1):
                d = math.hypot(x - mx, y - my)
                if r - 1 < d <= r and BAYER[y % 4][x % 4] < 9:
                    cv.p(x, y, (255, 226, 190, a))
    disc(cv, mx, my, mr, hexc('fff0d2'))
    # 달 그림자 (오른쪽 아래)와 크레이터
    for y in range(my - mr, my + mr + 1):
        for x in range(mx - mr, mx + mr + 1):
            if (x - mx) ** 2 + (y - my) ** 2 <= mr * mr and (x - mx - 5) ** 2 + (y - my - 4) ** 2 > (mr + 1) ** 2:
                pass
            if (x - mx) ** 2 + (y - my) ** 2 <= mr * mr and (x - mx) + (y - my) > mr * 0.7:
                cv.p(x, y, hexc('e8cfae'))
    for cx, cy, r in ((-5, -4, 3), (4, 3, 2.2), (-2, 6, 1.6), (6, -6, 1.4)):
        disc(cv, mx + cx, my + cy, r, hexc('e6d0b0'))
        cv.p(mx + cx - 1, my + cy - 1, hexc('fff8e6'))

    # 지평선 노을 띠 (가는 구름 줄)
    for y, w, c in ((150, 0.6, hexc('f19a6a', 160)), (162, 0.8, hexc('ffc68a', 140)), (172, 0.5, hexc('ffdca0', 120))):
        x = 0
        while x < LW:
            ln = rnd.randint(18, 60)
            if rnd.random() < w:
                cv.rect(x, y, x + ln, y, c)
                cv.rect(x + 4, y + 1, x + ln - 6, y + 1, shade(c, 0.9))
            x += ln + rnd.randint(4, 30)
    save(cv, 'title_sky')


# ─── 2. 먼 산 ────────────────────────────────────────────────

def mountains():
    cv = Canvas(LW, LH)
    far = ridge(3, LW, 0.55, 150, 34)
    fill_below(cv, far, hexc('6a4c7e'), rim=hexc('c47a86'), snow=(130, hexc('e8cce0')))
    near = ridge(8, LW, 0.5, 172, 22)
    fill_below(cv, near, hexc('4a3a68'), rim=hexc('9a6280'))
    # 산 아래 옅은 안개
    for y in range(176, 200):
        a = int(90 * (1 - abs(y - 186) / 12))
        for x in range(LW):
            if a > 0 and cv.get(x, y)[3] and BAYER[y % 4][x % 4] < 8:
                cv.p(x, y, (240, 150, 140, a))
    save(cv, 'title_mountains')


# ─── 3. 언덕 위의 성 + 숲 ───────────────────────────────────

def tower(cv, x, top, w, base, body, roof, lit):
    cv.rect(x, top, x + w - 1, base, body)
    # 성가퀴
    for i in range(0, w, 3):
        cv.rect(x + i, top - 2, x + i + 1, top - 1, body)
    if roof:
        for i in range(w // 2 + 2):
            cv.rect(x + w // 2 - i, top - 2 - (w // 2 + 2 - i) * 2, x + w // 2 + i - (1 - w % 2), top - 3 - (w // 2 + 2 - i) * 2 + 2, roof)
    for wy in lit:
        cv.p(x + w // 2, wy, WIN); cv.p(x + w // 2, wy + 1, WIN_D)


def pine(cv, x, base, h, c, rim=None):
    for i in range(h):
        w = int((i / h) * h * 0.38) + 1
        if i % 3 == 2: w -= 1
        cv.rect(x - w, base - h + i, x + w, base - h + i, c)
        if rim and i % 3 == 0: cv.p(x - w, base - h + i, rim)
    cv.rect(x, base, x, base + 2, shade(c, 0.8))


def castle():
    rnd = random.Random(21)
    cv = Canvas(LW, LH)
    hills = [186 - 14 * math.exp(-((x - 380) / 70) ** 2) - 6 * math.sin(x / 37) for x in range(LW)]
    body = hexc('2f2748')
    # 성 (오른쪽 가운데 언덕 위)
    cx, gy = 380, 176
    cv.rect(cx - 34, gy - 22, cx + 34, gy, body)                    # 성벽
    for i in range(cx - 34, cx + 35, 4): cv.rect(i, gy - 24, i + 1, gy - 23, body)
    tower(cv, cx - 42, gy - 40, 9, gy, body, body, [gy - 32, gy - 20])
    tower(cv, cx + 34, gy - 38, 9, gy, body, body, [gy - 30])
    tower(cv, cx - 12, gy - 60, 12, gy, body, body, [gy - 52, gy - 40, gy - 28])  # 본성 탑
    tower(cv, cx + 6, gy - 48, 8, gy, body, body, [gy - 40])
    # 깃대
    cv.rect(cx - 6, gy - 82, cx - 6, gy - 72, body)
    cv.rect(cx - 5, gy - 82, cx - 1, gy - 79, hexc('a8322a'))
    for wx in range(cx - 28, cx + 30, 9):
        cv.p(wx, gy - 12, WIN); cv.p(wx, gy - 11, WIN_D)
    fill_below(cv, hills, hexc('2a2440'), rim=hexc('7a4a62'))
    # 숲 (언덕을 따라 뾰족한 침엽수)
    for x in range(0, LW, 5):
        if 340 < x < 422: continue
        h = rnd.randint(9, 16)
        pine(cv, x + rnd.randint(-1, 1), int(hills[min(x, LW - 1)]) + 6, h, hexc('231d38'), rim=hexc('5a3a56'))
    # 마을 불빛 (언덕 기슭)
    for _ in range(14):
        x = rnd.randint(230, 470)
        if 346 < x < 416: continue
        y = int(hills[x]) + rnd.randint(8, 14)
        cv.p(x, y, WIN)
        cv.p(x + 1, y, WIN_D)
    save(cv, 'title_castle')


# ─── 4. 길드 회관 (왼쪽 언덕) + 오른쪽 큰 나무 ───────────────────

GX, GBASE = 54, 214   # 회관 왼쪽 끝, 바닥 (레이어 좌표)


def timber_wall(cv, x0, y0, x1, y1):
    cv.rect(x0, y0, x1, y1, hexc('8e7a68'))           # 회벽 (황혼 그늘)
    beam = hexc('3a2418')
    cv.rect(x0, y0, x1, y0 + 1, beam); cv.rect(x0, y1 - 1, x1, y1, beam)
    for x in range(x0, x1 + 1, 14): cv.rect(x, y0, x + 1, y1, beam)
    for x in range(x0, x1 - 13, 14):        # X자 버팀목
        cv.line((x + 2, y0 + 2), (x + 13, y1 - 2), beam)
        cv.line((x + 13, y0 + 2), (x + 2, y1 - 2), beam)
    # 노을빛이 닿는 왼쪽 가장자리
    cv.rect(x0, y0, x0, y1, shade(hexc('8e7a68'), 1.25))


def lit_window(cv, x, y, w, h, glow_cv):
    cv.rect(x - 1, y - 1, x + w, y + h, hexc('2a1810'))
    for yy in range(y, y + h):
        c = mix(WIN, WIN_D, (yy - y) / max(1, h - 1))
        cv.rect(x, yy, x + w - 1, yy, c)
    cv.rect(x + w // 2, y, x + w // 2, y + h - 1, hexc('5a3420'))
    cv.rect(x, y + h // 2, x + w - 1, y + h // 2, hexc('5a3420'))
    cv.rect(x - 2, y + h, x + w + 1, y + h, hexc('6a4430'))
    # 창 불빛 무리 (깜빡임용 별도 그림)
    cx, cy = x + w / 2, y + h / 2
    R = max(w, h) + 7
    for yy in range(int(cy - R), int(cy + R) + 1):
        for xx in range(int(cx - R), int(cx + R) + 1):
            d = math.hypot((xx - cx) * 0.9, yy - cy)
            if d < R:
                a = int(120 * (1 - d / R) ** 2)
                if a > 6 and BAYER[yy % 4][xx % 4] * 8 < a + 20:
                    glow_cv.p(xx, yy, (255, 200, 110, a))


def roof(cv, x0, x1, top, base, overhang=6):
    """기와 박공 지붕 (정면에서 본 경사면): x0~x1 처마, top 용마루"""
    dark, mid, light = hexc('3a2030'), hexc('5c2c36'), hexc('84404a')
    for y in range(top, base + 1):
        t = (y - top) / max(1, base - top)
        l = int(x0 - overhang * t)
        r = int(x1 + overhang * t)
        for x in range(l, r + 1):
            row = (y - top) // 3
            off = 2 if row % 2 else 0
            c = mid
            if (y - top) % 3 == 2: c = dark
            elif (x + off) % 4 == 0: c = dark
            elif (x + off) % 4 == 2 and (y - top) % 3 == 0: c = light
            cv.p(x, y, c)
        cv.p(l, y, RIM if y % 2 else shade(RIM, 0.8))   # 노을 테두리
    cv.rect(x0 - overhang - 1, base + 1, x1 + overhang + 1, base + 2, hexc('2a1810'))
    cv.rect(x0 - 2, top - 1, x1 + 2, top, hexc('2a1810'))


def guild():
    cv = Canvas(LW, LH)
    glow = Canvas(LW, LH)
    rnd = random.Random(5)

    # 언덕
    hill = [GBASE + 2 - 18 * math.exp(-((x - 120) / 120) ** 2) + max(0, (x - 220) * 0.25) for x in range(LW)]
    hill = [min(h, LH) for h in hill]

    gx, gb = GX, GBASE - 10
    W1 = 132                       # 회관 폭
    # 1층 돌 기단
    stone = hexc('5a5468')
    cv.rect(gx, gb - 30, gx + W1, gb, stone)
    for y in range(gb - 30, gb + 1, 5):
        off = 4 if ((y - gb) // 5) % 2 else 0
        cv.rect(gx, y, gx + W1, y, shade(stone, 0.75))
        for x in range(gx + off, gx + W1, 9): cv.rect(x, y, x, y + 4, shade(stone, 0.75))
    cv.rect(gx, gb - 30, gx, gb, shade(stone, 1.3))
    # 2층 목조
    timber_wall(cv, gx - 4, gb - 70, gx + W1 + 4, gb - 31)
    cv.rect(gx - 7, gb - 32, gx + W1 + 7, gb - 30, hexc('3a2418'))   # 2층 바닥 띠
    # 지붕
    roof(cv, gx - 4, gx + W1 + 4, gb - 104, gb - 71, overhang=10)
    # 지붕창(도머)
    dx = gx + W1 // 2 - 9
    cv.rect(dx, gb - 94, dx + 18, gb - 76, hexc('8e7a68'))
    for i in range(12):
        cv.rect(dx - 3 + i, gb - 95 - i // 1 + 0, dx + 21 - i, gb - 95 - i // 1, hexc('5c2c36'))
    lit_window(cv, dx + 5, gb - 90, 8, 10, glow)
    # 굴뚝 (연기는 런타임)
    cv.rect(gx + W1 - 26, gb - 118, gx + W1 - 16, gb - 96, hexc('4a4458'))
    cv.rect(gx + W1 - 28, gb - 120, gx + W1 - 14, gb - 117, hexc('3a3446'))
    for y in range(gb - 116, gb - 96, 3): cv.rect(gx + W1 - 26, y, gx + W1 - 16, y, hexc('3a3446'))
    # 깃대 (깃발은 런타임)
    cv.rect(gx + 22, gb - 132, gx + 22, gb - 98, hexc('2a1810'))
    cv.p(gx + 22, gb - 133, hexc('f2d488'))
    # 2층 창 4개
    for i, wx in enumerate((gx + 10, gx + 40, gx + 82, gx + 112)):
        lit_window(cv, wx, gb - 62, 10, 14, glow)
    # 1층: 큰 아치 문 + 창 2개
    door_x = gx + W1 // 2 - 9
    cv.rect(door_x - 3, gb - 27, door_x + 20, gb, hexc('3a2418'))
    for yy in range(gb - 24, gb + 1):
        cv.rect(door_x, yy, door_x + 17, yy, mix(hexc('ffcc70'), hexc('c86a28'), (yy - gb + 24) / 24))
    for i in range(6):   # 아치 모서리 다듬기
        cv.rect(door_x + i * 0, gb - 24, door_x + 2 - i // 3, gb - 24 + (2 - i // 3), hexc('3a2418'))
        cv.rect(door_x + 15 + i // 3, gb - 24, door_x + 17, gb - 24 + (2 - i // 3), hexc('3a2418'))
    cv.rect(door_x + 8, gb - 24, door_x + 9, gb, hexc('8a4a20'))       # 열린 문 사이
    lit_window(cv, gx + 14, gb - 22, 12, 10, glow)
    lit_window(cv, gx + W1 - 26, gb - 22, 12, 10, glow)
    # 문 불빛 바닥으로
    for yy in range(gb + 1, gb + 14):
        w = 10 + (yy - gb)
        a = int(110 * (1 - (yy - gb) / 14))
        for xx in range(door_x + 9 - w, door_x + 9 + w):
            if BAYER[yy % 4][xx % 4] * 7 < a: glow.p(xx, yy, (255, 190, 100, a))
    # 간판 (방패 문장) — 문 위에 매달림
    sx, sy = door_x - 2, gb - 44
    cv.rect(sx + 3, sy - 4, sx + 3, sy, hexc('2a1810')); cv.rect(sx + 18, sy - 4, sx + 18, sy, hexc('2a1810'))
    cv.rect(sx, sy, sx + 21, sy + 10, hexc('6a4430'))
    cv.rect(sx, sy, sx + 21, sy, hexc('a06a3c'))
    cv.rect(sx, sy + 10, sx + 21, sy + 10, hexc('2a1810'))
    for i in range(5):                         # 작은 방패
        cv.rect(sx + 8 + i // 2, sy + 2 + i, sx + 13 - i // 2, sy + 2 + i, hexc('d4a84a'))
    cv.rect(sx + 10, sy + 3, sx + 11, sy + 5, hexc('a8322a'))
    # 등불 걸이 (불꽃은 런타임)
    for lx in (door_x - 8, door_x + 25):
        cv.rect(lx, gb - 20, lx + 3, gb - 20, hexc('2a1810'))
        cv.rect(lx, gb - 19, lx, gb - 14, hexc('2a1810')); cv.rect(lx + 3, gb - 19, lx + 3, gb - 14, hexc('2a1810'))
        cv.rect(lx, gb - 13, lx + 3, gb - 13, hexc('2a1810'))
        glow_disc(glow, lx + 1.5, gb - 16, 12, 120)
    # 통·상자
    for bx in (gx - 14, gx - 6):
        cv.rect(bx, gb - 9, bx + 6, gb, hexc('6a4430'))
        cv.rect(bx, gb - 7, bx + 6, gb - 7, hexc('2e3038')); cv.rect(bx, gb - 2, bx + 6, gb - 2, hexc('2e3038'))
        cv.rect(bx, gb - 9, bx, gb, hexc('a06a3c'))
    cv.rect(gx + W1 + 4, gb - 8, gx + W1 + 14, gb, hexc('7a4a2a'))
    cv.line((gx + W1 + 4, gb - 8), (gx + W1 + 14, gb), hexc('4a2c18'))

    # 언덕 (회관 앞을 덮지 않게 회관 뒤에 그리면 안 되므로 따로: 회관 바닥 아래만)
    land = Canvas(LW, LH)
    grass, grass_d = hexc('2e3a36'), hexc('222c2a')
    for x in range(LW):
        top = int(hill[x])
        for y in range(top, LH):
            land.p(x, y, grass if (x * 7 + y * 3) % 11 else grass_d)
        land.p(x, top, hexc('8a6a50') if hill[x - 1 if x else 0] >= hill[x] else hexc('4a5248'))
        if (x * 13) % 7 == 0: land.p(x, top - 1, hexc('3a4a40'))
    land.paste(cv, 0, 0)
    cv = land

    # 오른쪽 큰 나무 (전경 쪽 실루엣)
    tree(cv, 470, 232, 74)
    tree(cv, 432, 236, 50)
    # 왼쪽 끝 나무
    tree(cv, 12, 222, 58)

    save(cv, 'title_guild')
    save(glow, 'title_guild_glow')


def glow_disc(cv, cx, cy, R, amax):
    for yy in range(int(cy - R), int(cy + R) + 1):
        for xx in range(int(cx - R), int(cx + R) + 1):
            d = math.hypot(xx - cx, yy - cy)
            if d < R:
                a = int(amax * (1 - d / R) ** 2)
                if a > 6 and BAYER[yy % 4][xx % 4] * 8 < a + 20:
                    cv.p(xx, yy, (255, 196, 110, a))


def tree(cv, x, base, h):
    """잎이 무성한 활엽수 실루엣 (노을 테두리)"""
    rnd = random.Random(x * 31 + h)
    trunk = hexc('241a20')
    cv.rect(x - 3, base - h // 2, x + 3, base, trunk)
    cv.rect(x - 5, base - 2, x + 5, base, trunk)
    leaf, leaf_d = hexc('26283a'), hexc('1c1e2e')
    blobs = []
    for _ in range(14):
        bx = x + rnd.randint(-h // 3, h // 3)
        by = base - h // 2 - rnd.randint(0, h // 2)
        r = rnd.randint(h // 7, h // 4)
        blobs.append((bx, by, r))
    for bx, by, r in blobs:
        disc(cv, bx, by, r, leaf)
    for bx, by, r in blobs:   # 아래쪽 그늘
        for yy in range(by, by + r + 1):
            for xx in range(bx - r, bx + r + 1):
                if (xx - bx) ** 2 + (yy - by) ** 2 <= r * r and (yy - by) > r * 0.4 and BAYER[yy % 4][xx % 4] < 10:
                    cv.p(xx, yy, leaf_d)
    # 왼쪽 위 테두리 빛
    src = cv.im.copy().load()
    for yy in range(base - h - 4, base - h // 3):
        for xx in range(x - h // 2 - 4, x + h // 2 + 4):
            if 0 <= xx < cv.w and 0 <= yy < cv.h and src[xx, yy][:3] == leaf[:3]:
                if (xx - 1 < 0 or src[xx - 1, yy][3] == 0) or (yy - 1 < 0 or src[xx, yy - 1][3] == 0):
                    cv.p(xx, yy, hexc('6a4a5a'))


# ─── 5. 길 (모험가가 걷는 곳) ────────────────────────────────

ROAD_Y = 252   # 모험가 발 높이 (레이어 좌표)


def road():
    rnd = random.Random(9)
    cv = Canvas(LW, LH)
    top = [ROAD_Y - 12 + 3 * math.sin(x / 41) for x in range(LW)]
    grass, grass_l = hexc('2a3430'), hexc('3a463e')
    for x in range(LW):
        for y in range(int(top[x]) - 10, LH):
            cv.p(x, y, grass)
    # 흙길
    dirt, dirt_d, dirt_l = hexc('6a4a3a'), hexc('4e3428'), hexc('8a6450')
    for x in range(LW):
        a, b = int(top[x]), int(top[x]) + 22
        for y in range(a, b + 1):
            c = dirt
            if y in (a, a + 1): c = dirt_l
            elif y == b: c = dirt_d
            elif (y - a) in (7, 15) and (x // 3) % 4 != 0: c = dirt_d     # 바퀴 자국
            cv.p(x, y, c)
    for _ in range(140):   # 돌멩이
        x = rnd.randrange(LW)
        y = int(top[x]) + rnd.randint(2, 20)
        cv.p(x, y, dirt_l); cv.p(x + 1, y, dirt_d)
    for _ in range(260):   # 풀 포기
        x = rnd.randrange(LW)
        y = int(top[x]) - rnd.randint(0, 9) if rnd.random() < 0.6 else int(top[x]) + 24 + rnd.randint(0, 30)
        for i in range(rnd.randint(2, 4)):
            cv.p(x + rnd.randint(-1, 1), y - i, grass_l if i else hexc('202a26'))
    # 작은 꽃
    for _ in range(40):
        x = rnd.randrange(LW)
        y = int(top[x]) - rnd.randint(1, 8)
        cv.p(x, y, rnd.choice([hexc('e8a0b8'), hexc('f2d488'), hexc('b8a0f0')]))
    save(cv, 'title_road')


# ─── 6. 맨 앞 덤불·울타리·가로등 ────────────────────────────

def front():
    rnd = random.Random(13)
    cv = Canvas(LW, LH)
    dark, dark2 = hexc('141a18'), hexc('1c2420')
    # 아래쪽 모서리 덤불
    for cx, cy, s in ((10, 290, 40), (60, 296, 28), (470, 292, 44), (510, 284, 30), (250, 302, 22)):
        for _ in range(26):
            bx = cx + rnd.randint(-s, s)
            by = cy - rnd.randint(0, s // 2)
            disc(cv, bx, by, rnd.randint(5, 10), dark)
        for _ in range(10):
            bx = cx + rnd.randint(-s, s)
            by = cy - rnd.randint(s // 3, s // 2 + 4)
            disc(cv, bx, by, rnd.randint(3, 6), dark2)
    # 울타리 (왼쪽)
    for i in range(6):
        px = 28 + i * 16
        cv.rect(px, 262 - i // 2, px + 2, 282, hexc('2a1e18'))
        cv.p(px + 1, 261 - i // 2, hexc('6a4a3a'))
    cv.rect(26, 268, 118, 269, hexc('2a1e18')); cv.rect(26, 275, 118, 276, hexc('2a1e18'))
    # 가로등 (오른쪽, 불꽃은 런타임)
    lx = 408
    cv.rect(lx, 196, lx + 2, 290, hexc('1e1a20'))
    cv.rect(lx - 3, 288, lx + 5, 290, hexc('1e1a20'))
    cv.rect(lx - 4, 194, lx + 6, 195, hexc('1e1a20'))
    cv.rect(lx - 3, 182, lx + 5, 182, hexc('1e1a20'))
    cv.rect(lx - 3, 182, lx - 3, 193, hexc('1e1a20')); cv.rect(lx + 5, 182, lx + 5, 193, hexc('1e1a20'))
    cv.rect(lx - 2, 183, lx + 4, 193, (255, 210, 120, 110))
    cv.rect(lx - 1, 179, lx + 3, 181, hexc('1e1a20')); cv.p(lx + 1, 178, hexc('1e1a20'))
    save(cv, 'title_front')

    glow = Canvas(LW, LH)
    glow_disc(glow, lx + 1, 188, 30, 110)
    save(glow, 'title_front_glow')


# ─── 로고 ───────────────────────────────────────────────────

def text_mask(text, size):
    font = ImageFont.truetype(FONT, size)
    l, t, r, b = font.getbbox(text)
    im = Image.new('L', (r - l + 2, b - t + 2), 0)
    from PIL import ImageDraw
    d = ImageDraw.Draw(im)
    d.fontmode = '1'   # 안티앨리어싱 없이
    d.text((1 - l, 1 - t), text, font=font, fill=255)
    return im


def logo():
    mask = text_mask('길드 매니저', 32)
    mw, mh = mask.size
    pad = 6
    W, H = mw + pad * 2, mh + pad * 2 + 3
    cv = Canvas(W, H)
    m = mask.load()
    inside = lambda x, y: 0 <= x < mw and 0 <= y < mh and m[x, y] > 127

    # 그림자 (아래로 3px)
    for y in range(mh):
        for x in range(mw):
            if inside(x, y):
                for dx in range(-2, 3):
                    for dy in range(-2, 3):
                        cv.p(x + pad + dx, y + pad + dy + 3, (20, 8, 16, 255))
    # 바깥 테두리 2px
    for y in range(mh):
        for x in range(mw):
            if inside(x, y):
                for dx in range(-2, 3):
                    for dy in range(-2, 3):
                        if abs(dx) + abs(dy) <= 3:
                            cv.p(x + pad + dx, y + pad + dy, hexc('2a120a'))
    # 금빛 채움 (위 밝게 → 아래 진하게, 가운데 띠 반사)
    stops = [(0, hexc('fff6c8')), (0.35, hexc('ffd860')), (0.55, hexc('e8a030')), (0.62, hexc('ffe08a')), (1, hexc('a85a18'))]
    for y in range(mh):
        c = lerp_stops(stops, y / (mh - 1))
        for x in range(mw):
            if inside(x, y):
                cc = c
                if not inside(x, y - 1): cc = hexc('fffbe8')                 # 윗변 반짝임
                elif not inside(x, y + 1): cc = hexc('7a3a10')               # 아랫변 그늘
                elif not inside(x - 1, y): cc = shade(c, 1.08)
                elif not inside(x + 1, y): cc = shade(c, 0.82)
                cv.p(x + pad, y + pad, cc)
    save(cv, 'title_logo')
    return W, H


def subtitle():
    mask = text_mask('GUILD  MANAGER', 16)
    mw, mh = mask.size
    m = mask.load()
    W = mw + 60
    H = mh + 4
    cv = Canvas(W, H)
    ox = 30
    for y in range(mh):
        for x in range(mw):
            if m[x, y] > 127:
                cv.p(x + ox + 1, y + 2 + 1, hexc('2a120a'))
                cv.p(x + ox, y + 2, hexc('f2e2c0'))
    # 양옆 장식선 ─◆
    cy = H // 2 + 1
    for side in (-1, 1):
        x0 = ox - 4 if side < 0 else ox + mw + 3
        for i in range(22):
            x = x0 + side * i
            cv.p(x, cy, hexc('d4a84a') if i < 18 else hexc('8a6420'))
        for d in range(3):
            xx = x0 + side * 24
            cv.rect(xx - d + 0, cy - (2 - d), xx + d, cy - (2 - d), hexc('f2d488'))
            cv.rect(xx - d, cy + (2 - d), xx + d, cy + (2 - d), hexc('d4a84a'))
    save(cv, 'title_subtitle')


def emblem():
    """로고 위 문장: 방패 뒤로 교차한 두 검 (칼끝이 위)"""
    S = 72
    cv = Canvas(S, S)
    c = S // 2
    steel, steel_l, steel_d = hexc('d8dce6'), hexc('ffffff'), hexc('7a7e90')

    def sword(sx):
        # sx=+1: 왼쪽 아래 손잡이 → 오른쪽 위 칼끝,  sx=-1: 반대
        hx, hy = c - sx * 22, c + 24       # 손잡이 끝
        tx, ty = c + sx * 26, c - 30       # 칼끝
        n = 64
        for i in range(n + 1):
            t = i / n
            x = hx + (tx - hx) * t
            y = hy + (ty - hy) * t
            if t < 0.12:      # 폼멜·손잡이
                cv.rect(x - 1, y - 1, x + 1, y + 1, hexc('5a3018') if t > 0.04 else hexc('f2d488'))
            elif t < 0.17:    # 코등이 (칼과 수직)
                for k in range(-7, 8):
                    cv.p(x + k * 0.7, y + k * 0.7 * sx, hexc('d4a84a') if abs(k) < 6 else hexc('f2d488'))
            else:             # 칼날 3px
                w = 1 if t < 0.94 else 0
                cv.p(x, y, steel)
                if w:
                    cv.p(x - 1, y, steel_l if sx > 0 else steel_d)
                    cv.p(x + 1, y, steel_d if sx > 0 else steel_l)
    sword(1)
    sword(-1)
    # 방패
    top, bot, hw = c - 16, c + 20, 15
    def half(y, inset):
        t = (y - top) / (bot - top)
        w = hw - inset
        return w if t < 0.5 else int(w * math.cos((t - 0.5) / 0.5 * math.pi / 2) + 0.5)
    for y in range(top - 1, bot + 2):
        w = half(min(max(y, top), bot), -1)
        cv.rect(c - w, y, c + w, y, hexc('2a120a'))
    for y in range(top + 1, bot):
        w = half(y, 1)
        for x in range(c - w, c + w + 1):
            edge = abs(x - c) >= w - 1 or y <= top + 1
            col = hexc('e0b850') if edge else (hexc('b83a2e') if x < c else hexc('8e2820'))
            if not edge and y < top + 4: col = hexc('d85a46') if x < c else hexc('b03a2e')
            cv.p(x, y, col)
    # 가운데 금빛 별
    pts = []
    for i in range(10):
        a = -math.pi / 2 + i * math.pi / 5
        r = 7 if i % 2 == 0 else 3
        pts.append((c + math.cos(a) * r, c + 1 + math.sin(a) * r))
    cv.poly(pts, hexc('f2d488'))
    cv.p(c, c - 3, hexc('fffbe8')); cv.p(c - 1, c, hexc('fffbe8'))
    cv.outline(hexc('1a0a08'))
    save(cv, 'title_emblem')


# ─── 버튼 · 커서 ────────────────────────────────────────────

def button(name, face, face_l, border, press=False):
    W, H = 88, 18
    cv = Canvas(W, H)
    off = 1 if press else 0
    cv.rect(2, 2, W - 3, H - 1, hexc('1a0f0a'))               # 그림자/테두리
    cv.rect(1, 1 + off, W - 2, H - 3 + off, hexc('1a0f0a'))
    cv.rect(2, 2 + off, W - 3, H - 4 + off, face)
    for x in range(3, W - 3):                                   # 나뭇결
        if (x * 7) % 13 == 0: cv.rect(x, 4 + off, x + 5, 4 + off, shade(face, 0.85))
        if (x * 5) % 17 == 0: cv.rect(x, H - 7 + off, x + 4, H - 7 + off, shade(face, 0.85))
    cv.rect(2, 2 + off, W - 3, 2 + off, face_l)                # 윗변 밝게
    cv.rect(2, H - 4 + off, W - 3, H - 4 + off, shade(face, 0.7))
    # 쇠 모서리 장식
    for x0 in (2, W - 6):
        cv.rect(x0, 2 + off, x0 + 3, H - 4 + off, border)
        cv.p(x0 + 1, 4 + off, hexc('fff4d0')); cv.p(x0 + 1, H - 6 + off, hexc('fff4d0'))
        cv.rect(x0 + (3 if x0 == 2 else 0), 2 + off, x0 + (3 if x0 == 2 else 0), H - 4 + off, shade(border, 0.7))
    save(cv, name)


def cursor():
    """메뉴 커서: 오른쪽을 가리키는 작은 검"""
    cv = Canvas(14, 9)
    for x in range(5, 13):
        cv.p(x, 3, hexc('f0f2f8')); cv.p(x, 4, hexc('c8ccd8')); cv.p(x, 5, hexc('8a90a0'))
    cv.p(13, 4, hexc('f0f2f8'))
    cv.rect(4, 1, 4, 7, hexc('d4a84a'))
    cv.rect(1, 4, 3, 4, hexc('6a3a20'))
    cv.p(0, 4, hexc('f2d488'))
    cv.outline()
    save(cv, 'title_cursor')


# ─── 애니메이션 조각 ────────────────────────────────────────

def flag_frames():
    for f in range(4):
        cv = Canvas(26, 16)
        for x in range(22):
            phase = f * math.pi / 2
            dy = int(round(math.sin(x / 4.5 - phase) * (x / 22) * 2.4))
            shadeF = 0.8 + 0.25 * math.cos(x / 4.5 - phase)
            for y in range(10):
                if x > 16 and abs(y - 4.5) < (x - 16) * 0.9: continue   # 제비꼬리
                c = hexc('a8322a') if 2 < y < 7 else hexc('84241e')
                if 2 < y < 7 and 6 < x < 11: c = hexc('d4a84a')
                cv.p(x + 1, y + 2 + dy, shade(c, shadeF))
        cv.outline(hexc('1a0a08'))
        save(cv, f'title_flag_{f}')


def flame_frames():
    for f in range(4):
        cv = Canvas(7, 10)
        sway = [0, 1, 0, -1][f]
        h = [8, 9, 8, 7][f]
        for i in range(h):
            t = i / (h - 1)
            w = max(0, int(round((1 - t) * 2.4)))
            cx = 3 + (sway if t > 0.45 else 0)
            col = P['flame_ll'] if t < 0.3 else P['flame_l'] if t < 0.6 else P['flame']
            for dx in range(-w, w + 1):
                cv.p(cx + dx, 9 - i, col if abs(dx) < w or w == 0 else P['flame_d'])
        save(cv, f'title_flame_{f}')


def clouds():
    rnd = random.Random(4)
    for k, (w, h) in enumerate(((64, 16), (44, 12), (80, 18))):
        cv = Canvas(w, h)
        blobs = [(rnd.randint(6, w - 7), rnd.randint(h // 2, h - 5), rnd.randint(3, h // 2 - 1)) for _ in range(9)]
        for bx, by, r in blobs: disc(cv, bx, by, r, hexc('8a5a86'))           # 그늘 (윗면, 달빛 등진 쪽)
        for bx, by, r in blobs: disc(cv, bx, by + 2, r - 1, hexc('d47a72'))   # 노을 받은 아랫면
        for bx, by, r in blobs:
            if r > 3: disc(cv, bx - 1, by - 1, r - 3, hexc('a06a90'))
        cv.rect(4, h - 2, w - 5, h - 2, hexc('f0a070'))
        save(cv, f'title_cloud_{k}')


def small_bits():
    # 반짝이는 별 (십자)
    cv = Canvas(5, 5)
    cv.p(2, 2, hexc('ffffff'))
    for d in ((1, 2), (3, 2), (2, 1), (2, 3)): cv.p(*d, hexc('ffe8c8', 200))
    for d in ((0, 2), (4, 2), (2, 0), (2, 4)): cv.p(*d, hexc('ffd8a8', 90))
    save(cv, 'title_star')
    # 반딧불이
    cv = Canvas(5, 5)
    for y in range(5):
        for x in range(5):
            d = math.hypot(x - 2, y - 2)
            if d < 2.6: cv.p(x, y, (230, 255, 150, int(255 * max(0, 1 - d / 2.6) ** 1.5)))
    cv.p(2, 2, hexc('fbffd8'))
    save(cv, 'title_firefly')
    # 연기 한 덩이
    cv = Canvas(9, 9)
    disc(cv, 4, 4, 4, (120, 100, 130, 150))
    disc(cv, 3, 3, 2.5, (170, 140, 160, 150))
    save(cv, 'title_smoke')
    # 별똥별 (오른쪽 아래로 떨어지는 꼬리, 머리는 오른쪽)
    cv = Canvas(24, 8)
    for i in range(22):
        a = int(255 * (i / 21) ** 2)
        cv.p(i + 1, int(i * 0.3), (255, 240, 210, a))
    cv.p(23, 6, hexc('ffffff'))
    save(cv, 'title_meteor')
    # 로고 반짝임 띠 (흰색 사선)
    cv = Canvas(12, 48)
    for y in range(48):
        for x in range(12):
            xx = x - (47 - y) * 0.25 + 6
            if 0 <= xx < 5:
                cv.p(x, y, (255, 255, 240, 200 if 1 <= xx < 4 else 90))
    save(cv, 'title_shine')
    # 1픽셀 흰 그림 (페이드·막대용)
    cv = Canvas(4, 4)
    cv.rect(0, 0, 3, 3, hexc('ffffff'))
    save(cv, 'title_white')


def preview():
    """화면 구성 미리보기 (Docs/title_preview.png, 4배)"""
    names = ['title_sky', 'title_mountains', 'title_castle', 'title_guild', 'title_guild_glow', 'title_road', 'title_front', 'title_front_glow']
    base = Image.new('RGBA', (LW, LH), (0, 0, 0, 255))
    for n in names:
        base.alpha_composite(Image.open(os.path.join(OUT, n + '.png')))
    for n, x, y in (('title_cloud_0', 60, 70), ('title_cloud_2', 260, 40), ('title_cloud_1', 380, 100)):
        base.alpha_composite(Image.open(os.path.join(OUT, n + '.png')), (x, y))
    em = Image.open(os.path.join(OUT, 'title_emblem.png'))
    lg = Image.open(os.path.join(OUT, 'title_logo.png'))
    sb = Image.open(os.path.join(OUT, 'title_subtitle.png'))
    cx = LW // 2
    base.alpha_composite(em, (cx - em.width // 2, OY + 2))
    base.alpha_composite(lg, (cx - lg.width // 2, OY + 60))
    base.alpha_composite(sb, (cx - sb.width // 2, OY + 60 + lg.height + 2))
    bt = Image.open(os.path.join(OUT, 'title_button.png'))
    for i in range(3):
        base.alpha_composite(bt, (cx - bt.width // 2, OY + 142 + i * 22))
    base.alpha_composite(Image.open(os.path.join(OUT, 'title_flag_0.png')), (GX + 23, GBASE - 10 - 133))
    crop = base.crop((OX, OY, OX + 480, OY + 270)).resize((1920, 1080), Image.NEAREST)
    crop.save(os.path.join(ROOT, 'Docs', 'title_preview.png'))


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    sky(); mountains(); castle(); guild(); road(); front()
    logo(); subtitle(); emblem(); cursor()
    button('title_button', hexc('7a4a2a'), hexc('a06a3c'), hexc('8a90a0'))
    button('title_button_hover', hexc('9a6236'), hexc('d49a5a'), hexc('f2d488'))
    button('title_button_pressed', hexc('6a3e22'), hexc('8a5a32'), hexc('d4a84a'), press=True)
    button('title_button_disabled', hexc('4e4440'), hexc('6a5e58'), hexc('545866'))
    flag_frames(); flame_frames(); clouds(); small_bits()
    preview()
    print('ok')
