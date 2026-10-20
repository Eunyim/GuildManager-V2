# 도트 그림 공통 도구 (로비·거리 그림 생성기가 같이 씀)
# 아이소메트릭: 바닥 한 칸 = 64x32px (Unity 격자 1 x 0.5, PPU 64)
import os
from PIL import Image, ImageDraw


def hexc(h, a=255):
    h = h.lstrip('#')
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


def shade(c, f):
    """f<1 어둡게, f>1 밝게 (알파 유지)"""
    return tuple(max(0, min(255, int(round(v * f)))) for v in c[:3]) + (c[3],)


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3)) + (255,)


# ─── 팔레트 (따뜻한 중세 판타지) ───────────────────────────────
P = {
    'outline': hexc('1a0f0a'),
    'wood_d': hexc('4a2c18'), 'wood': hexc('7a4a2a'), 'wood_l': hexc('a06a3c'), 'wood_ll': hexc('c48a52'),
    'dark_wood_d': hexc('2e1a10'), 'dark_wood': hexc('4e3020'), 'dark_wood_l': hexc('6a4430'),
    'stone_d': hexc('3e3a40'), 'stone': hexc('5e5860'), 'stone_l': hexc('807a82'), 'stone_ll': hexc('a49ea4'),
    'plaster_d': hexc('a08c70'), 'plaster': hexc('c8b494'), 'plaster_l': hexc('e0cfae'),
    'gold_d': hexc('8a6420'), 'gold': hexc('d4a84a'), 'gold_l': hexc('f2d488'),
    'iron_d': hexc('2e3038'), 'iron': hexc('545866'), 'iron_l': hexc('8a90a0'),
    'paper_d': hexc('c8b088'), 'paper': hexc('eadcb8'), 'paper_l': hexc('fff4dc'),
    'red_d': hexc('6a1a18'), 'red': hexc('a8322a'), 'red_l': hexc('d8584a'),
    'blue_d': hexc('1e2a5a'), 'blue': hexc('34488e'), 'blue_l': hexc('5a74c0'),
    'green_d': hexc('1e4a24'), 'green': hexc('3a7a38'), 'green_l': hexc('6aae52'),
    'purple': hexc('6a3a8e'),
    'flame_d': hexc('c8401a'), 'flame': hexc('f08a24'), 'flame_l': hexc('ffd860'), 'flame_ll': hexc('fff6c0'),
    'glass': hexc('3a5a7a'), 'glass_l': hexc('8ab8d8'),
    'foam': hexc('fff8e8'), 'beer': hexc('d89a2a'),
    'shadow': (0, 0, 0, 90),
    'clear': (0, 0, 0, 0),
}


class Canvas:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.im = Image.new('RGBA', (w, h), (0, 0, 0, 0))
        self.px = self.im.load()
        self.dr = ImageDraw.Draw(self.im)

    def p(self, x, y, c):
        x, y = int(round(x)), int(round(y))
        if 0 <= x < self.w and 0 <= y < self.h:
            if c[3] == 255 or self.px[x, y][3] == 0:
                self.px[x, y] = c
            else:  # 반투명은 섞음
                base = self.px[x, y]
                t = c[3] / 255
                self.px[x, y] = tuple(int(base[i] * (1 - t) + c[i] * t) for i in range(3)) + (max(base[3], c[3]),)

    def get(self, x, y):
        if 0 <= x < self.w and 0 <= y < self.h:
            return self.px[x, y]
        return (0, 0, 0, 0)

    def rect(self, x0, y0, x1, y1, c):
        for y in range(int(y0), int(y1) + 1):
            for x in range(int(x0), int(x1) + 1):
                self.p(x, y, c)

    def poly(self, pts, c):
        self.dr.polygon([(round(x), round(y)) for x, y in pts], fill=c)

    def line(self, a, b, c):
        self.dr.line([(round(a[0]), round(a[1])), (round(b[0]), round(b[1]))], fill=c, width=1)

    def outline(self, c=None, inner=False):
        """바깥 테두리: 투명 칸 중 칠해진 칸과 상하좌우로 맞닿은 칸을 c로"""
        c = c or P['outline']
        src = self.im.copy().load()
        for y in range(self.h):
            for x in range(self.w):
                if src[x, y][3] != 0:
                    continue
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < self.w and 0 <= ny < self.h and src[nx, ny][3] > 128:
                        self.px[x, y] = c
                        break

    def paste(self, other, x, y):
        self.im.alpha_composite(other.im, (int(x), int(y)))

    def save(self, path):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        self.im.save(path)


class Iso:
    """아이소메트릭 좌표: o = 바닥 앞 꼭짓점(그림 좌표). a축 = 오른쪽 위(+32,-16), b축 = 왼쪽 위(-32,-16), h = 위로(px)"""

    def __init__(self, cv, ox, oy):
        self.cv, self.ox, self.oy = cv, ox, oy

    def pt(self, a, b, h=0):
        return (int(round(self.ox + 32 * a - 32 * b)), int(round(self.oy - 16 * a - 16 * b - h)))

    def box(self, a0, b0, a1, b1, h0, h1, top, left, right, edge=None, top_edge=True):
        """상자: 윗면(top), 왼쪽 앞면(a=a0), 오른쪽 앞면(b=b0)"""
        pt = self.pt
        cv = self.cv
        cv.poly([pt(a0, b0, h0), pt(a0, b1, h0), pt(a0, b1, h1), pt(a0, b0, h1)], left)
        cv.poly([pt(a0, b0, h0), pt(a1, b0, h0), pt(a1, b0, h1), pt(a0, b0, h1)], right)
        cv.poly([pt(a0, b0, h1), pt(a1, b0, h1), pt(a1, b1, h1), pt(a0, b1, h1)], top)
        if edge:
            # 앞쪽 세로 모서리와 윗면 앞 모서리를 밝게/어둡게 해서 입체감
            cv.line(pt(a0, b0, h0), pt(a0, b0, h1), edge)
            if top_edge:
                cv.line(pt(a0, b0, h1), pt(a1, b0, h1), shade(top, 1.15))
                cv.line(pt(a0, b0, h1), pt(a0, b1, h1), shade(top, 1.15))

    def face_lines_a(self, b, a0, a1, h0, h1, step, c):
        """오른쪽 앞면(b 고정)에 가로줄 (판자)"""
        h = h0 + step
        while h < h1:
            self.cv.line(self.pt(a0, b, h), self.pt(a1, b, h), c)
            h += step

    def face_lines_b(self, a, b0, b1, h0, h1, step, c):
        """왼쪽 앞면(a 고정)에 가로줄"""
        h = h0 + step
        while h < h1:
            self.cv.line(self.pt(a, b0, h), self.pt(a, b1, h), c)
            h += step

    def top_lines_a(self, a0, b0, a1, b1, h, step, c):
        """윗면에 a축 방향 줄 (판자)"""
        b = b0 + step
        while b < b1 - 1e-6:
            self.cv.line(self.pt(a0, b, h), self.pt(a1, b, h), c)
            b += step

    def shadow(self, a0, b0, a1, b1, grow=0.15):
        pt = self.pt
        self.cv.poly([pt(a0 - grow, b0 - grow), pt(a1 + grow, b0 - grow), pt(a1 + grow, b1 + grow), pt(a0 - grow, b1 + grow)], P['shadow'])


def flame(cv, x, y, frame, size=1):
    """작은 불꽃 (x,y = 밑동). frame 0~3으로 흔들림"""
    sway = [0, 1, 0, -1][frame % 4]
    hgt = [5, 6, 5, 4][frame % 4] * size
    for i in range(hgt):
        t = i / max(1, hgt - 1)
        w = max(0, int(round((1 - t) * 1.6 * size)))
        cx = x + (sway if t > 0.5 else 0)
        col = P['flame_ll'] if t < 0.3 and w > 0 else P['flame_l'] if t < 0.6 else P['flame']
        for dx in range(-w, w + 1):
            cv.p(cx + dx, y - i, col if abs(dx) < w or w == 0 else P['flame_d'])


def save_with_pivot(cv, out_dir, name, pivot_px, pivots):
    """pivot_px = 그림 좌표(왼쪽 위 원점)의 기준점 → Unity 피벗(왼쪽 아래 원점, 0~1)으로 기록"""
    path = os.path.join(out_dir, name + '.png')
    cv.save(path)
    pivots[name] = (pivot_px[0] / cv.w, 1 - pivot_px[1] / cv.h)


def write_pivots(out_dir, pivots):
    with open(os.path.join(out_dir, 'pivots.txt'), 'w', encoding='utf-8') as f:
        f.write('# 이름 피벗x 피벗y (0~1, 왼쪽 아래 기준) — Tools/lobby_art_gen.py가 만듦, Editor/PixelArtImporter가 읽음\n')
        for k, (x, y) in sorted(pivots.items()):
            f.write(f'{k} {x:.5f} {y:.5f}\n')
