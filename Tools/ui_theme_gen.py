# 판타지 픽셀 UI 테마 스프라이트 생성기
# 실행: python Tools/ui_theme_gen.py  → Assets/Resources/UI/Theme/*.png
# 9-슬라이스 테두리 값은 Assets/Editor/UIThemeSpriteImporter.cs의 표와 맞춰야 함
import os
from PIL import Image

OUT = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'Resources', 'UI', 'Theme')

def hexc(h, a=255):
    h = h.lstrip('#'); return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)

CLEAR = (0, 0, 0, 0)
OUTLINE = hexc('120a06')

def ring_shaded(px, size, r, light, dark, corner=None):
    """r번째 테두리 고리: 위·왼쪽은 light, 아래·오른쪽은 dark"""
    n = size - 1 - r
    for i in range(r, n + 1):
        px[i, r] = light; px[r, i] = light
        px[i, n] = dark;  px[n, i] = dark
    if corner is not None:
        px[n, r] = corner; px[r, n] = corner

def fill(px, size, r, c):
    for y in range(r, size - r):
        for x in range(r, size - r):
            px[x, y] = c

def cut_corners(px, size):
    for x, y in [(0, 0), (size - 1, 0), (0, size - 1), (size - 1, size - 1)]:
        px[x, y] = CLEAR

def panel():
    # 금테 두른 나무 창틀 (24x24, 테두리 8)
    s = 24; im = Image.new('RGBA', (s, s), CLEAR); px = im.load()
    fill(px, s, 0, OUTLINE)
    ring_shaded(px, s, 1, hexc('8a5a34'), hexc('4a2c18'), hexc('6b4226'))
    ring_shaded(px, s, 2, hexc('6b4226'), hexc('3c2414'), hexc('553420'))
    ring_shaded(px, s, 3, hexc('f2d488'), hexc('9c7428'), hexc('d4a84a'))
    ring_shaded(px, s, 4, hexc('1b110b'), hexc('1b110b'))
    ring_shaded(px, s, 5, hexc('22170f'), hexc('2a1c13'))
    fill(px, s, 6, hexc('2c1e15'))
    cut_corners(px, s)
    # 네 모서리 금 장식 (다이아몬드)
    for cx, cy in [(2, 2), (s - 3, 2), (2, s - 3), (s - 3, s - 3)]:
        for dx, dy in [(0, 0), (1, 0), (-1, 0), (0, 1), (0, -1)]:
            px[cx + dx, cy + dy] = hexc('d4a84a')
        px[cx, cy] = hexc('fff0b8')
    return im

def panel_small():
    # 툴팁용 얇은 금테 (12x12, 테두리 4)
    s = 12; im = Image.new('RGBA', (s, s), CLEAR); px = im.load()
    fill(px, s, 0, OUTLINE)
    ring_shaded(px, s, 1, hexc('f2d488'), hexc('9c7428'), hexc('d4a84a'))
    ring_shaded(px, s, 2, hexc('1b110b'), hexc('1b110b'))
    fill(px, s, 3, hexc('241912'))
    cut_corners(px, s)
    return im

def inset():
    # 안쪽으로 파인 홈 (12x12, 테두리 3): 목록 칸, 초상화 틀, 입력 칸
    s = 12; im = Image.new('RGBA', (s, s), CLEAR); px = im.load()
    ring_shaded(px, s, 0, hexc('0f0905'), hexc('5a3e2a'), hexc('3a2819'))
    ring_shaded(px, s, 1, hexc('170f09'), hexc('34241a'), hexc('241912'))
    fill(px, s, 2, hexc('20160f'))
    return im

BUTTONS = {
    # 이름: (밝은 면, 얼굴, 어두운 면, 아래 턱)
    'wood':  ('c48a52', '8e5c32', '5f3c20', '3f2614'),
    'stone': ('948a80', '635b54', '433c36', '2b2622'),
    'green': ('9ad874', '4f9a3a', '2f6624', '1d4216'),
    'red':   ('e6846a', 'a8382a', '6e2018', '4a130e'),
    'gold':  ('fff0b0', 'd9a840', '9c7222', '6b4c14'),
}

def button(light, face, dark, lip, pressed=False):
    # 입체 버튼 (16x16, 테두리 5). 눌린 모양은 아래 턱이 없고 얼굴이 한 칸 내려감
    s = 16; im = Image.new('RGBA', (s, s), CLEAR); px = im.load()
    L, F, D, P = hexc(light), hexc(face), hexc(dark), hexc(lip)
    fill(px, s, 0, OUTLINE)
    bottom = s - 2  # 마지막 안쪽 줄
    lip_rows = 0 if pressed else 2
    top = 2 if pressed else 1
    for y in range(top, bottom + 1):
        for x in range(1, s - 1):
            if y > bottom - lip_rows: c = P
            elif y == top: c = L
            elif x == 1: c = L
            elif x == s - 2 or y == bottom - lip_rows: c = D
            else: c = F
            px[x, y] = c
    if pressed:
        for x in range(1, s - 1): px[x, 1] = hexc('0b0604')
    cut_corners(px, s)
    return im

def knob():
    # 스크롤바 손잡이 (8x8, 테두리 3)
    s = 8; im = Image.new('RGBA', (s, s), CLEAR); px = im.load()
    fill(px, s, 0, OUTLINE)
    ring_shaded(px, s, 1, hexc('f2d488'), hexc('9c7428'), hexc('d4a84a'))
    fill(px, s, 2, hexc('c9973e'))
    cut_corners(px, s)
    return im

def hud_bar():
    # 상단 정보줄 띠 (16x16, 아래 테두리 4): 어두운 나무판 + 금 줄
    s = 16; im = Image.new('RGBA', (s, s), CLEAR); px = im.load()
    for y in range(s):
        for x in range(s):
            if y >= s - 1: c = OUTLINE
            elif y >= s - 3: c = hexc('d4a84a') if y == s - 3 else hexc('8a6420')
            elif y == s - 4: c = hexc('1a100a')
            else: c = hexc('3a2416') if y % 5 else hexc('2e1c10')
            px[x, y] = c
    return im


def save(im, name):
    im.save(os.path.join(OUT, name + '.png'))

os.makedirs(OUT, exist_ok=True)
save(panel(), 'ui_panel')
save(panel_small(), 'ui_panel_small')
save(inset(), 'ui_inset')
save(knob(), 'ui_knob')
save(hud_bar(), 'ui_hud_bar')
for k, v in BUTTONS.items():
    save(button(*v), 'ui_button_' + k)
    save(button(*v, pressed=True), 'ui_button_' + k + '_pressed')

# 미리보기 (확인용, 저장소에는 넣지 않음)
if __name__ == '__main__' and os.environ.get('PREVIEW'):
    names = ['ui_panel', 'ui_panel_small', 'ui_inset', 'ui_knob'] + ['ui_button_' + k for k in BUTTONS] + ['ui_button_' + k + '_pressed' for k in BUTTONS]
    sc = 8; pad = 8; W = sum(Image.open(os.path.join(OUT, n + '.png')).width * sc + pad for n in names)
    sheet = Image.new('RGBA', (W, 24 * sc), hexc('000000'))
    x = 0
    for n in names:
        im = Image.open(os.path.join(OUT, n + '.png'))
        sheet.paste(im.resize((im.width * sc, im.height * sc), Image.NEAREST), (x, 0)); x += im.width * sc + pad
    sheet.save(os.environ['PREVIEW'])
