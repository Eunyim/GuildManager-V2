# 길드 바닥 나무 판자 타일 생성기
# 아이소메트릭 마름모 64x32 (PPU 64) 4종 → Assets/Art/Tiles/LobbyTileset/IsoFloorTiles/iso_gray0~3.png 덮어쓰기
# (파일 이름·GUID를 그대로 써서 씬 타일맵이 자동으로 바뀜. 이름은 예전 돌바닥 그대로 iso_gray)
#
# 판자는 방의 긴 쪽(격자 A축, 화면 오른쪽 위 방향)으로 길게 놓임. 한 칸에 판자 2장.
# 판자 경계는 칸 경계와 맞물려 어느 칸끼리 붙어도 이음새가 이어지고,
# 판자 끝 이음매(가로 홈)는 변형마다 다른 자리에 있어 무작위로 깔면 엇갈려 보임.
import os
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'Assets', 'Art', 'Tiles', 'LobbyTileset', 'IsoFloorTiles')
PREVIEW = os.environ.get('FLOOR_PREVIEW')  # 지정하면 깔아 본 미리보기 그림을 저장

W, H = 64, 32
PLANKS = 2  # 한 칸에 놓이는 판자 수

# 따뜻한 참나무 색 (어두움 → 밝음)
WOOD = [(0x3a, 0x22, 0x14), (0x5a, 0x36, 0x1e), (0x74, 0x47, 0x27), (0x8a, 0x56, 0x2f),
        (0x9e, 0x66, 0x38), (0xb4, 0x7a, 0x44), (0xc8, 0x8e, 0x52)]
SEAM = (0x2a, 0x18, 0x0e)
NAIL = (0x2e, 0x24, 0x20)
NAIL_L = (0x6a, 0x5c, 0x50)

BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]


def noise(x, y, p=0):
    n = (x * 374761393 + y * 668265263 + p * 2147483647) & 0xffffffff
    n = ((n ^ (n >> 13)) * 1274126177) & 0xffffffff
    return ((n ^ (n >> 16)) & 0xffff) / 65535.0


def smooth(t, p):
    """1차원 부드러운 값 노이즈 (나뭇결 흔들림용)"""
    i = int(t // 1)
    f = t - i
    f = f * f * (3 - 2 * f)
    return noise(i, 7, p) * (1 - f) + noise(i + 1, 7, p) * f


# 변형별 판자 끝 이음매 위치 (판자 길이 방향 v, None = 이음매 없음)와 판자 색조
VARIANTS = [
    {'joints': [None, 0.55], 'tone': [0.1, -0.5]},
    {'joints': [0.30, None], 'tone': [0.45, -0.1]},
    {'joints': [None, None], 'tone': [-0.4, 0.3]},
    {'joints': [0.75, 0.20], 'tone': [0.2, -0.3]},
]


def local(px, py):
    """화면 픽셀 중심 → 칸 좌표 (u: 판자를 가로지르는 축, v: 판자 길이 축), 0~1"""
    a = py / 16.0
    b = (px - 32.0) / 32.0
    return (a + b) / 2.0, (a - b) / 2.0


def wood_at(variant, px, py):
    u, v = local(px + 0.5, py + 0.5)
    u = min(max(u, 0.0), 0.9999)
    v = min(max(v, 0.0), 0.9999)
    var = VARIANTS[variant]
    k = int(u * PLANKS)                 # 몇 번째 판자
    fu = u * PLANKS - k                 # 판자 안에서 가로 위치 0~1
    seg = 0 if var['joints'][k] is None or v < var['joints'][k] else 1
    seed = variant * 31 + k * 7 + seg * 3

    # 나뭇결: 판자 길이 방향으로 흐르는 물결 줄무늬
    wobble = (smooth(v * 4.0, seed) - 0.5) * 0.45
    g = fu * 2.6 + wobble + noise(seed, 3) * 0.5
    grain = abs(g - round(g))           # 0 = 결 한가운데
    level = 3.5 + var['tone'][k] * (1 if seg == 0 else -0.7)
    if grain < 0.07:
        level -= 1.3                    # 짙은 결 (판자 길이 방향으로 길게 이어지는 선)
    elif grain < 0.13:
        level -= 0.5
    elif grain > 0.40:
        level += 0.35                   # 결 사이 밝은 면
    level += (noise(int(v * 14), int(fu * 10), seed) - 0.5) * 0.35  # 잔결 (길게 늘어진 얼룩)

    # 옹이 (변형 1, 3에 하나씩)
    if variant in (1, 3) and k == (variant // 2):
        ku, kv = 0.5, 0.62 if variant == 1 else 0.4
        du, dv = (fu - ku) * 2.2, (v - kv) * 7.0
        d = du * du + dv * dv
        if d < 0.10:
            level -= 1.6
        elif d < 0.30:
            level -= 0.7

    # 판자 모서리: 빛 받는 쪽(화면 위) 밝게, 반대쪽 어둡게 → 살짝 도드라진 판자
    if fu < 0.125:
        level += 1.4                    # 홈 바로 아래 한 줄: 위쪽 모서리 빛
    elif fu > 0.9375:
        level -= 1.0                    # 아래쪽 모서리 그늘

    # 판자 끝 이음매 근처는 살짝 어둡게
    j = var['joints'][k]
    if j is not None and abs(v - j) < 0.05:
        level -= 0.6

    # 디더링으로 두 단계 사이를 섞음
    lo = int(level // 1)
    frac = level - lo
    if frac * 16 > BAYER[py % 4][px % 4]:
        lo += 1
    return WOOD[max(0, min(len(WOOD) - 1, lo))], (u, v, k, fu, j)


def make_tile(variant, mask):
    im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    px_ = im.load()
    for y in range(H):
        for x in range(W):
            if not mask[y][x]:
                continue
            c, (u, v, k, fu, j) = wood_at(variant, x, y)
            px_[x, y] = c + (255,)

    # 판자 사이 홈 (u = k/PLANKS 선, 칸 경계 u=0 포함). 2:1 아이소 선이라 픽셀이 깔끔하게 이어짐
    for y in range(H):
        for x in range(W):
            if not mask[y][x]:
                continue
            u, v = local(x + 0.5, y + 0.5)
            for k in range(PLANKS):
                b = k / PLANKS
                # 화면에서 선 두께 1px: u가 b 바로 위인 줄
                if b <= u < b + 1.0 / 32.0 * 1.0 + 0.0001:
                    px_[x, y] = SEAM + (255,)
            # 판자 끝 이음매 (v 일정한 선)
            uu = min(max(u, 0.0), 0.9999)
            kk = int(uu * PLANKS)
            j = VARIANTS[variant]['joints'][kk]
            if j is not None and j <= v < j + 1.0 / 32.0:
                px_[x, y] = SEAM + (255,)

    # 못: 이음매 양옆에 두 개씩
    for k in range(PLANKS):
        j = VARIANTS[variant]['joints'][k]
        if j is None:
            continue
        for dv in (-0.07, 0.09):
            for fu in (0.55,):
                u = (k + fu) / PLANKS
                vv = j + dv
                x = int(32 + 32 * (u - vv))
                y = int(16 * (u + vv))
                if 0 <= x < W and 0 <= y < H and mask[y][x]:
                    px_[x, y] = NAIL + (255,)
                    if x + 1 < W and mask[y][x + 1]:
                        px_[x + 1, y] = NAIL_L + (255,)
    return im


def load_mask():
    path = os.path.join(OUT, 'iso_gray0.png')
    if os.path.exists(path):
        a = Image.open(path).convert('RGBA').split()[3]
        return [[a.getpixel((x, y)) > 0 for x in range(W)] for y in range(H)]
    # 기본 마름모
    return [[abs(x + 0.5 - 32) / 32 + abs(y + 0.5 - 16) / 16 <= 1.0 for x in range(W)] for y in range(H)]


def preview(tiles, path, cols=8, rows=8, scale=3):
    import random
    rnd = random.Random(1234)
    pw, ph = (cols + rows) * 32 + 64, (cols + rows) * 16 + 32
    im = Image.new('RGBA', (pw, ph), (12, 10, 14, 255))
    for r in range(rows):
        for c in range(cols):
            t = tiles[rnd.randrange(len(tiles))]
            # 칸 (c, r): u축 = c, v축 = r (local()과 같은 방향)
            x = (c - r) * 32 + rows * 32
            y = (c + r) * 16
            im.alpha_composite(t, (x, y))
    im = im.resize((pw * scale, ph * scale), Image.NEAREST)
    im.save(path)


if __name__ == '__main__':
    mask = load_mask()
    tiles = [make_tile(i, mask) for i in range(len(VARIANTS))]
    for i, t in enumerate(tiles):
        t.save(os.path.join(OUT, f'iso_gray{i}.png'))
    if PREVIEW:
        preview(tiles, PREVIEW)
    print('나무 바닥 타일', len(tiles), '종 저장:', OUT)
