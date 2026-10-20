# 길드 신청서 화면 도트 그림 생성기 (촛불 켜진 모험가 협회 책상 + 신청서)
# 실행: python Tools/creation_art_gen.py  → Assets/Art/Creation/*.png
# 책상은 화면 4배(512x288, 가운데 480x270이 보임), 신청서·도장·서명은 글자가 많아 2배로 표시
import math
import os
import random
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from pixel_lib import Canvas, shade, mix, hexc
import title_art_gen as T

ROOT = os.path.join(os.path.dirname(__file__), '..')
OUT = os.path.join(ROOT, 'Assets', 'Art', 'Creation')
LW, LH = 512, 288
OX, OY = 16, 9
BAYER = T.BAYER
INK = hexc('3a2014')
INK_L = hexc('6a4028')
RED_INK = hexc('b8281e')

# 신청서 (2배 표시) 크기와 칸 위치 — Editor/GuildCreationSetup.cs와 같은 값
PW, PH = 368, 464
NAME_LINE_Y = 178
SIGN_LINE_Y = 426


def save(cv, name):
    cv.save(os.path.join(OUT, name + '.png'))


def disc(cv, cx, cy, r, c):
    T.disc(cv, cx, cy, r, c)


def text(cv, s, size, x, y, color, center=False, shadow=None):
    m = T.text_mask(s, size)
    px = m.load()
    if center: x = x - m.size[0] // 2
    for yy in range(m.size[1]):
        for xx in range(m.size[0]):
            if px[xx, yy] > 127:
                if shadow: cv.p(x + xx + 1, y + yy + 1, shadow)
                cv.p(x + xx, y + yy, color)
    return m.size


# ─── 책상 ───────────────────────────────────────────────────

def desk():
    rnd = random.Random(3)
    cv = Canvas(LW, LH)
    base = [hexc('5a3420'), hexc('4e2c1a'), hexc('623a24'), hexc('54301c')]
    plank_h = 36
    for i, y0 in enumerate(range(0, LH, plank_h)):
        c = base[i % len(base)]
        for y in range(y0, min(LH, y0 + plank_h)):
            for x in range(LW):
                # 나뭇결: 물결무늬 줄
                g = math.sin((x * 0.045) + math.sin(y * 0.4 + i) * 1.6 + i * 3)
                col = c
                if g > 0.86: col = shade(c, 1.12)
                elif g < -0.9: col = shade(c, 0.86)
                if (x * 13 + y * 7 + i * 31) % 97 == 0: col = shade(c, 0.8)
                cv.p(x, y, col)
        cv.rect(0, y0, LW - 1, y0, hexc('2a160c'))                # 판자 틈
        cv.rect(0, y0 + 1, LW - 1, y0 + 1, shade(c, 1.15))
        # 판자 이음매 + 못
        jx = rnd.randint(40, LW - 40)
        cv.rect(jx, y0, jx, y0 + plank_h - 1, hexc('2a160c'))
        cv.p(jx - 4, y0 + 5, hexc('9a8a7a')); cv.p(jx + 4, y0 + plank_h - 6, hexc('9a8a7a'))
        for _ in range(2):   # 옹이
            kx, ky = rnd.randint(10, LW - 10), y0 + rnd.randint(8, plank_h - 8)
            for r in (4, 3, 2):
                for a in range(0, 360, 12):
                    cv.p(kx + math.cos(math.radians(a)) * r * 1.6, ky + math.sin(math.radians(a)) * r * 0.7, shade(c, 0.75 + r * 0.03))

    # ── 왼쪽 위: 촛대와 초 ──
    cx, cy = 72, 132
    disc(cv, cx, cy + 34, 22, (0, 0, 0, 70))                          # 그림자
    for r, col in ((18, hexc('8a6420')), (16, hexc('d4a84a')), (13, hexc('b88a34'))):
        for a in range(0, 360, 2):
            for rr in range(r - 2, r + 1):
                cv.p(cx + math.cos(math.radians(a)) * rr, cy + 30 + math.sin(math.radians(a)) * rr * 0.45, col)
    for y in range(cy + 22, cy + 31):
        cv.rect(cx - 4, y, cx + 4, y, hexc('b88a34'))
    cv.rect(cx - 4, cy + 22, cx - 3, cy + 30, hexc('f2d488'))
    cv.rect(cx - 7, cy + 19, cx + 7, cy + 22, hexc('d4a84a'))
    # 초 (밀랍, 흘러내린 자국)
    cv.rect(cx - 5, cy - 14, cx + 5, cy + 19, hexc('efe2c4'))
    cv.rect(cx - 5, cy - 14, cx - 4, cy + 19, hexc('fff6e0'))
    cv.rect(cx + 4, cy - 14, cx + 5, cy + 19, hexc('cbb894'))
    for dx, ln in ((-3, 9), (1, 14), (3, 6)):
        cv.rect(cx + dx, cy - 13, cx + dx + 1, cy - 13 + ln, hexc('fff6e0'))
    cv.rect(cx - 6, cy - 15, cx + 6, cy - 14, hexc('fff6e0'))
    cv.rect(cx, cy - 18, cx, cy - 15, hexc('2a1810'))                # 심지 (불꽃은 런타임)

    # ── 왼쪽 아래: 책 더미와 동전 주머니 ──
    books = [(26, 222, 92, 16, hexc('6a1e1e')), (32, 207, 82, 15, hexc('1e3a5a')), (22, 193, 88, 14, hexc('3a5a2a'))]
    disc(cv, 70, 246, 40, (0, 0, 0, 50))
    for x, y, w, h, c in books:
        cv.rect(x, y, x + w, y + h, c)
        cv.rect(x, y, x + w, y + 1, shade(c, 1.3))
        cv.rect(x + w - 6, y + 2, x + w - 1, y + h - 1, hexc('e8dcc0'))   # 종이 단면
        for yy in range(y + 3, y + h - 1, 2): cv.rect(x + w - 6, yy, x + w - 1, yy, hexc('c8b898'))
        cv.rect(x + 8, y + 4, x + 8, y + h - 3, hexc('d4a84a')); cv.rect(x + 12, y + 4, x + 12, y + h - 3, hexc('d4a84a'))
        cv.rect(x - 1, y, x - 1, y + h, shade(c, 0.6))
    # 동전 주머니
    px_, py_ = 108, 236
    disc(cv, px_, py_ + 4, 14, (0, 0, 0, 60))
    disc(cv, px_, py_, 12, hexc('8a5a32'))
    disc(cv, px_ - 3, py_ - 3, 7, hexc('a8743e'))
    cv.rect(px_ - 5, py_ - 14, px_ + 5, py_ - 10, hexc('8a5a32'))
    cv.rect(px_ - 7, py_ - 11, px_ + 7, py_ - 10, hexc('d4a84a'))
    for i, (dx, dy) in enumerate(((16, 8), (22, 2), (19, 13), (28, 10), (12, 16))):
        cx2, cy2 = px_ + dx, py_ + dy
        for yy in range(-2, 3):
            for xx in range(-3, 4):
                if xx * xx / 9 + yy * yy / 4 <= 1:
                    cv.p(cx2 + xx, cy2 + yy, hexc('f2d488') if yy < 0 else hexc('d4a84a'))
        cv.p(cx2 - 3, cy2, hexc('8a6420')); cv.p(cx2 + 3, cy2, hexc('8a6420'))

    # ── 오른쪽 위: 지도 한 장 ──
    mx0, my0 = 372, 24
    pts = [(mx0, my0 + 6), (mx0 + 98, my0), (mx0 + 104, my0 + 64), (mx0 + 6, my0 + 72)]
    cv.poly([(x + 3, y + 4) for x, y in pts], (0, 0, 0, 70))
    cv.poly(pts, hexc('d8c49a'))
    for _ in range(200):
        x, y = rnd.randint(mx0 + 4, mx0 + 98), rnd.randint(my0 + 6, my0 + 66)
        cv.p(x, y, hexc('c8b088'))
    coast = [(mx0 + 14, my0 + 40), (mx0 + 30, my0 + 28), (mx0 + 48, my0 + 34), (mx0 + 62, my0 + 20), (mx0 + 86, my0 + 26), (mx0 + 92, my0 + 50)]
    for a, b in zip(coast, coast[1:]): cv.line(a, b, hexc('6a4a2a'))
    for i in range(6): cv.p(mx0 + 40 + i * 6, my0 + 50 + (i % 2) * 2, hexc('8a6a3a'))   # 점선 길
    cv.line((mx0 + 74, my0 + 42), (mx0 + 80, my0 + 48), RED_INK); cv.line((mx0 + 80, my0 + 42), (mx0 + 74, my0 + 48), RED_INK)

    # ── 오른쪽 아래: 잉크병 (깃펜은 런타임 그림) ──
    ix, iy = 430, 222
    disc(cv, ix + 2, iy + 16, 18, (0, 0, 0, 70))
    for y in range(iy - 6, iy + 15):
        t = (y - iy + 6) / 21
        w = int(13 - 3 * abs(t - 0.6) * 2)
        cv.rect(ix - w, y, ix + w, y, hexc('1e2a3a'))
        cv.p(ix - w + 2, y, hexc('5a7a9a')); cv.p(ix - w + 3, y, hexc('3a5470'))
    cv.rect(ix - 6, iy - 12, ix + 6, iy - 6, hexc('1e2a3a'))
    cv.rect(ix - 7, iy - 13, ix + 7, iy - 12, hexc('d4a84a'))
    cv.rect(ix - 5, iy - 12, ix + 5, iy - 11, hexc('0e0a10'))
    cv.p(ix - 9, iy + 2, hexc('9ab8d8'))
    # 잉크 얼룩
    for dx, dy, r in ((-30, 22, 3), (-36, 26, 2), (22, 24, 2)):
        disc(cv, ix + dx, iy + dy, r, hexc('1a1420'))

    # ── 가장자리 어둡게 (비네트, 디더) ──
    for y in range(LH):
        for x in range(LW):
            dx = (x - LW / 2) / (LW / 2)
            dy = (y - LH / 2) / (LH / 2)
            d = math.sqrt(dx * dx * 0.8 + dy * dy)
            a = int(max(0, min(1, (d - 0.72) / 0.5)) * 150)
            if a > 0:
                cv.p(x, y, (12, 6, 10, a))
    save(cv, 'creation_desk')

    # 촛불 빛 (깜빡임용)
    glow = Canvas(LW, LH)
    fx, fy = cx, cy - 24
    for y in range(LH):
        for x in range(LW):
            d = math.hypot(x - fx, (y - fy) * 1.15)
            a = int(120 * max(0, 1 - d / 210) ** 2.2)
            if a > 2:
                glow.p(x, y, (255, 190, 100, a))
    save(glow, 'creation_glow')


# ─── 신청서 ─────────────────────────────────────────────────

def paper():
    rnd = random.Random(17)
    cv = Canvas(PW, PH)
    # 종이 모양: 가장자리가 살짝 울퉁불퉁
    left = [6 + int(2 * math.sin(y / 23) + rnd.random() * 1.3) for y in range(PH)]
    right = [PW - 7 - int(2 * math.sin(y / 29 + 1) + rnd.random() * 1.3) for y in range(PH)]
    top = [5 + int(2 * math.sin(x / 31) + rnd.random() * 1.2) for x in range(PW)]
    bot = [PH - 6 - int(2 * math.sin(x / 19 + 2) + rnd.random() * 1.2) for x in range(PW)]
    inside = lambda x, y: left[y] <= x <= right[y] and top[x] <= y <= bot[x]
    # 그림자
    for y in range(PH):
        for x in range(PW):
            if 0 <= x - 4 < PW and 0 <= y - 6 < PH and inside(x - 4, y - 6) and not inside(x, y):
                cv.p(x, y, (0, 0, 0, 90))
    base, light, dark = hexc('e8d6ae'), hexc('f4e6c4'), hexc('c8ae80')
    for y in range(PH):
        for x in range(PW):
            if not inside(x, y): continue
            edge = min(x - left[y], right[y] - x, y - top[x], bot[x] - y)
            n = (math.sin(x * 0.7) * math.cos(y * 0.53) + math.sin(x * 0.13 + y * 0.21)) * 0.5
            c = mix(base, light, max(0, n) * 0.6) if n > 0 else mix(base, dark, -n * 0.35)
            if edge < 10: c = mix(c, hexc('b8945e'), (10 - edge) / 10 * 0.7)
            if edge < 2: c = hexc('8a6438')
            cv.p(x, y, c)
    # 종이 얼룩 (점)
    for _ in range(500):
        x, y = rnd.randrange(PW), rnd.randrange(PH)
        if inside(x, y): cv.p(x, y, shade(cv.get(x, y), 0.95))

    cx = PW // 2
    # 머리 장식: 작은 방패 문장 + 덩굴선
    em = Image.open(os.path.join(ROOT, 'Assets', 'Art', 'Title', 'title_emblem.png')).convert('RGBA')
    small = Canvas(36, 36)
    # 문장을 반으로 줄여 잉크 한 색으로 (도장처럼)
    src = em.resize((36, 36), Image.NEAREST).load()
    for y in range(36):
        for x in range(36):
            if src[x, y][3] > 128:
                lum = sum(src[x, y][:3]) / 3
                small.p(x, y, INK if lum < 120 else INK_L if lum < 190 else hexc('a07850'))
    cv.paste(small, cx - 18, 14)
    for side in (-1, 1):
        for i in range(70):
            x = cx + side * (26 + i)
            y = 34 + int(math.sin(i / 7) * 3)
            cv.p(x, y, INK_L)
            if i % 14 == 7: disc(cv, x, y - 3 * (1 if (i // 14) % 2 else -1), 1.5, INK_L)
    # 제목
    text(cv, '신규 길드 신청서', 32, cx, 60, INK, center=True, shadow=hexc('c8ae80'))
    cv.rect(46, 98, PW - 47, 98, INK_L); cv.rect(46, 101, PW - 47, 101, INK_L)
    for x in (46, PW - 47): disc(cv, x, 99.5, 2, INK)
    text(cv, '모험가 협회 귀중', 16, cx, 108, INK_L, center=True)

    # 칸: 길드 이름 (값은 입력칸)
    text(cv, '길드 이름', 16, 34, 140, INK)
    cv.rect(34, NAME_LINE_Y, PW - 35, NAME_LINE_Y, INK_L)
    for x in range(34, PW - 34, 4): cv.p(x, NAME_LINE_Y + 1, hexc('a07850'))
    # 칸: 창립일 · 초기 지원 (값은 TMP)
    text(cv, '창립일', 16, 34, 196, INK)
    cv.rect(104, 214, 200, 214, hexc('a07850'))
    text(cv, '초기 지원', 16, 34, 224, INK)
    cv.rect(118, 242, PW - 35, 242, hexc('a07850'))

    # 규약
    cv.rect(30, 258, PW - 31, 258, INK_L)
    text(cv, '[ 협회 규약 ]', 16, cx, 266, INK, center=True)
    rules = ['1. 길드는 협회의 규약을 따른다.',
             '2. 모험가의 급여는 매달 지급한다.',
             '3. 급여를 못 주면 길드는 해산한다.',
             '4. 동료를 버리지 않는다.']
    for i, line in enumerate(rules):
        text(cv, line, 16, 38, 286 + i * 20, INK_L)
    cv.rect(30, 368, PW - 31, 368, INK_L)

    # 서명
    text(cv, '길드 마스터 서명', 16, 34, 376, INK)
    cv.rect(34, SIGN_LINE_Y, 230, SIGN_LINE_Y, INK_L)
    text(cv, 'X', 16, 36, SIGN_LINE_Y - 18, INK_L)
    # 도장 자리 (점선 원)
    sx, sy = 290, 414
    for a in range(0, 360, 10):
        cv.p(sx + math.cos(math.radians(a)) * 30, sy + math.sin(math.radians(a)) * 30, hexc('b89870'))
    text(cv, '인', 16, sx, sy - 8, hexc('b89870'), center=True)
    save(cv, 'creation_paper')


def stamp():
    """빨간 '승인' 도장 자국 (거칠게 찍힌 느낌)"""
    rnd = random.Random(5)
    S = 72
    cv = Canvas(S, S)
    c = S / 2
    for y in range(S):
        for x in range(S):
            d = math.hypot(x - c + 0.5, y - c + 0.5)
            if 29 <= d <= 33 or 24 <= d <= 25.5:
                if rnd.random() < 0.88: cv.p(x, y, RED_INK)
    m = T.text_mask('승인', 16)
    mw, mh = m.size
    big = m.resize((mw * 2, mh * 2), Image.NEAREST).load()
    ox, oy = int(c - mw), int(c - mh)
    for y in range(mh * 2):
        for x in range(mw * 2):
            if big[x, y] > 127 and rnd.random() < 0.92:
                cv.p(ox + x, oy + y, RED_INK)
    # 위아래 별
    for dy in (-17, 17):
        for dx in (-1, 0, 1): cv.p(c + dx, c + dy, RED_INK)
        cv.p(c, c + dy - 1, RED_INK); cv.p(c, c + dy + 1, RED_INK)
    save(cv, 'creation_stamp')


def signature():
    """서명 낙서 (가로로 펼쳐지며 그려짐)"""
    W, H = 180, 40
    cv = Canvas(W, H)
    pts = []
    for i in range(240):
        t = i / 239
        x = 6 + t * (W - 16)
        y = H / 2 + math.sin(t * 19) * 9 * (1 - t * 0.5) + math.sin(t * 7 + 1) * 5
        if 0.35 < t < 0.42: y -= (t - 0.35) * 160      # 높은 획
        pts.append((x, y))
    for a, b in zip(pts, pts[1:]):
        cv.line(a, b, INK)
        cv.line((a[0], a[1] + 1), (b[0], b[1] + 1), INK)
    cv.line((20, H - 6), (W - 30, H - 9), INK)          # 밑줄 획
    save(cv, 'creation_signature')


def quill():
    """깃펜 (펜촉이 왼쪽 아래)"""
    W, H = 22, 52
    cv = Canvas(W, H)
    # 깃대
    for i in range(46):
        cv.p(3 + i * 0.3, H - 3 - i, hexc('e8dcc0'))
    # 깃털 (오른쪽 위로 넓게)
    for i in range(14, 50):
        t = (i - 14) / 36
        w = int(math.sin(t * math.pi) * 8) + 1
        x0 = 3 + i * 0.3
        for k in range(1, w + 1):
            col = hexc('f4ece0') if k < w * 0.6 else hexc('c8bca8')
            cv.p(x0 + k, H - 3 - i + k * 0.4, col)
            if k < 4: cv.p(x0 - k * 0.6, H - 3 - i + k * 0.3, hexc('d8cebe'))
        if i % 5 == 0: cv.p(x0 + w, H - 3 - i + w * 0.4, hexc('8a7e6a'))
    # 펜촉
    cv.p(2, H - 2, INK); cv.p(3, H - 3, INK); cv.p(2, H - 3, hexc('1e1a20')); cv.p(1, H - 1, INK)
    cv.outline(hexc('2a1810'))
    save(cv, 'creation_quill')


def ink_drop():
    cv = Canvas(4, 4)
    disc(cv, 1.5, 1.5, 1.6, RED_INK)
    save(cv, 'creation_ink')


def small_button(name, face, face_l, border):
    """작은 버튼 (신청서 안 '추천' 버튼, 2배 표시)"""
    W, H = 40, 16
    cv = Canvas(W, H)
    cv.rect(1, 1, W - 2, H - 1, hexc('1a0f0a'))
    cv.rect(0, 0, W - 1, H - 2, hexc('1a0f0a'))
    cv.rect(1, 1, W - 2, H - 3, face)
    cv.rect(1, 1, W - 2, 1, face_l)
    cv.rect(1, H - 3, W - 2, H - 3, shade(face, 0.7))
    cv.p(2, 2, border); cv.p(W - 3, 2, border); cv.p(2, H - 4, border); cv.p(W - 3, H - 4, border)
    save(cv, name)


def preview():
    base = Image.new('RGBA', (LW, LH), (0, 0, 0, 255))
    for n in ('creation_desk', 'creation_glow'):
        base.alpha_composite(Image.open(os.path.join(OUT, n + '.png')))
    full = base.resize((LW * 4, LH * 4), Image.NEAREST)
    pp = Image.open(os.path.join(OUT, 'creation_paper.png'))
    pp2 = pp.resize((PW * 2, PH * 2), Image.NEAREST)
    px = LW * 2 - PW - 120
    py = (LH * 4 - PH * 2) // 2 + 6
    full.alpha_composite(pp2, (px, py))
    st = Image.open(os.path.join(OUT, 'creation_stamp.png')).resize((144, 144), Image.NEAREST)
    full.alpha_composite(st, (px + 290 * 2 - 72, py + 414 * 2 - 72))
    sg = Image.open(os.path.join(OUT, 'creation_signature.png')).resize((360, 80), Image.NEAREST)
    full.alpha_composite(sg, (px + 40 * 2, py + (SIGN_LINE_Y - 36) * 2))
    bt = Image.open(os.path.join(OUT, 'creation_button_sign.png')).resize((352, 72), Image.NEAREST)
    full.alpha_composite(bt, (1460, 520))
    q = Image.open(os.path.join(OUT, 'creation_quill.png')).resize((88, 208), Image.NEAREST)
    full.alpha_composite(q, (430 * 4 - 10, 222 * 4 - 230))
    full.crop((OX * 4, OY * 4, OX * 4 + 1920, OY * 4 + 1080)).save(os.path.join(ROOT, 'Docs', 'creation_preview.png'))


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    desk(); paper(); stamp(); signature(); quill(); ink_drop()
    T.OUT = OUT   # 타이틀 생성기의 버튼 그리기를 빌려 씀 (저장 위치만 바꿈)
    T.button('creation_button_sign', hexc('9a2a22'), hexc('d0544a'), hexc('f2d488'))
    T.button('creation_button_sign_hover', hexc('b83a2e'), hexc('f07a6a'), hexc('fff0b0'))
    T.button('creation_button_sign_pressed', hexc('7a1e18'), hexc('a8322a'), hexc('d4a84a'), press=True)
    small_button('creation_button_small', hexc('7a4a2a'), hexc('a06a3c'), hexc('d4a84a'))
    small_button('creation_button_small_hover', hexc('9a6236'), hexc('d49a5a'), hexc('fff0b0'))
    preview()
    print('ok')
