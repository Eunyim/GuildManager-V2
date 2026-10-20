# 모험가 부위 시트 생성
#   python Tools/adventurer_art/build.py            → 전부 내보내기 (Assets/Resources/Adventurers)
#   python Tools/adventurer_art/build.py preview    → 무작위 조합 미리보기만 (ADV_PREVIEW 폴더)
#
# 부위 하나 = {id}.bytes (PNG 내용, Unity에서 TextAsset으로 읽음) + {id}_meta.json (조각 위치)
#   조각(tile) = 한 프레임의 한 층. row(예: idle_side, warrior_attack_front), f(프레임), s(층 0~4), 아틀라스 위치, 칸 안의 위치
# 다시 칠할 색(KEY)은 R=255·B=255·G=0/50/100/150/200 (명암 0~4). 빛·효과 픽셀은 알파 254 (외곽선 제외)
import json
import math
import os
import random
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import parts as PD  # noqa: E402
import rig  # noqa: E402
from core import ramp  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, 'Assets', 'Resources', 'Adventurers')
CW, CH = 96, 72           # 칸 크기
OX, OY = 48, 64           # 칸 안의 발 밑 기준점
PX_PER_UNIT = 48
DIRS = ['side', 'front', 'back']
SHARED = [('idle', 4, 6.0, True), ('walk', 6, 10.0, True), ('death', 6, 9.0, False), ('sit', 4, 3.0, True)]
JOBANIM = [('attack', 6, 12.0, False), ('skill', 6, 10.0, False)]


def rows_for(job=None):
    """(행 이름, 동작, 방향, 직업, 프레임 수, fps, loop)"""
    out = []
    for anim, n, fps, loop in SHARED:
        for d in DIRS:
            out.append((f'{anim}_{d}', anim, d, job or 'warrior', n, fps, loop))
    jobs = [job] if job else PD.JOBS
    for j in jobs:
        for anim, n, fps, loop in JOBANIM:
            for d in DIRS:
                out.append((f'{j}_{anim}_{d}', anim, d, j, n, fps, loop))
    return out


def default_look(gender, job):
    return dict(gender=gender, body=PD.BODIES[gender][0], hair=PD.HAIRS[gender][0], top=PD.TOPS[gender][0],
                bottom=PD.BOTTOMS[gender][0], weapon=PD.WEAPONS[job][0])


def frame_layers(look, job, anim, d, i, want):
    ps = rig.poses(rig.make_spec(look['gender'], look['body']), job, anim, d)
    return rig.render(look, job, ps[i], d, CW, CH, OX, OY, want=want)


# ─── 아틀라스 ───────────────────────────────────────────────────
class Packer:
    def __init__(self, width=1024):
        self.w = width
        self.x = self.y = self.row_h = 0
        self.items = []

    def add(self, img):
        w, h = img.size
        if self.x + w > self.w:
            self.x = 0
            self.y += self.row_h + 1
            self.row_h = 0
        pos = (self.x, self.y)
        self.items.append((img, pos))
        self.x += w + 1
        self.row_h = max(self.row_h, h)
        return pos

    def image(self):
        H = self.y + self.row_h + 1
        H = 1 << max(4, (H - 1).bit_length())
        im = Image.new('RGBA', (self.w, H), (0, 0, 0, 0))
        for img, pos in self.items:
            im.paste(img, pos)
        return im


def layer_image(m, rgb, glow):
    ys, xs = np.nonzero(m)
    y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    out = np.zeros((y1 - y0, x1 - x0, 4), np.uint8)
    sub = m[y0:y1, x0:x1]
    out[..., :3] = np.clip(rgb[y0:y1, x0:x1], 0, 255).astype(np.uint8)
    out[..., 3] = np.where(sub, np.where(glow[y0:y1, x0:x1], 254, 255), 0)
    return Image.fromarray(out, 'RGBA'), int(x0), int(y0)


def export_part(pid, part, gender, idx, job=None):
    """부위 하나의 모든 행·프레임·층을 잘라 아틀라스로"""
    pk = Packer()
    tiles = []
    jobs_rows = rows_for(job) if part == 'weapon' else rows_for()
    for name, anim, d, j, n, fps, loop in jobs_rows:
        look = default_look(gender, j)
        if part == 'weapon':
            look['weapon'] = PD.WEAPONS[job][idx]
        else:
            look[part] = {'body': PD.BODIES, 'hair': PD.HAIRS, 'top': PD.TOPS, 'bottom': PD.BOTTOMS}[part][gender][idx]
        for i in range(n):
            layers = frame_layers(look, j, anim, d, i, part)
            for si, sub in enumerate(rig.SUBS):
                if (sub, part) not in layers:
                    continue
                img, x0, y0 = layer_image(*layers[(sub, part)])
                ax, ay = pk.add(img)
                tiles.append(dict(row=name, f=i, s=si, x=ax, y=ay, w=img.width, h=img.height, dx=x0, dy=y0))
    atlas = pk.image()
    path = os.path.join(OUT, pid)
    atlas.save(path + '.png.tmp', 'PNG')
    os.replace(path + '.png.tmp', path + '.bytes')
    with open(path + '_meta.json', 'w', encoding='utf-8') as f:
        json.dump(dict(id=pid, atlasW=atlas.width, atlasH=atlas.height, tiles=tiles), f, ensure_ascii=False, separators=(',', ':'))
    return len(tiles)


def export_all(only=None):
    os.makedirs(OUT, exist_ok=True)
    manifest = dict(cellW=CW, cellH=CH, pivotX=OX, pivotY=OY, pxPerUnit=PX_PER_UNIT, subs=len(rig.SUBS),
                    female=dict(body=[], hair=[], top=[], bottom=[]), male=dict(body=[], hair=[], top=[], bottom=[]),
                    weapons={j: [] for j in PD.JOBS}, rows=[], hairNames=[], topNames=[], bottomNames=[])
    for name, anim, d, j, n, fps, loop in rows_for():
        manifest['rows'].append(dict(row=name, anim=anim, dir=d, job=(j if name.startswith(j + '_') else ''), count=n, fps=fps, loop=loop))
    jobs = []
    for gender, gname in (('f', 'female'), ('m', 'male')):
        for part, table in (('body', PD.BODIES), ('hair', PD.HAIRS), ('top', PD.TOPS), ('bottom', PD.BOTTOMS)):
            for idx in range(len(table[gender])):
                pid = f'{gender}_{part}_{idx + 1:02d}'
                manifest[gname][part].append(pid)
                jobs.append((pid, part, gender, idx, None))
    for job in PD.JOBS:
        for idx in range(len(PD.WEAPONS[job])):
            pid = f'w_{job}_{idx + 1:02d}'
            manifest['weapons'][job].append(pid)
            jobs.append((pid, 'weapon', 'f', idx, job))
    for pid, part, gender, idx, job in jobs:
        if only and not any(pid.startswith(o) for o in only):
            continue
        n = export_part(pid, part, gender, idx, job)
        print(pid, n, flush=True)
    with open(os.path.join(OUT, 'manifest.json'), 'w', encoding='utf-8') as f:
        json.dump(manifest, f, ensure_ascii=False, indent=1)


# ─── 미리보기 (게임에서 조립하는 것과 같은 방식) ─────────────────────
def compose(look, job, anim, d, i, colors):
    layers = rig.render(look, job, rig.poses(rig.make_spec(look['gender'], look['body']), job, anim, d)[i], d, CW, CH, OX, OY)
    out = np.zeros((CH, CW, 4), np.uint8)
    glowm = np.zeros((CH, CW), bool)
    for sub in rig.SUBS:
        for part in rig.PARTS:
            if (sub, part) not in layers:
                continue
            m, rgb, glow = layers[(sub, part)]
            col = rgb.astype(np.int32)
            key = (col[..., 0] == 255) & (col[..., 2] == 255) & np.isin(col[..., 1], [0, 50, 100, 150, 200])
            if part in colors:
                rp = np.array(ramp(colors[part]), np.int32)
                idx = np.clip(col[..., 1] // 50, 0, 4)
                col = np.where(key[..., None], rp[idx], col)
            out[m, :3] = col[m]
            out[m, 3] = 255
            glowm[m] = glow[m]
    # 외곽선
    a = (out[..., 3] > 0) & ~glowm
    ol = np.zeros_like(a)
    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        ol |= np.roll(a, (dy, dx), axis=(0, 1)) & (out[..., 3] == 0)
    out[ol] = (22, 12, 26, 255)
    return Image.fromarray(out, 'RGBA')


HAIR_COLORS = ['#2a2430', '#5a3a2a', '#e8c868', '#d84a3a', '#e88aa8', '#4a6ad8', '#c8ccd8', '#5aa860', '#8a5ad0', '#f0e8d8']
CLOTH_COLORS = ['#c83a3a', '#3a6ac8', '#3a9a5a', '#e8d8b8', '#5a3a8a', '#2a2a34', '#d88a2a', '#3aa8a8', '#f0f0f0', '#8a2a4a']


def preview(path, n=8, seed=3):
    rnd = random.Random(seed)
    cells = [('idle', 'front', 0), ('idle', 'side', 0), ('idle', 'back', 0), ('walk', 'side', 1), ('walk', 'front', 1), ('attack', 'side', 2),
             ('attack', 'front', 2), ('skill', 'side', 2), ('skill', 'front', 3), ('death', 'side', 5),
             ('sit', 'side', 0), ('sit', 'front', 0), ('sit', 'back', 0)]
    rows = []
    for k in range(n):
        g = 'f' if k % 2 == 0 else 'm'
        job = PD.JOBS[k % 5]
        look = dict(gender=g, body=rnd.choice(PD.BODIES[g]), hair=rnd.choice(PD.HAIRS[g]), top=rnd.choice(PD.TOPS[g]),
                    bottom=rnd.choice(PD.BOTTOMS[g]), weapon=rnd.choice(PD.WEAPONS[job]))
        colors = dict(hair=rnd.choice(HAIR_COLORS), top=rnd.choice(CLOTH_COLORS), bottom=rnd.choice(CLOTH_COLORS))
        row = Image.new('RGBA', (CW * len(cells), CH), (64, 60, 72, 255))
        for ci, (anim, d, i) in enumerate(cells):
            row.alpha_composite(compose(look, job, anim, d, i, colors), (ci * CW, 0))
        rows.append(row)
    im = Image.new('RGBA', (CW * len(cells), CH * n), (40, 38, 48, 255))
    for k, r in enumerate(rows):
        im.alpha_composite(r, (0, k * CH))
    im = im.resize((im.width * 3, im.height * 3), Image.NEAREST)
    im.save(path)


if __name__ == '__main__':
    if len(sys.argv) > 1 and sys.argv[1] == 'preview':
        preview(os.environ.get('ADV_PREVIEW', 'adv_preview.png'), seed=int(sys.argv[2]) if len(sys.argv) > 2 else 3)
    else:
        export_all(sys.argv[1:] or None)
