# 로비(길드 1층 홀·2층 여관) 도트 오브젝트 생성기
# 실행: python Tools/lobby_art_gen.py  → Assets/Art/Lobby/Pixel/*.png + pivots.txt
# 바닥 한 칸 = 64x32px, PPU 64. 피벗은 바닥에 닿는 점(발밑)으로 기록 → 오브젝트 위치 = 바닥 위 그 지점
import math
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from pixel_lib import P, Canvas, Iso, shade, mix, flame, save_with_pivot, write_pivots, hexc

OUT = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'Art', 'Lobby', 'Pixel')
pivots = {}
FRAMES = 4


def new(w, h, ox, oy):
    cv = Canvas(w, h)
    return cv, Iso(cv, ox, oy)


def foot(iso, a, b):
    """발밑(피벗) = 바닥 칸 중심"""
    return iso.pt(a, b, 0)


# ─── 소품 ───────────────────────────────────────────────────

def barrel(cv, cx, by, r, h, lying=False):
    """서 있는 나무통: (cx, by) = 밑면 중심, r = 반지름(px), h = 높이"""
    ry = max(2, r // 2)
    for x in range(-r, r + 1):
        t = (x + r) / (2 * r)
        col = P['wood_l'] if t < 0.3 else P['wood'] if t < 0.75 else P['wood_d']
        curve = int(round(math.sqrt(max(0, 1 - (x / r) ** 2)) * ry))
        for y in range(by - h + curve, by + curve + 1):
            cv.p(cx + x, y, col)
        # 쇠테 두 줄
        for hh in (int(h * 0.22), int(h * 0.78)):
            cv.p(cx + x, by - hh + curve, P['iron'] if t < 0.6 else P['iron_d'])
    # 윗면 타원
    for x in range(-r, r + 1):
        curve = int(round(math.sqrt(max(0, 1 - (x / r) ** 2)) * ry))
        for y in range(by - h - curve, by - h + curve + 1):
            cv.p(cx + x, y, P['wood_ll'] if y < by - h else P['wood_l'])
    cv.p(cx, by - h, P['wood_d'])


def mug(cv, x, y):
    """맥주잔 (x,y = 밑 왼쪽)"""
    cv.rect(x, y - 5, x + 3, y, P['wood_l'])
    cv.rect(x + 3, y - 5, x + 3, y, P['wood'])
    cv.rect(x, y - 7, x + 3, y - 6, P['foam'])
    cv.p(x + 4, y - 4, P['wood_d']); cv.p(x + 5, y - 3, P['wood_d']); cv.p(x + 4, y - 2, P['wood_d'])


def candle(cv, x, y, frame, h=6):
    cv.rect(x, y - h, x + 1, y, P['paper_l'])
    cv.p(x + 1, y - h + 1, P['paper_d'])
    cv.p(x, y - h - 1, P['iron_d'])
    flame(cv, x, y - h - 2, frame)
    # 바닥 촛대
    cv.rect(x - 1, y, x + 2, y + 1, P['gold'])


def book_open(iso, a0, b0, a1, b1, h):
    cv = iso.cv
    iso.box(a0, b0, a1, b1, h, h + 2, P['paper_l'], P['paper_d'], P['red'])
    am = (a0 + a1) / 2
    cv.line(iso.pt(am, b0, h + 2), iso.pt(am, b1, h + 2), P['paper_d'])
    for k in range(1, 4):
        bb = b0 + (b1 - b0) * k / 4
        cv.line(iso.pt(a0 + 0.05, bb, h + 2), iso.pt(am - 0.05, bb, h + 2), P['paper_d'])


# ─── 1층 ────────────────────────────────────────────────────

def reception():
    """접수 데스크: 두 칸짜리 카운터, 장부·깃펜·종·촛불 (촛불이 흔들림)"""
    for f in range(FRAMES):
        cv, iso = new(120, 112, 40, 104)
        iso.shadow(0, 0, 2, 1)
        iso.box(0, 0, 2, 1, 0, 24, P['wood_l'], P['wood'], P['wood_d'], edge=P['wood_l'])
        # 앞판 액자 무늬
        for a in (0.1, 0.95, 1.05, 1.9):
            cv.line(iso.pt(a, 0, 3), iso.pt(a, 0, 19), shade(P['wood_d'], 0.8))
        iso.face_lines_a(0, 0.1, 0.95, 3, 19, 16, shade(P['wood_d'], 0.8))
        iso.face_lines_a(0, 1.05, 1.9, 3, 19, 16, shade(P['wood_d'], 0.8))
        cv.line(iso.pt(0, 0, 3), iso.pt(2, 0, 3), shade(P['wood_d'], 0.8))
        cv.line(iso.pt(0, 0, 19), iso.pt(2, 0, 19), shade(P['wood_d'], 0.8))
        # 금 장식 줄
        cv.line(iso.pt(0, 0, 21), iso.pt(2, 0, 21), P['gold'])
        cv.line(iso.pt(0, 0, 21), iso.pt(0, 1, 21), P['gold_d'])
        # 상판 (살짝 튀어나옴)
        iso.box(-0.06, -0.06, 2.06, 1.06, 24, 27, P['wood_ll'], P['wood_l'], P['wood'], edge=P['wood_ll'])
        iso.top_lines_a(-0.06, -0.06, 2.06, 1.06, 27, 0.33, shade(P['wood_ll'], 0.88))
        # 금 명패
        px, py = iso.pt(1.0, 0, 12)
        cv.rect(px - 5, py - 3, px + 5, py + 1, P['gold'])
        cv.rect(px - 4, py - 2, px + 4, py, P['gold_l'])
        # 장부와 깃펜
        book_open(iso, 0.25, 0.25, 0.95, 0.75, 27)
        qx, qy = iso.pt(0.85, 0.35, 29)
        for i in range(7):
            cv.p(qx + i, qy - i, P['paper_l'] if i > 1 else P['iron_d'])
            if i > 2: cv.p(qx + i + 1, qy - i, P['paper'])
        # 종
        bx, by = iso.pt(1.35, 0.3, 27)
        cv.rect(bx - 3, by - 1, bx + 3, by, P['gold_d'])
        cv.rect(bx - 2, by - 4, bx + 2, by - 2, P['gold'])
        cv.rect(bx - 1, by - 5, bx + 1, by - 5, P['gold_l'])
        cv.p(bx, by - 6, P['gold_d'])
        cv.p(bx - 1, by - 4, P['gold_l'])
        # 촛불
        cx, cy = iso.pt(1.75, 0.6, 27)
        candle(cv, cx, cy, f, h=8)
        cv.outline()
        save_with_pivot(cv, OUT, f'reception_{f}', foot(iso, 1, 0.5), pivots)


def questboard():
    """의뢰 게시판: 기둥 두 개, 지붕, 의뢰서들"""
    cv, iso = new(104, 140, 16, 128)
    b = 0.15
    iso.shadow(0, 0, 2, 0.3, grow=0.1)
    # 기둥
    for a in (0.12, 1.88):
        iso.box(a - 0.06, b - 0.06, a + 0.06, b + 0.06, 0, 86, P['wood_l'], P['wood'], P['wood_d'])
    # 판 (b 평면)
    pt = iso.pt
    cv.poly([pt(0.05, b, 26), pt(1.95, b, 26), pt(1.95, b, 76), pt(0.05, b, 76)], P['wood_d'])
    cv.poly([pt(0.12, b, 30), pt(1.88, b, 30), pt(1.88, b, 72), pt(0.12, b, 72)], hexc('6a4a30'))
    for hh in (40, 52, 64):
        cv.line(pt(0.12, b, hh), pt(1.88, b, hh), hexc('5e4028'))
    # 의뢰서 (종이 + 핀)
    notes = [(0.2, 0.62, 50, 69, P['paper']), (0.75, 1.15, 56, 70, P['paper_l']), (1.25, 1.75, 48, 68, P['paper']),
             (0.3, 0.8, 32, 47, P['paper_l']), (1.0, 1.5, 33, 51, hexc('e8c890')), (1.55, 1.82, 34, 44, P['paper'])]
    for a0, a1, h0, h1, col in notes:
        cv.poly([pt(a0, b, h0), pt(a1, b, h0), pt(a1, b, h1), pt(a0, b, h1)], col)
        cv.line(pt(a0, b, h0), pt(a1, b, h0), P['paper_d'])
        for hh in range(h0 + 3, h1 - 2, 3):
            cv.line(pt(a0 + 0.06, b, hh), pt(a1 - 0.08, b, hh), shade(col, 0.72))
        px, py = pt((a0 + a1) / 2, b, h1 - 1)
        cv.p(px, py, P['red_l']); cv.p(px, py + 1, P['red_d'])
    # 현상수배서 얼굴
    fx, fy = pt(1.25, b, 41)
    cv.rect(fx - 2, fy - 3, fx + 2, fy + 1, hexc('b08060'))
    cv.p(fx - 1, fy - 2, P['outline']); cv.p(fx + 1, fy - 2, P['outline'])
    # 판 테두리
    for a0, h0, a1, h1 in ((0.05, 26, 1.95, 26), (0.05, 76, 1.95, 76)):
        cv.line(pt(a0, b, h0), pt(a1, b, h1), P['wood'])
    # 지붕 (작은 박공)
    iso.box(-0.1, b - 0.22, 2.1, b + 0.22, 82, 86, P['red'], P['red_d'], shade(P['red_d'], 0.8))
    cv.poly([pt(-0.1, b - 0.22, 86), pt(2.1, b - 0.22, 86), pt(2.1, b, 93), pt(-0.1, b, 93)], P['red_l'])
    cv.poly([pt(-0.1, b, 93), pt(2.1, b, 93), pt(2.1, b + 0.22, 86), pt(-0.1, b + 0.22, 86)], P['red'])
    for a in [0.1 * i for i in range(0, 22, 2)]:
        cv.line(pt(a - 0.1, b - 0.22, 86), pt(a - 0.1, b, 93), P['red'])
    # 금 표지
    sx, sy = pt(1.0, b, 80)
    cv.rect(sx - 6, sy - 3, sx + 6, sy + 2, P['gold'])
    cv.rect(sx - 5, sy - 2, sx + 5, sy + 1, P['gold_l'])
    cv.rect(sx - 1, sy - 2, sx + 1, sy + 1, P['red'])
    cv.outline()
    save_with_pivot(cv, OUT, 'questboard', foot(iso, 1, b), pivots)


def chair(iso, a, b, facing_a=True):
    s = 0.32
    iso.box(a, b, a + s, b + s, 0, 12, P['wood_l'], P['wood'], P['wood_d'])
    iso.box(a - 0.02, b - 0.02, a + s + 0.02, b + s + 0.02, 12, 14, P['wood_ll'], P['wood_l'], P['wood'])
    if facing_a:
        iso.box(a, b + s - 0.06, a + s, b + s, 14, 28, P['wood_l'], P['wood'], P['wood_d'])
    else:
        iso.box(a + s - 0.06, b, a + s, b + s, 14, 28, P['wood_l'], P['wood'], P['wood_d'])


def adventurers():
    """모험가 작전 테이블: 지도, 투구, 의자 두 개"""
    cv, iso = new(136, 104, 56, 92)
    iso.shadow(-0.5, -0.1, 2.1, 1.5)
    # 뒤쪽 의자 (테이블보다 먼저)
    chair(iso, 0.7, 1.15)
    # 다리
    for a, b in ((0.12, 0.12), (1.8, 0.12), (0.12, 0.88), (1.8, 0.88)):
        iso.box(a, b, a + 0.08, b + 0.08, 0, 18, P['wood'], P['wood_d'], P['dark_wood'])
    # 상판
    iso.box(0, 0, 2, 1, 18, 22, P['wood_l'], P['wood'], P['wood_d'], edge=P['wood_ll'])
    iso.top_lines_a(0, 0, 2, 1, 22, 0.25, shade(P['wood_l'], 0.88))
    # 지도
    pt = iso.pt
    cv.poly([pt(0.3, 0.15, 22), pt(1.55, 0.12, 22), pt(1.6, 0.85, 22), pt(0.28, 0.82, 22)], P['paper'])
    cv.line(pt(0.3, 0.15, 22), pt(0.28, 0.82, 22), P['paper_d'])
    cv.line(pt(1.55, 0.12, 22), pt(1.6, 0.85, 22), P['paper_d'])
    # 지도 위 길·산·X
    path = [(0.45, 0.3), (0.65, 0.45), (0.85, 0.4), (1.05, 0.55), (1.25, 0.5), (1.4, 0.7)]
    for (a0, b0), (a1, b1) in zip(path, path[1:]):
        x0, y0 = pt(a0, b0, 22); x1, y1 = pt(a1, b1, 22)
        for t in (0, 0.5):
            cv.p(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, P['red'])
    for a, b in ((0.6, 0.65), (0.75, 0.7)):
        x, y = pt(a, b, 22)
        cv.p(x, y - 1, P['green_d']); cv.p(x - 1, y, P['green']); cv.p(x + 1, y, P['green'])
    x, y = pt(1.4, 0.7, 22)
    for d in (-1, 0, 1):
        cv.p(x + d, y + d, P['red_d']); cv.p(x + d, y - d, P['red_d'])
    # 투구
    hx, hy = pt(1.75, 0.6, 22)
    cv.rect(hx - 4, hy - 4, hx + 3, hy, P['iron'])
    cv.rect(hx - 3, hy - 6, hx + 2, hy - 5, P['iron_l'])
    cv.rect(hx - 4, hy - 2, hx + 3, hy - 2, P['iron_d'])
    cv.p(hx - 1, hy - 7, P['red']); cv.p(hx, hy - 8, P['red_l']); cv.p(hx - 2, hy - 8, P['red'])
    # 촛불 하나
    candle(cv, *pt(0.2, 0.75, 22), frame=1, h=5)
    cv.outline()
    save_with_pivot(cv, OUT, 'adventurers', foot(iso, 1, 0.5), pivots)


def pub():
    """주점 카운터: 맥주통 두 개, 잔, 술병 선반"""
    cv, iso = new(144, 136, 64, 126)
    iso.shadow(0, 0, 2, 2)
    pt = iso.pt
    # 뒤쪽 선반 (술병)
    iso.box(0.1, 1.55, 1.9, 1.85, 0, 46, P['dark_wood_l'], P['dark_wood'], P['dark_wood_d'])
    for hh in (16, 31, 46):
        cv.line(pt(0.1, 1.55, hh), pt(1.9, 1.55, hh), P['dark_wood_l'])
    colors = [P['green'], P['red'], P['blue_l'], P['purple'], P['green_l'], P['gold']]
    for row, hh in enumerate((17, 32)):
        for k in range(7):
            a = 0.25 + k * 0.24
            x, y = pt(a, 1.55, hh)
            col = colors[(k + row * 2) % len(colors)]
            cv.rect(x - 1, y - 6, x, y - 1, col)
            cv.p(x - 1, y - 6, shade(col, 1.4))
            cv.rect(x - 1, y - 9, x - 1, y - 7, shade(col, 0.7))
    # 맥주통 (선반 앞)
    for a in (0.45, 1.3):
        x, y = pt(a, 1.2, 0)
        barrel(cv, x, y, 9, 22)
        # 꼭지
        cv.rect(x - 1, y - 9, x + 1, y - 8, P['iron_l'])
    # 카운터
    iso.box(0, 0, 2, 0.8, 0, 24, P['dark_wood_l'], P['dark_wood'], P['dark_wood_d'], edge=P['dark_wood_l'])
    iso.face_lines_a(0, 0, 2, 0, 24, 6, shade(P['dark_wood_d'], 0.8))
    iso.box(-0.06, -0.06, 2.06, 0.86, 24, 27, P['wood_l'], P['wood'], P['wood_d'], edge=P['wood_ll'])
    iso.top_lines_a(-0.06, -0.06, 2.06, 0.86, 27, 0.28, shade(P['wood_l'], 0.88))
    # 잔과 접시
    for a, b in ((0.35, 0.35), (0.6, 0.5), (1.45, 0.35)):
        mug(cv, *pt(a, b, 27))
    x, y = pt(1.05, 0.45, 27)
    cv.rect(x - 4, y - 1, x + 4, y, P['paper_l'])
    cv.rect(x - 2, y - 3, x + 2, y - 2, hexc('c8783a'))
    # 금 장식 줄
    cv.line(pt(0, 0, 20), pt(2, 0, 20), P['gold_d'])
    cv.outline()
    save_with_pivot(cv, OUT, 'pub', foot(iso, 1, 0.6), pivots)


def stairs(up=True):
    """계단: b축(오른쪽 벽을 따라 뒤쪽)으로 올라감 / 내려가는 계단은 바닥 구멍"""
    steps = 7
    length = 2.6
    cv, iso = new(150, 170 if up else 120, 90, 160 if up else 110)
    pt = iso.pt
    if up:
        iso.shadow(0, 0, 1, length)
        d = length / steps
        for k in reversed(range(steps)):
            b0 = k * d
            h1 = (k + 1) * 11
            iso.box(0, b0, 1, b0 + d, 0, h1, P['wood_l'], P['stone'] if k == 0 else P['wood'], P['wood_d'])
            cv.line(pt(0, b0, h1), pt(1, b0, h1), P['wood_ll'])
        # 옆판 (돌벽돌)
        for k in range(steps):
            b0 = k * d
            h1 = (k + 1) * 11
            cv.poly([pt(0, b0, 0), pt(0, b0 + d, 0), pt(0, b0 + d, h1), pt(0, b0, h1)], P['stone'])
            for hh in range(0, h1, 6):
                cv.line(pt(0, b0, hh), pt(0, b0 + d, hh), P['stone_d'])
                off = d / 2 if (hh // 6 + k) % 2 else 0
                x, y = pt(0, b0 + off, hh)
                cv.line((x, y), (x, y - 6), P['stone_d'])
            cv.line(pt(0, b0, h1), pt(0, b0 + d, h1), P['stone_ll'])
        # 난간
        posts = [0.05, 1.3, length - 0.05]
        tops = []
        for b in posts:
            k = min(steps - 1, int(b / d))
            h0 = (k + 1) * 11
            x0, y0 = pt(0.05, b, h0)
            cv.rect(x0 - 1, y0 - 22, x0, y0, P['wood_d'])
            cv.p(x0 - 1, y0 - 23, P['gold'])
            tops.append((x0, y0 - 22))
        for (x0, y0), (x1, y1) in zip(tops, tops[1:]):
            cv.line((x0, y0), (x1, y1), P['wood_l'])
            cv.line((x0, y0 + 1), (x1, y1 + 1), P['wood_d'])
        save_name = 'stairs_up'
    else:
        # 바닥 구멍과 내려가는 계단
        hole = [pt(0, 0), pt(1, 0), pt(1, length), pt(0, length)]
        cv.poly(hole, hexc('120a06'))
        # 계단은 구멍 안쪽에만 보이도록 따로 그려서 구멍 모양으로 잘라 붙임
        from PIL import Image, ImageDraw
        layer = Canvas(cv.w, cv.h)
        lis = Iso(layer, iso.ox, iso.oy)
        d = length / steps
        for k in reversed(range(steps)):
            b0 = k * d
            dep = 2 + k * 4
            col = mix(P['wood_l'], hexc('1a100a'), min(1, k / steps * 1.2))
            # 디딤판(윗면)과 챌판(앞면)
            layer.poly([lis.pt(0, b0, -dep), lis.pt(1, b0, -dep), lis.pt(1, b0 + d, -dep), lis.pt(0, b0 + d, -dep)], col)
            layer.poly([lis.pt(0, b0, -dep), lis.pt(1, b0, -dep), lis.pt(1, b0, -dep - 4), lis.pt(0, b0, -dep - 4)], shade(col, 0.6))
            layer.line(lis.pt(0, b0, -dep), lis.pt(1, b0, -dep), shade(col, 1.25))
        mask = Image.new('L', (cv.w, cv.h), 0)
        ImageDraw.Draw(mask).polygon(hole, fill=255)
        cv.im.paste(layer.im, (0, 0), Image.composite(layer.im.split()[3], Image.new('L', (cv.w, cv.h), 0), mask))
        # 앞쪽·왼쪽 바닥 테두리 (돌)
        iso.box(0, -0.12, 1, 0, -4, 0, P['stone_l'], P['stone'], P['stone_d'])
        iso.box(-0.12, -0.12, 0, length, -4, 0, P['stone_l'], P['stone'], P['stone_d'])
        # 난간 (왼쪽과 뒤쪽)
        for b in (0.1, 1.3, length - 0.1):
            x, y = pt(-0.06, b, 0)
            cv.rect(x - 1, y - 20, x, y, P['wood_d'])
            cv.p(x - 1, y - 21, P['gold'])
        cv.line(pt(-0.06, 0.1, 20), pt(-0.06, length - 0.1, 20), P['wood_l'])
        cv.line(pt(-0.06, 0.1, 19), pt(-0.06, length - 0.1, 19), P['wood_d'])
        x, y = pt(0.95, length, 0)
        cv.rect(x - 1, y - 20, x, y, P['wood_d'])
        cv.line(pt(-0.06, length - 0.1, 20), pt(0.95, length, 20), P['wood_l'])
        save_name = 'stairs_down'
    cv.outline()
    save_with_pivot(cv, OUT, save_name, foot(iso, 0.5, length / 2), pivots)


def guild_door():
    """길드 출입문: 왼쪽 뒤 벽(b=0 평면)에 난 돌 아치 문. 문짝은 안쪽으로 열려 있고 바깥 거리와 햇빛이 보임"""
    cv, iso = new(112, 150, 8, 118)
    pt = iso.pt
    A = 1.4                 # 문 전체 폭 (칸)
    ox, oy = iso.ox, iso.oy

    # 바닥에 비치는 햇빛 (문 앞, b<0 쪽)
    for y in range(cv.h):
        for x in range(cv.w):
            # 바닥 평면 역변환: x = ox + 32a - 32b, y = oy - 16a - 16b
            a = ((x - ox) / 32 - (y - oy) / 16) / 2
            b = (-(x - ox) / 32 - (y - oy) / 16) / 2
            if 0.2 <= a <= 1.2 and -1.3 <= b <= 0:
                t = -b / 1.3
                edge = min(a - 0.2, 1.2 - a) / 0.25
                alpha = int(80 * (1 - t) * min(1, edge))
                if alpha > 0: cv.p(x, y, (255, 236, 170, alpha))

    def outer_top(a):
        d = (a - A / 2) / (A / 2)
        return 62 + 16 * math.sqrt(max(0, 1 - d * d))

    def inner_top(a):
        d = (a - A / 2) / 0.55
        return 52 + 12 * math.sqrt(max(0, 1 - d * d))

    for y in range(cv.h):
        for x in range(cv.w):
            a = (x - ox) / 32
            h = oy - 16 * a - y
            if a < 0 or a > A or h < 0:
                continue
            if h > outer_top(a):
                continue
            inside = 0.15 <= a <= A - 0.15 and h <= inner_top(a)
            if not inside:
                # 돌 아치 (벽돌 줄눈)
                row = int(h // 6)
                col = int((a * 8 + (row % 2) * 0.5))
                edge_h = (h % 6) < 1
                edge_a = abs(a * 8 + (row % 2) * 0.5 - round(a * 8 + (row % 2) * 0.5)) < 0.06
                c = P['stone_d'] if (edge_h or edge_a) else (P['stone_l'] if (row + col) % 3 == 0 else P['stone'])
                if h > outer_top(a) - 2: c = P['stone_ll']
                cv.p(x, y, c)
                continue
            # 문 안쪽: 바깥 풍경 (하늘 → 집 → 길)
            top = inner_top(a)
            if h < 8:
                c = mix(hexc('b8a888'), hexc('d8c8a0'), h / 8)
            elif h < 26 and (int(a * 20) % 5 != 0 or h < 20):
                c = hexc('d8c8a8') if int(a * 10) % 3 else hexc('c8a070')
                if h > 20: c = hexc('a8503a')
            else:
                c = mix(hexc('fff4d0'), hexc('8ac8f0'), min(1, (h - 8) / max(1, top - 8)))
            cv.p(x, y, c)
            # 안쪽으로 열린 문짝 (양옆)
            if a < 0.34 or a > A - 0.34:
                plank = int(a * 40) % 3 == 0
                c2 = P['wood_d'] if plank else P['wood']
                if h > top - 3: c2 = P['wood_d']
                cv.p(x, y, c2)
                if abs(h - 14) < 1 or abs(h - 44) < 1:
                    cv.p(x, y, P['iron_d'])

    # 쐐기돌 (금 장식)
    kx, ky = pt(A / 2, 0, 76)
    cv.rect(kx - 3, ky - 3, kx + 3, ky + 4, P['stone_ll'])
    cv.rect(kx - 1, ky - 1, kx + 1, ky + 2, P['gold'])
    # 문턱
    iso.box(0.1, -0.18, A - 0.1, 0, 0, 3, P['stone_l'], P['stone'], P['stone_d'])
    # 오른쪽 등불
    lx, ly = pt(A + 0.12, 0, 46)
    cv.rect(lx - 2, ly - 1, lx + 2, ly + 5, P['iron'])
    cv.rect(lx - 1, ly, lx + 1, ly + 4, P['flame_l'])
    cv.p(lx, ly - 2, P['iron_d'])
    # 문 위 '길드' 현판
    sx, sy = pt(A / 2, 0, 84)
    cv.rect(sx - 10, sy - 4, sx + 10, sy + 3, P['wood_d'])
    cv.rect(sx - 9, sy - 3, sx + 9, sy + 2, P['wood_l'])
    cv.rect(sx - 2, sy - 2, sx + 2, sy + 1, P['gold'])
    cv.outline()
    save_with_pivot(cv, OUT, 'guild_door', pt(A / 2, 0, 0), pivots)


# ─── 벽 ─────────────────────────────────────────────────────

WALL_H = 112


def wall(name, cells, axis, style, beams=None):
    """뒤쪽 벽. axis='a' 왼쪽 뒤 벽(b=0 평면, a로 뻗음), 'b' 오른쪽 뒤 벽(a=0 평면, b로 뻗음)
    style='hall' 1층(돌 아랫단 + 회벽 + 나무 기둥), 'inn' 2층(나무 판벽 + 회벽)"""
    W = cells * 32 + 24
    H = cells * 16 + WALL_H + 24
    if axis == 'a':
        cv, iso = new(W, H, 12, H - 8)
        def pt(t, h): return iso.pt(t, 0, h)
    else:
        cv, iso = new(W, H, W - 12, H - 8)
        def pt(t, h): return iso.pt(0, t, h)

    def quad(t0, t1, h0, h1, c):
        cv.poly([pt(t0, h0), pt(t1, h0), pt(t1, h1), pt(t0, h1)], c)

    def hline(t0, t1, h, c):
        cv.line(pt(t0, h), pt(t1, h), c)

    def vline(t, h0, h1, c):
        cv.line(pt(t, h0), pt(t, h1), c)

    low = 34
    if style == 'hall':
        # 돌 아랫단
        quad(0, cells, 0, low, P['stone'])
        for h in range(0, low, 8):
            hline(0, cells, h, P['stone_d'])
            off = 0.25 if (h // 8) % 2 else 0
            t = off
            while t < cells:
                vline(t, h, min(low, h + 8), P['stone_d'])
                t += 0.5
        hline(0, cells, low - 1, P['stone_l'])
        upper, upper_d, beam, beam_d = P['plaster'], P['plaster_d'], P['dark_wood'], P['dark_wood_d']
    else:
        quad(0, cells, 0, low, P['wood'])
        t = 0
        while t < cells:
            vline(t, 0, low, P['wood_d'])
            t += 0.25
        hline(0, cells, low - 1, P['wood_ll'])
        upper, upper_d, beam, beam_d = hexc('d8c8a0'), hexc('b8a47c'), P['wood'], P['wood_d']
    quad(0, cells, low, WALL_H, upper)
    # 회벽 얼룩
    for i in range(cells * 6):
        t = (i * 0.37) % cells
        h = low + 6 + (i * 13) % (WALL_H - low - 12)
        x, y = pt(t, h)
        cv.p(x, y, upper_d)
    # 나무 기둥·보
    for t in (beams if beams is not None else range(0, cells + 1, 2)):
        quad(t - 0.08, t + 0.08, low, WALL_H, beam)
        vline(t + 0.08, low, WALL_H, beam_d)
    for h in (low, 78, WALL_H - 6):
        quad(0, cells, h, h + 5, beam)
        hline(0, cells, h, beam_d)
    # 위쪽 마감 (벽 두께)
    if axis == 'a':
        cv.poly([iso.pt(0, 0, WALL_H), iso.pt(cells, 0, WALL_H), iso.pt(cells, 0.15, WALL_H), iso.pt(0, 0.15, WALL_H)], beam_d)
    else:
        cv.poly([iso.pt(0, 0, WALL_H), iso.pt(0, cells, WALL_H), iso.pt(0.15, cells, WALL_H), iso.pt(0.15, 0, WALL_H)], beam_d)

    # 장식 자리 (기둥 사이 칸 가운데: 홀수 칸)
    slots = [t + 1 for t in range(0, cells - 1, 2)]
    return cv, iso, pt, quad, hline, vline, slots


def window(pt, cv, t, h0, h1, night=False):
    """아치 창문 (평면 위)"""
    w = 0.42
    for hh in range(h0, h1 + 1):
        frac = (hh - h0) / (h1 - h0)
        # 위쪽은 둥글게
        r = 1.0 if frac < 0.72 else math.sqrt(max(0, 1 - ((frac - 0.72) / 0.28) ** 2))
        tw = w * r
        x0, y0 = pt(t - tw, hh); x1, y1 = pt(t + tw, hh)
        glass = mix(hexc('2a3a6a') if night else hexc('8ac0e8'), hexc('e8f4ff') if not night else hexc('5a6aa0'), frac)
        cv.line((x0, y0), (x1, y1), glass)
    # 창살
    xa, ya = pt(t, h0); xb, yb = pt(t, h1)
    cv.line((xa, ya), (xb, yb), P['dark_wood_d'])
    mid = (h0 + h1) // 2 - 2
    cv.line(pt(t - w, mid), pt(t + w, mid), P['dark_wood_d'])
    # 창틀
    cv.line(pt(t - w - 0.05, h0 - 1), pt(t + w + 0.05, h0 - 1), P['stone_l'])
    cv.line(pt(t - w - 0.05, h0 - 2), pt(t + w + 0.05, h0 - 2), P['stone'])


def banner(pt, cv, t, h_top, color, color_d, emblem='crest'):
    """걸린 깃발: 길드 문장"""
    w = 0.32
    length = 40
    for hh in range(0, length):
        cut = hh > length - 7 and abs((hh - (length - 7))) > 0
        tw = w if hh < length - 6 else w * (1 - (hh - (length - 6)) / 7)
        x0, y0 = pt(t - w, h_top - hh); x1, y1 = pt(t + w, h_top - hh)
        if hh >= length - 6:
            # 제비꼬리
            k = hh - (length - 6)
            cv.line(pt(t - w, h_top - hh), pt(t - w + 0.05 * (6 - k), h_top - hh), color_d)
            cv.line(pt(t + w - 0.05 * (6 - k), h_top - hh), pt(t + w, h_top - hh), color_d)
            continue
        cv.line((x0, y0), (x1, y1), color)
        cv.p(x1, y1, color_d)
    # 금 테두리와 문장
    cv.line(pt(t - w, h_top - 1), pt(t + w, h_top - 1), P['gold'])
    cx, cy = pt(t, h_top - 18)
    if emblem == 'crest':
        cv.rect(cx - 4, cy - 5, cx + 4, cy + 3, P['gold'])
        cv.rect(cx - 3, cy - 4, cx + 3, cy + 2, P['gold_l'])
        cv.p(cx, cy + 4, P['gold']); cv.rect(cx - 2, cy + 3, cx + 2, cy + 3, P['gold'])
        cv.rect(cx - 1, cy - 3, cx + 1, cy + 1, color_d)
        cv.p(cx, cy - 4, color_d)
    else:
        for d in range(-3, 4):
            cv.p(cx + d, cy, P['gold_l']); cv.p(cx, cy + d, P['gold_l'])
    # 봉
    cv.line(pt(t - w - 0.06, h_top + 1), pt(t + w + 0.06, h_top + 1), P['dark_wood_d'])


def shelf(pt, cv, t, h):
    """벽 선반: 책·병"""
    cv.line(pt(t - 0.45, h), pt(t + 0.45, h), P['wood_d'])
    cv.line(pt(t - 0.45, h + 1), pt(t + 0.45, h + 1), P['wood_l'])
    cols = [P['red'], P['blue'], P['green'], P['gold_d'], P['purple'], P['red_d']]
    for k in range(8):
        x, y = pt(t - 0.4 + k * 0.1, h + 2)
        c = cols[k % len(cols)]
        hh = 6 + (k * 3) % 4
        cv.rect(x, y - hh, x + 1, y, c)
        cv.p(x, y - hh, shade(c, 1.4))


def painting(pt, cv, t, h, scene):
    w = 0.38
    quad = lambda t0, t1, h0, h1, c: cv.poly([pt(t0, h0), pt(t1, h0), pt(t1, h1), pt(t0, h1)], c)
    quad(t - w - 0.05, t + w + 0.05, h - 2, h + 22, P['gold_d'])
    quad(t - w, t + w, h, h + 20, hexc('6a9ac8') if scene == 'sea' else hexc('5a8a4a'))
    quad(t - w, t + w, h, h + 7, hexc('3a6a3a') if scene != 'sea' else hexc('2a4a7a'))
    x, y = pt(t + 0.1, h + 14)
    cv.rect(x - 1, y - 1, x + 1, y + 1, P['flame_l'])


def build_walls():
    # 1층: 왼쪽 뒤 벽 16칸, 오른쪽 뒤 벽 10칸
    for floor in ('hall', 'inn'):
        # 2층 왼쪽 벽은 객실 문(2~3, 5~6, 8~9, 11~12, 14~15칸) 사이에 기둥과 장식이 오도록
        inn_beams = [0, 1, 4, 7, 10, 13, 16]
        cv, iso, pt, quad, hline, vline, slots = wall(f'wall_{floor}_left', 16, 'a', floor,
                                                      beams=None if floor == 'hall' else inn_beams)
        if floor == 'hall':
            for i, t in enumerate(slots):
                if i in (1, 4, 6): window(pt, cv, t, 44, 74)
                elif i in (2, 5): banner(pt, cv, t, 102, P['red'], P['red_d'])
                elif i == 0: shelf(pt, cv, t, 58)
                elif i == 3: window(pt, cv, t, 44, 74)
        else:
            # 문 위는 이름표 자리라 비우고, 문 사이에만 작은 그림·창
            for k, t in enumerate((4, 7, 10, 13)):
                if k % 2 == 0: painting(pt, cv, t + 0.55, 44, 'sea' if k == 0 else 'field')
                else: window(pt, cv, t + 0.55, 44, 70)
        cv.outline()
        save_with_pivot(cv, OUT, f'wall_{floor}_left', iso.pt(0, 0, 0), pivots)

        cv, iso, pt, quad, hline, vline, slots = wall(f'wall_{floor}_right', 10, 'b', floor)
        for i, t in enumerate(slots):
            if floor == 'hall':
                if i == 0: banner(pt, cv, t, 102, P['blue'], P['blue_d'], emblem='star')
                elif i == 2: pass  # 벽난로 자리 (따로 그림)
                elif i == 3: pass  # 7~8칸: 길드 출입문 자리
                elif i == 1: shelf(pt, cv, t, 58)
            else:
                if i in (0, 3): window(pt, cv, t, 50, 76)
                elif i == 1: painting(pt, cv, t, 56, 'field')
                elif i == 2: banner(pt, cv, t, 100, P['green'], P['green_d'], emblem='star')
        cv.outline()
        save_with_pivot(cv, OUT, f'wall_{floor}_right', iso.pt(0, 0, 0), pivots)


def torch():
    """벽 횃불 (불꽃이 흔들림). 피벗 = 받침 아래"""
    for f in range(FRAMES):
        cv = Canvas(16, 32)
        cv.rect(7, 18, 8, 28, P['wood_d'])
        cv.rect(6, 16, 9, 18, P['iron'])
        cv.rect(5, 15, 10, 15, P['iron_l'])
        # 빛 번짐
        for y in range(0, 16):
            for x in range(0, 16):
                d = math.hypot(x - 7.5, y - 10)
                if d < 7:
                    cv.p(x, y, (255, 200, 90, int(40 * (1 - d / 7))))
        flame(cv, 7, 14, f, size=2)
        flame(cv, 8, 14, (f + 2) % 4, size=1)
        cv.outline()
        save_with_pivot(cv, OUT, f'torch_{f}', (8, 28), pivots)


def fireplace():
    """오른쪽 벽 벽난로 (불이 흔들림). 벽(a=0 평면)에 붙는 돌 화덕"""
    for f in range(FRAMES):
        cv, iso = new(96, 120, 64, 112)
        pt = iso.pt
        iso.shadow(0, -0.2, 0.45, 1.4)
        # 굴뚝·화덕 몸체 (벽에서 앞으로 튀어나옴)
        iso.box(0, 0, 0.4, 1.2, 0, 104, P['stone_l'], P['stone'], P['stone_d'])
        for h in range(0, 104, 8):
            iso.cv.line(pt(0, 0, h), pt(0, 1.2, h), P['stone_d'])
            iso.cv.line(pt(0, 0, h), pt(0.4, 0, h), shade(P['stone_d'], 0.8))
        # 선반(맨틀)
        iso.box(-0.08, -0.05, 0.48, 1.25, 50, 55, P['wood_l'], P['wood'], P['wood_d'])
        # 화구 (왼쪽 앞면에 구멍)
        def fq(b0, b1, h0, h1, c):
            cv.poly([pt(0, b0, h0), pt(0, b1, h0), pt(0, b1, h1), pt(0, b0, h1)], c)
        fq(0.2, 1.0, 0, 38, hexc('1a0c06'))
        fq(0.25, 0.95, 0, 34, hexc('2a1208'))
        # 장작
        x0, y0 = pt(0, 0.35, 4); x1, y1 = pt(0, 0.85, 4)
        cv.line((x0, y0), (x1, y1), P['wood_d'])
        cv.line((x0, y0 - 1), (x1, y1 - 1), P['wood'])
        # 불
        for k, b in enumerate((0.42, 0.55, 0.68, 0.8)):
            x, y = pt(0, b, 6)
            flame(cv, x, y, f + k, size=2 if k in (1, 2) else 1)
        # 빛
        for y in range(cv.h):
            for x in range(cv.w):
                cx, cy = pt(0, 0.6, 14)
                d = math.hypot(x - cx, (y - cy) * 1.4)
                if d < 26 and cv.get(x, y)[3] > 0:
                    pass
        # 맨틀 위 장식: 촛대와 방패
        candle(cv, *pt(0.2, 0.3, 55), frame=f, h=5)
        candle(cv, *pt(0.2, 1.0, 55), frame=f + 2, h=5)
        sx, sy = pt(0, 0.65, 78)
        cv.rect(sx - 5, sy - 6, sx + 5, sy + 4, P['red'])
        cv.rect(sx - 4, sy - 5, sx + 4, sy + 3, P['red_l'])
        cv.rect(sx - 1, sy - 5, sx + 1, sy + 3, P['gold'])
        cv.rect(sx - 4, sy - 1, sx + 4, sy, P['gold'])
        cv.outline()
        save_with_pivot(cv, OUT, f'fireplace_{f}', pt(0.2, 0.6, 0), pivots)


# ─── 2층 ────────────────────────────────────────────────────

def inn_door():
    """객실 문: 벽(왼쪽 뒤 벽, b=0 평면)에 붙은 돌 문틀 + 나무 문 + 번호판 자리 + 등불 (불이 흔들림)"""
    for f in range(FRAMES):
        cv, iso = new(80, 96, 22, 88)
        pt = iso.pt
        def q(a0, a1, h0, h1, c):
            cv.poly([pt(a0, 0, h0), pt(a1, 0, h0), pt(a1, 0, h1), pt(a0, 0, h1)], c)
        # 문틀 (돌)
        q(-0.05, 1.05, 0, 58, P['stone_l'])
        q(0.05, 0.95, 0, 52, P['stone'])
        # 문
        q(0.12, 0.88, 0, 50, P['wood'])
        a = 0.12
        while a < 0.88:
            cv.line(pt(a, 0, 0), pt(a, 0, 50), P['wood_d'])
            a += 0.19
        q(0.12, 0.88, 46, 50, P['wood_l'])
        for h in (10, 38):
            cv.line(pt(0.12, 0, h), pt(0.7, 0, h), P['iron_d'])
        x, y = pt(0.78, 0, 24)
        cv.rect(x - 1, y - 1, x + 1, y + 1, P['gold'])
        # 번호판 (금)
        x, y = pt(0.5, 0, 55)
        cv.rect(x - 6, y - 3, x + 6, y + 2, P['gold_d'])
        cv.rect(x - 5, y - 2, x + 5, y + 1, P['gold'])
        # 문턱
        iso.box(0.05, 0, 0.95, 0.18, 0, 2, P['stone_l'], P['stone'], P['stone_d'])
        # 등불
        lx, ly = pt(1.18, 0, 44)
        cv.rect(lx - 2, ly - 1, lx + 2, ly + 4, P['iron'])
        cv.rect(lx - 1, ly, lx + 1, ly + 3, P['flame_l'] if f % 2 == 0 else P['flame'])
        cv.p(lx, ly - 2, P['iron_d']); cv.p(lx, ly - 3, P['iron_d'])
        for yy in range(ly - 6, ly + 10):
            for xx in range(lx - 7, lx + 8):
                d = math.hypot(xx - lx, yy - ly - 1)
                if d < 7 and cv.get(xx, yy)[3] > 0:
                    cv.p(xx, yy, (255, 210, 120, int(30 * (1 - d / 7))))
        cv.outline()
        save_with_pivot(cv, OUT, f'inn_door_{f}', pt(0.5, 0.25, 0), pivots)


def inn_sign():
    """여관 안내판: 기둥에 매단 나무 간판 (침대와 달)"""
    cv, iso = new(64, 96, 32, 88)
    pt = iso.pt
    iso.shadow(-0.15, -0.15, 0.15, 0.15)
    iso.box(-0.05, -0.05, 0.05, 0.05, 0, 70, P['dark_wood_l'], P['dark_wood'], P['dark_wood_d'])
    # 팔
    x0, y0 = pt(0, 0, 66)
    cv.rect(x0, y0 - 1, x0 + 22, y0, P['dark_wood'])
    cv.rect(x0 + 4, y0 + 1, x0 + 4, y0 + 2, P['iron_d']); cv.rect(x0 + 19, y0 + 1, x0 + 19, y0 + 2, P['iron_d'])
    # 간판
    cv.rect(x0 + 1, y0 + 3, x0 + 23, y0 + 19, P['wood_d'])
    cv.rect(x0 + 2, y0 + 4, x0 + 22, y0 + 18, P['wood_l'])
    cv.rect(x0 + 2, y0 + 4, x0 + 22, y0 + 4, P['wood_ll'])
    # 침대 그림
    bx, by = x0 + 5, y0 + 15
    cv.rect(bx, by - 3, bx + 14, by - 1, P['red'])
    cv.rect(bx, by - 5, bx + 4, by - 3, P['paper_l'])
    cv.rect(bx, by - 7, bx, by, P['wood_d']); cv.rect(bx + 14, by - 4, bx + 14, by, P['wood_d'])
    # 달
    cv.rect(x0 + 17, y0 + 6, x0 + 19, y0 + 8, P['gold_l'])
    cv.p(x0 + 19, y0 + 6, P['wood_l'])
    cv.outline()
    save_with_pivot(cv, OUT, 'inn_sign', foot(iso, 0, 0), pivots)


def rug(name, a_len, b_len, base, border, pattern):
    """바닥 카펫 (평면). 피벗 = 가운데"""
    W = int((a_len + b_len) * 32) + 8
    H = int((a_len + b_len) * 16) + 8
    cv, iso = new(W, H, int(b_len * 32) + 4, H - 4)
    pt = iso.pt
    cv.poly([pt(0, 0), pt(a_len, 0), pt(a_len, b_len), pt(0, b_len)], border)
    m = 0.18
    cv.poly([pt(m, m), pt(a_len - m, m), pt(a_len - m, b_len - m), pt(m, b_len - m)], base)
    m2 = 0.3
    for (a0, b0), (a1, b1) in (((m2, m2), (a_len - m2, m2)), ((a_len - m2, m2), (a_len - m2, b_len - m2)),
                               ((a_len - m2, b_len - m2), (m2, b_len - m2)), ((m2, b_len - m2), (m2, m2))):
        cv.line(pt(a0, b0), pt(a1, b1), pattern)
    ca, cb = a_len / 2, b_len / 2
    r = min(a_len, b_len) * 0.28
    cv.poly([pt(ca - r, cb), pt(ca, cb - r), pt(ca + r, cb), pt(ca, cb + r)], pattern)
    r2 = r * 0.55
    cv.poly([pt(ca - r2, cb), pt(ca, cb - r2), pt(ca + r2, cb), pt(ca, cb + r2)], base)
    for k in range(int(b_len * 6)):
        bb = (k + 0.5) / 6
        for a, d in ((0, -1), (a_len, 1)):
            x, y = pt(a, bb)
            cv.p(x + d * 2, y + d, pattern)
            cv.p(x + d, y, pattern)
    save_with_pivot(cv, OUT, name, pt(a_len / 2, b_len / 2), pivots)


def crates():
    """상자 더미와 자루 (장식)"""
    cv, iso = new(96, 96, 48, 88)
    iso.shadow(-0.1, -0.1, 1.1, 1.1)

    def crate(a0, b0, s, h0):
        hh = int(s * 32)
        iso.box(a0, b0, a0 + s, b0 + s, h0, h0 + hh, P['wood_l'], P['wood'], P['wood_d'], edge=P['wood_ll'])
        pt = iso.pt
        cv.line(pt(a0, b0, h0), pt(a0 + s, b0, h0 + hh), P['wood_d'])
        cv.line(pt(a0, b0, h0), pt(a0, b0 + s, h0 + hh), shade(P['wood'], 0.7))
    crate(0.45, 0.4, 0.55, 0)
    crate(0.0, 0.05, 0.55, 0)
    crate(0.1, 0.2, 0.42, 18)
    x, y = iso.pt(0.95, 0.0, 0)
    for yy in range(14):
        w = 5 - abs(yy - 8) // 2
        for xx in range(-w, w + 1):
            cv.p(x + xx, y - yy, hexc('c8a870') if xx < 1 else hexc('a08850'))
    cv.rect(x - 2, y - 15, x + 2, y - 14, hexc('8a6a3a'))
    cv.outline()
    save_with_pivot(cv, OUT, 'crates', iso.pt(0.5, 0.5, 0), pivots)


def weapon_rack():
    """무기 거치대 (장식): 창·검·방패"""
    cv, iso = new(80, 96, 24, 88)
    pt = iso.pt
    iso.shadow(0, 0, 1.2, 0.3)
    iso.box(0, 0, 1.2, 0.25, 0, 6, P['wood_l'], P['wood'], P['wood_d'])
    for a in (0.05, 1.1):
        iso.box(a, 0.05, a + 0.06, 0.12, 6, 52, P['wood_l'], P['wood'], P['wood_d'])
    cv.line(pt(0.05, 0.08, 46), pt(1.16, 0.08, 46), P['wood_d'])
    for a, top, col in ((0.3, 72, P['iron_l']), (0.55, 66, P['iron_l']), (0.85, 58, P['stone_ll'])):
        x0, y0 = pt(a, 0.12, 6)
        x1, y1 = pt(a + 0.05, 0.12, top)
        cv.line((x0, y0), (x1, y1), P['wood_d'] if top > 60 else P['iron'])
        cv.rect(x1 - 1, y1 - 4, x1 + 1, y1, col)
    x, y = pt(0.75, 0.0, 22)
    cv.rect(x - 6, y - 8, x + 6, y + 4, P['blue'])
    cv.rect(x - 5, y - 7, x + 5, y + 3, P['blue_l'])
    cv.rect(x - 1, y - 7, x + 1, y + 3, P['gold'])
    cv.rect(x - 5, y - 3, x + 5, y - 2, P['gold'])
    cv.outline()
    save_with_pivot(cv, OUT, 'weapon_rack', pt(0.6, 0.12, 0), pivots)


def tavern_table():
    """주점 원탁: 둥근 탁자 + 의자 셋 + 맥주잔·촛불"""
    cv, iso = new(104, 96, 52, 80)
    pt = iso.pt
    iso.shadow(0, 0, 1, 1, grow=0.25)
    cx, cy = pt(0.5, 0.5, 0)

    def stool(a, b):
        x, y = pt(a, b, 0)
        cv.rect(x - 1, y - 9, x, y, P['wood_d'])
        for xx in range(-5, 6):
            dy = int(round(math.sqrt(max(0, 1 - (xx / 5.5) ** 2)) * 2.5))
            for yy in range(-dy, dy + 1):
                cv.p(x + xx, y - 11 + yy, P['wood_l'] if yy < 0 else P['wood'])
        cv.rect(x - 5, y - 10, x + 5, y - 9, P['wood_d'])

    stool(0.5, 1.35)    # 뒤쪽 의자 (탁자보다 먼저)
    stool(1.35, 0.5)
    # 다리
    cv.rect(cx - 2, cy - 18, cx + 1, cy, P['wood_d'])
    cv.rect(cx - 6, cy - 1, cx + 5, cy, P['wood_d'])
    # 둥근 상판 (타원)
    rx, ry, ty = 24, 12, cy - 20
    for yy in range(-ry - 3, ry + 4):
        for xx in range(-rx, rx + 1):
            e = (xx / rx) ** 2 + (yy / ry) ** 2
            if e <= 1:
                c = P['wood_l'] if e < 0.8 else P['wood_ll'] if yy < 0 else P['wood']
                if (xx + 2 * yy) % 9 == 0 and e < 0.8: c = shade(P['wood_l'], 0.9)
                cv.p(cx + xx, ty + yy, c)
            elif (xx / rx) ** 2 + ((yy - 3) / ry) ** 2 <= 1 and yy > 0:
                cv.p(cx + xx, ty + yy, P['wood_d'])  # 상판 두께
    # 탁자 위
    mug(cv, cx - 12, ty + 2)
    mug(cv, cx + 6, ty - 3)
    candle(cv, cx - 2, ty + 1, 1, h=4)
    x, y = cx + 10, ty + 5
    cv.rect(x - 3, y - 1, x + 3, y, P['paper_l'])
    cv.rect(x - 2, y - 2, x + 1, y - 2, hexc('c8783a'))
    stool(-0.35, 0.5)   # 앞쪽 의자
    cv.outline()
    save_with_pivot(cv, OUT, 'tavern_table', pt(0.5, 0.5, 0), pivots)


if __name__ == '__main__':
    tavern_table()
    rug('rug_hall', 5, 3.2, P['red'], P['gold_d'], P['gold'])
    rug('rug_inn', 11, 1.3, P['blue'], P['gold_d'], P['gold'])
    crates()
    weapon_rack()
    reception()
    questboard()
    adventurers()
    pub()
    stairs(True)
    stairs(False)
    guild_door()
    build_walls()
    torch()
    fireplace()
    inn_door()
    inn_sign()
    write_pivots(OUT, pivots)
    print('ok', len(pivots), 'sprites')
