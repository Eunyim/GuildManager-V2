# 던전 모닥불 도트 그림 생성기 (돌 화덕 + 장작, 불꽃 프레임, 바닥 불빛, 앉을 통나무·그루터기, 불티)
# 실행: python Tools/campfire_art_gen.py  → Assets/Art/Dungeon/Campfire/*.png
# PixelArtImporter가 PPU 64로 가져옴 (바닥 한 칸 = 64x32px와 같은 크기감)
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(__file__))
from pixel_lib import Canvas, shade, mix, hexc, write_pivots

OUT = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'Art', 'Dungeon', 'Campfire')
pivots = {}


def save(cv, name, pivot_px):
    cv.save(os.path.join(OUT, name + '.png'))
    pivots[name] = (pivot_px[0] / cv.w, 1 - pivot_px[1] / cv.h)


def ellipse(cv, cx, cy, rx, ry, c):
    for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
        for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
            if ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1.0:
                cv.p(x, y, c)


# ─── 화덕 (돌 둘레 + 엇갈린 장작 + 숯) ───────────────────────────

def base():
    rnd = random.Random(2)
    W, H = 60, 34
    cv = Canvas(W, H)
    cx, cy = W / 2, 20
    ellipse(cv, cx, cy + 2, 27, 11, (0, 0, 0, 80))                 # 그림자
    ellipse(cv, cx, cy, 19, 8, hexc('2a1c18'))                     # 재·숯 바닥
    for _ in range(40):
        a = rnd.random() * math.tau
        r = rnd.random() ** 0.5
        x, y = cx + math.cos(a) * r * 16, cy + math.sin(a) * r * 6
        cv.p(x, y, rnd.choice([hexc('4a3a34'), hexc('3a2a24'), hexc('e0602a'), hexc('ffa040')]))
    # 장작 4개 (가운데로 모임)
    logs = [(-15, 3, 4, -3), (15, 3, -4, -3), (-9, -4, 3, 0), (9, -4, -3, 0)]
    for x0, y0, x1, y1 in logs:
        for t in range(0, 21):
            k = t / 20
            x = cx + x0 + (x1 - x0) * k
            y = cy + y0 + (y1 - y0) * k
            for w in (-2, -1, 0, 1):
                col = hexc('8a5a32') if w < 0 else hexc('5e3a20') if w == 0 else hexc('3e2614')
                cv.p(x, y + w, col)
        ellipse(cv, cx + x0, cy + y0 - 0.5, 2, 2, hexc('c8945a'))    # 나이테 단면
        cv.p(cx + x0, cy + y0 - 0.5, hexc('8a5a32'))
    # 둘레 돌 (뒤쪽은 작고 어둡게, 앞쪽은 크고 밝게)
    n = 12
    stones = []
    for i in range(n):
        a = i / n * math.tau
        x = cx + math.cos(a) * 21
        y = cy + math.sin(a) * 9
        stones.append((y, x, a))
    for y, x, a in sorted(stones):
        front = math.sin(a) > 0
        rx, ry = (4.2, 3.2) if front else (3.4, 2.4)
        base_c = hexc('8a8490') if front else hexc('5e5864')
        ellipse(cv, x, y, rx, ry, shade(base_c, 0.7))
        ellipse(cv, x - 0.5, y - 0.6, rx - 1, ry - 1, base_c)
        cv.p(x - 1, y - 1.5, shade(base_c, 1.3))
    cv.outline(hexc('140c0a'))
    save(cv, 'campfire_base', (cx, cy + 2))


# ─── 불꽃 (6프레임, 밑동이 기준점) ───────────────────────────────

def flames():
    W, H = 30, 44
    for f in range(6):
        cv = Canvas(W, H)
        ph = f / 6 * math.tau
        bx, by = W / 2, H - 4
        for layer, (scale, col) in enumerate(((1.0, hexc('c8401a')), (0.78, hexc('f08a24')), (0.55, hexc('ffd860')), (0.3, hexc('fff6c0')))):
            hgt = (34 + 4 * math.sin(ph + layer)) * scale
            wid = 11 * scale + 1
            for i in range(int(hgt)):
                t = i / max(1, hgt)
                sway = math.sin(ph * 1.0 + t * 3.2) * 2.6 * t
                w = wid * (1 - t) ** 0.8 * (1 + 0.25 * math.sin(t * 9 + ph * 2))
                for dx in range(-int(w), int(w) + 1):
                    cv.p(bx + dx + sway, by - i, col)
        # 떨어져 나간 불똥
        for k in range(3):
            t = ((f / 6) + k / 3) % 1
            cv.p(bx + math.sin(k * 2.1 + t * 6) * 5, by - 30 - t * 10, hexc('ffd860') if t < 0.6 else hexc('f08a24'))
        save(cv, f'campfire_flame_{f}', (bx, by + 1))


# ─── 바닥 불빛 (타원, 부드러운 알파) ─────────────────────────────

def glow():
    W, H = 240, 120
    cv = Canvas(W, H)
    cx, cy = W / 2, H / 2
    for y in range(H):
        for x in range(W):
            d = math.hypot((x - cx) / (W / 2), (y - cy) / (H / 2))
            if d < 1:
                a = int(150 * (1 - d) ** 1.8)
                if a > 1: cv.p(x, y, (255, 170, 80, a))
    save(cv, 'campfire_glow', (cx, cy))


# ─── 앉을 자리: 눕힌 통나무(위·아래 자리) / 그루터기(옆 자리) ─────

def log_seat():
    W, H = 34, 14
    cv = Canvas(W, H)
    ellipse(cv, W / 2, H - 3, 15, 3, (0, 0, 0, 70))
    for x in range(4, W - 4):
        for y in range(3, 10):
            t = (y - 3) / 6
            col = mix(hexc('a06a3c'), hexc('4e2e18'), t)
            if (x * 7 + y * 3) % 11 == 0: col = shade(col, 0.82)
            cv.p(x, y, col)
        cv.p(x, 3, hexc('c48a52'))
    for ex in (4, W - 5):
        ellipse(cv, ex, 6.5, 2.5, 3.5, hexc('c8945a'))
        ellipse(cv, ex, 6.5, 1.2, 1.8, hexc('8a5a32'))
    cv.outline(hexc('1a0f0a'))
    save(cv, 'campfire_seat_log', (W / 2, 7))


def stump_seat():
    W, H = 22, 18
    cv = Canvas(W, H)
    ellipse(cv, W / 2, H - 3, 9, 3, (0, 0, 0, 70))
    for y in range(6, 14):
        for x in range(4, W - 4):
            col = hexc('6a4228') if x > W / 2 else hexc('8a5a32')
            if x in (4, W - 5): col = hexc('3e2614')
            if (x + y * 2) % 5 == 0: col = shade(col, 0.85)
            cv.p(x, y, col)
    ellipse(cv, W / 2, 6, 7, 3, hexc('c8945a'))
    ellipse(cv, W / 2, 6, 4.5, 1.8, hexc('a87444'))
    ellipse(cv, W / 2, 6, 2, 0.8, hexc('c8945a'))
    cv.outline(hexc('1a0f0a'))
    save(cv, 'campfire_seat_stump', (W / 2, 9))


def bits():
    cv = Canvas(3, 3)
    cv.p(1, 1, hexc('fff0a0'))
    for d in ((0, 1), (2, 1), (1, 0), (1, 2)): cv.p(*d, (255, 160, 60, 160))
    save(cv, 'campfire_ember', (1.5, 1.5))
    cv = Canvas(10, 10)
    ellipse(cv, 5, 5, 4.5, 4.5, (110, 100, 110, 140))
    ellipse(cv, 4, 4, 2.5, 2.5, (150, 140, 150, 140))
    save(cv, 'campfire_smoke', (5, 5))


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    base(); flames(); glow(); log_seat(); stump_seat(); bits()
    write_pivots(OUT, pivots)
    print('ok')
