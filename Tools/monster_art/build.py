# 몬스터 스프라이트 시트 생성
#   python Tools/monster_art/build.py              → 전부
#   python Tools/monster_art/build.py goblin orc   → 일부만
#   MONSTER_PREVIEW=폴더 를 주면 4배 확대 미리보기도 저장
# 출력: Assets/Art/Monsters/{key}.png (행 = 동작×방향, 열 = 프레임) + {key}.json (칸 크기, 발 기준점, PPU, 행 정보)
# Unity: Tools > 몬스터 도트 스프라이트 적용 이 json을 읽어 시트를 자르고 프리팹에 연결
import json
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from core import Canvas  # noqa: E402
import humanoid  # noqa: E402
import specs  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, 'Assets', 'Art', 'Monsters')
PX_PER_UNIT = 48          # 화면에서 1유닛에 들어가는 도트 수 (프리팹 크기와 상관없이 같게 맞춤)
ANIMS = [('idle', 4, 6.0, True), ('walk', 6, 10.0, True), ('attack', 6, 12.0, False), ('death', 6, 9.0, False)]
DIRS = ['side', 'front', 'back']
WORK = 260                # 작업 캔버스 (그린 뒤 전체 프레임 공통 영역으로 잘라냄)

# 시트 → 적용할 프리팹 (Assets/Prefabs/Characters). PPU는 프리팹 크기에 맞춰 Unity 쪽에서 계산
PREFABS = {
    'goblin': 'Enemy_Goblin', 'goblin_warrior': 'Enemy_GoblinWarrior', 'goblin_hunter': 'Enemy_GoblinHunter',
    'goblin_priest': 'Enemy_GoblinPriest', 'goblin_shaman_king': 'Enemy_GoblinShamanKing',
    'orc': 'Enemy_Orc', 'orc_grunt': 'Enemy_OrcGrunt', 'orc_centurion': 'Enemy_OrcCenturion', 'orc_rider': 'Enemy_OrcRider',
    'prairie_wolf': 'Enemy_PrairieWolf', 'alpha_wolf': 'Enemy_AlphaWolf',
    'bandit': 'Enemy_Bandit', 'bandit_archer': 'Enemy_BanditArcher', 'bandit_warrior': 'Enemy_BanditWarrior',
    'skeleton': 'Enemy_Skeleton', 'skeleton_warrior': 'Enemy_SkeletonWarrior', 'skeleton_archer': 'Enemy_SkeletonArcher',
    'skeleton_tank': 'Enemy_SkeletonTank', 'skeleton_necromancer': 'Enemy_SkeletonNecromancer',
    'dragon': 'Enemy_Dragon', 'baby_dragon': 'Enemy_BabyDragon', 'dragon_hatchling': 'Enemy_DragonHatchling',
    'slime': 'Enemy_Slime', 'king_slime': 'Enemy_KingSlime', 'mimic': 'Enemy_Mimic',
}


def render(key, spec):
    kind = spec.get('kind', 'humanoid')
    if kind == 'humanoid':
        mod = humanoid
    elif kind in ('quadruped', 'dragon', 'rider'):
        import beasts as mod
    else:
        import blobs as mod
    ox, oy = WORK // 2, int(WORK * 0.72)
    frames = {}
    for anim, count, _, _ in ANIMS:
        for d in DIRS:
            ps = mod.poses(spec, anim, d)
            assert len(ps) == count, (key, anim, d, len(ps))
            imgs = []
            for p in ps:
                cv = Canvas(WORK, WORK)
                mod.draw(cv, spec, p, d, ox, oy)
                cv.outline()
                imgs.append(cv.image())
            frames[(anim, d)] = imgs
    # 모든 프레임을 덮는 공통 영역
    box = None
    for imgs in frames.values():
        for im in imgs:
            b = im.getbbox()
            if b is None:
                continue
            box = b if box is None else (min(box[0], b[0]), min(box[1], b[1]), max(box[2], b[2]), max(box[3], b[3]))
    # 발 기준점이 칸 가운데 오도록 좌우 대칭으로 넓힘 (좌우 뒤집어도 기준점이 같게)
    half = max(ox - box[0], box[2] - ox) + 1
    x0, x1 = ox - half, ox + half
    y0, y1 = box[1] - 1, max(box[3], oy + 1) + 1
    cw, ch = x1 - x0, y1 - y0
    cols = max(c for _, c, _, _ in ANIMS)
    sheet = Image.new('RGBA', (cw * cols, ch * len(ANIMS) * len(DIRS)), (0, 0, 0, 0))
    rows = []
    r = 0
    for anim, count, fps, loop in ANIMS:
        for d in DIRS:
            for i, im in enumerate(frames[(anim, d)]):
                sheet.alpha_composite(im.crop((x0, y0, x1, y1)), (i * cw, r * ch))
            rows.append(dict(anim=anim, dir=d, count=count, fps=fps, loop=loop))
            r += 1
    fb = frames[('idle', 'front')][0].getbbox()
    meta = dict(key=key, prefab=PREFABS.get(key, ''), cellW=cw, cellH=ch, pivotX=ox - x0, pivotY=oy - y0, pxPerUnit=PX_PER_UNIT,
                bodyPx=(fb[3] - fb[1]) if fb else ch, rows=rows)
    return sheet, meta, frames, (x0, y0, x1, y1)


def preview(key, frames, crop, path, scale=4):
    x0, y0, x1, y1 = crop
    cw, ch = x1 - x0, y1 - y0
    cols = 6
    rows = len(ANIMS) * len(DIRS)
    im = Image.new('RGBA', (cw * cols, ch * rows), (58, 54, 66, 255))
    # 바둑판 배경 (투명 확인용)
    arr = np.array(im)
    yy, xx = np.mgrid[0:arr.shape[0], 0:arr.shape[1]]
    arr[((xx // 4 + yy // 4) % 2) == 0, :3] = (66, 62, 76)
    im = Image.fromarray(arr)
    r = 0
    for anim, count, _, _ in ANIMS:
        for d in DIRS:
            for i, f in enumerate(frames[(anim, d)]):
                im.alpha_composite(f.crop(crop), (i * cw, r * ch))
            r += 1
    im = im.resize((im.width * scale, im.height * scale), Image.NEAREST)
    im.save(path)


def main(keys):
    os.makedirs(OUT, exist_ok=True)
    prev = os.environ.get('MONSTER_PREVIEW')
    for key in keys:
        spec = specs.SPECS[key]
        sheet, meta, frames, crop = render(key, spec)
        sheet.save(os.path.join(OUT, key + '.png'))
        with open(os.path.join(OUT, key + '.json'), 'w', encoding='utf-8') as f:
            json.dump(meta, f, ensure_ascii=False, indent=1)
        if prev:
            os.makedirs(prev, exist_ok=True)
            preview(key, frames, crop, os.path.join(prev, key + '.png'))
        print(key, f"{meta['cellW']}x{meta['cellH']}")


if __name__ == '__main__':
    args = sys.argv[1:]
    main(args if args else list(specs.SPECS.keys()))
