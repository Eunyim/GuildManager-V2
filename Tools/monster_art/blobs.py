# 슬라임 (말랑한 젤리 + 큰 눈) · 킹 슬라임 (왕관) · 미믹 (이빨 달린 보물상자)
import math

import numpy as np

from core import Painter, Xf, add, dirv, lerp, mixc, ramp, hexc

R_EYE = 10.0
ANIMS = {'idle': 4, 'walk': 6, 'attack': 6, 'death': 6}


def P(**kw):
    p = dict(sx=1.0, sy=1.0, y=0.0, x=0.0, eyes='open', mouth='smile', fx=None, fxk=0.0, puddle=0.0, lid=0.0, tongue=0.0, lean=0.0, coins=0)
    p.update(kw)
    return p


def poses(s, anim, view):
    if s['kind'] == 'mimic':
        return _mimic_poses(s, anim, view)
    if anim == 'idle':
        return [P(sx=1 + a, sy=1 - a) for a in (0.0, 0.04, 0.07, 0.04)]
    if anim == 'walk':
        # 깡충 뛰기
        return [P(sx=1.12, sy=0.86), P(sx=0.9, sy=1.12, y=-2), P(sx=0.94, sy=1.06, y=-5), P(sx=0.98, sy=1.02, y=-4),
                P(sx=1.0, sy=1.0, y=-1), P(sx=1.16, sy=0.84)]
    if anim == 'attack':
        side = view == 'side'
        return [
            P(sx=1.2, sy=0.78, eyes='angry', mouth='open'),
            P(sx=1.28, sy=0.7, eyes='angry', mouth='open'),
            P(sx=0.82 if not side else 1.25, sy=1.2 if not side else 0.88, x=5 if side else 0, y=-5, eyes='angry', mouth='big', fx='lines', fxk=1.0, lean=12),
            P(sx=1.38, sy=0.66, x=6 if side else 0, eyes='angry', mouth='big', fx='splash', fxk=1.0),
            P(sx=1.15, sy=0.86, x=3 if side else 0, eyes='angry', fx='splash', fxk=0.5),
            P(sx=1.03, sy=0.97),
        ]
    return [
        P(sx=0.9, sy=1.12, eyes='hurt', mouth='open'),
        P(sx=1.2, sy=0.8, eyes='hurt', mouth='open'),
        P(sx=1.45, sy=0.55, eyes='x', mouth='open'),
        P(eyes='x', puddle=0.4),
        P(eyes='x', puddle=0.75, fx='drops', fxk=1.0),
        P(eyes='x', puddle=1.0, fx='drops', fxk=0.5),
    ]


def draw(cv, s, p, view, ox, oy):
    if s['kind'] == 'mimic':
        return _mimic(cv, s, p, view, ox, oy)
    R, H = s['r'], s['h']
    body = ramp(s['color'])
    xf = Xf(ox + p['x'], oy + p['y'])
    g = Painter(cv, xf)
    if p['puddle'] > 0:
        _puddle(cv, g, s, p, body)
        return
    sx, sy = p['sx'], p['sy']
    rx, ry = R * sx, H * 0.5 * sy
    c = (0.0, -ry)
    side = view == 'side'
    lean = p['lean'] if side else 0
    # 그림자 쪽 아랫부분이 살짝 퍼진 물방울 모양: 본체 + 바닥 넓적
    g.ell(add(c, (0, ry * 0.35)), rx * 1.04, ry * 0.68, body)
    m = g.ell(c, rx * 0.96, ry, body, ang=lean, line=False)
    # 젤리 속: 가장자리보다 살짝 밝은 안쪽 + 기포
    for bx, by, br in ((0.42, 0.42, 0.11), (-0.35, 0.5, 0.08), (0.15, 0.62, 0.06)):
        q = add(c, (rx * bx, ry * by))
        r0 = max(0.8, R * br)
        g.ell(q, r0, r0, ramp(mixc(hexc(s['color']), (255, 255, 255), 0.45)), line=False)
    # 반짝임 (왼쪽 위)
    hl = add(c, (-rx * 0.45, -ry * 0.5))
    g.ell(hl, max(1.2, rx * 0.16), max(1.0, ry * 0.2), [(255, 255, 255)] * 5, line=False, ang=-30)
    g.px(add(hl, (rx * 0.25, -ry * 0.12)), (255, 255, 255))
    if s.get('crown'):
        _crown(g, s, c, rx, ry, view)
    if view != 'back':
        _slime_face(g, s, p, c, rx, ry, side)
    _slime_fx(g, s, p, c, rx, ry, body)


def _crown(g, s, c, rx, ry, view):
    gd = ramp('#e8b840')
    top = c[1] - ry * 0.92
    w = rx * 0.5
    pts = [(-w, top + 2.5), (-w * 1.05, top - 3.5), (-w * 0.5, top - 1.0), (0, top - 5.0), (w * 0.5, top - 1.0), (w * 1.05, top - 3.5), (w, top + 2.5)]
    g.poly(pts, gd, shade=0.35, grad=((-0.5, -1), 0.6))
    g.line((-w + 0.5, top + 1.5), (w - 0.5, top + 1.5), gd[1])
    if view != 'back':
        g.ell((0, top - 0.3), 1.3, 1.3, ramp('#e83050'))
        g.px((-w * 0.6, top + 0.5), ramp('#40a0ff')[3])
        g.px((w * 0.6, top + 0.5), ramp('#40e080')[3])


def _slime_face(g, s, p, c, rx, ry, side):
    global R_EYE
    R_EYE = s['r']   # 눈 크기는 찌그러짐과 상관없이 일정하게
    line = (20, 30, 24) if not s.get('face_line') else s['face_line']
    eye_r = ramp(s.get('eye', '#203020'))
    sep = rx * (0.38 if not side else 0.3)
    cx = c[0] + (rx * 0.32 if side else 0)
    ey = c[1] - ry * 0.05
    ew = max(2, int(round(R_EYE * 0.2)))
    eh = max(4, int(round(R_EYE * 0.3)))
    mood = p['eyes']
    for k, sg in enumerate((-1, 1)):
        ex = cx + sg * sep - ew / 2
        if side and sg < 0:
            ex += 1   # 먼 쪽 눈은 좁게
        wk = ew - (1 if side and sg < 0 else 0)
        if mood == 'x':
            for i in range(3):
                g.px((ex + i, ey - 1 + i), line)
                g.px((ex + 2 - i, ey - 1 + i), line)
            continue
        if mood == 'hurt':
            g.px((ex, ey), line)
            g.px((ex + 1, ey + 1), line)
            g.px((ex + 2, ey), line)
            continue
        for j in range(eh):
            for i in range(wk):
                col = line if j < eh - 1 else mixc(line, (60, 90, 70), 0.4)
                g.px((ex + i, ey - eh / 2 + j), col)
        g.px((ex, ey - eh / 2 + 1), (255, 255, 255))
        if eh >= 4 and wk >= 2:
            g.px((ex + wk - 1, ey + eh / 2 - 2), (190, 230, 255))
        if mood == 'angry':
            g.line((ex - (1 if sg < 0 else -wk), ey - eh / 2 - 2), (ex + (wk if sg < 0 else -1), ey - eh / 2 - 0.5), line)
    # 볼
    for sg in (-1, 1):
        if side and sg < 0:
            continue
        g.px((cx + sg * (sep + ew * 0.6), ey + eh * 0.5), ramp('#ff8090')[3])
        g.px((cx + sg * (sep + ew * 0.6) + 1, ey + eh * 0.5), ramp('#ff8090')[3])
    # 입
    my = ey + eh * 0.5 + 1.5
    mth = p['mouth']
    if mth == 'smile':
        g.px((cx - 1, my), line)
        g.px((cx, my + 1), line)
        g.px((cx + 1, my), line)
    elif mth == 'open':
        g.ell((cx, my + 0.8), 1.6, 1.3, ramp('#6a1a2a'), line=False)
    else:
        g.ell((cx, my + 1.2), 2.6, 2.0, ramp('#6a1a2a'))
        g.px((cx - 1, my + 0.2), (255, 255, 255))


def _slime_fx(g, s, p, c, rx, ry, body):
    fx, k = p['fx'], p['fxk']
    if fx == 'splash':
        for a, d in ((-160, 1.0), (-130, 0.8), (-50, 0.8), (-20, 1.0), (-95, 0.6)):
            q = add((0, -1), dirv(a, (rx + 2) * d * (0.8 + k * 0.6)))
            g.ell(q, 1.2 * k + 0.6, 1.2 * k + 0.6, body, line=False)
    elif fx == 'lines':
        for i in range(3):
            y = c[1] + (i - 1) * 3
            g.line((-rx - 7, y), (-rx - 2, y), (255, 255, 240), glow=True)


def _puddle(cv, g, s, p, body):
    t = p['puddle']
    R = s['r']
    w = R * (1.2 + t * 0.6)
    h = max(1.5, s['h'] * 0.22 * (1.1 - t * 0.5))
    g.ell((0, -h * 0.6), w, h, body)
    g.ell((-w * 0.4, -h * 0.9), max(1, w * 0.12), max(0.8, h * 0.3), [(255, 255, 255)] * 5, line=False)
    # 눈 (X)
    line = (20, 30, 24)
    for sg in (-1, 1):
        x0 = sg * w * 0.3
        y0 = -h * 1.1
        g.px((x0 - 1, y0 - 1), line)
        g.px((x0 + 1, y0 + 1), line)
        g.px((x0 + 1, y0 - 1), line)
        g.px((x0 - 1, y0 + 1), line)
        g.px((x0, y0), line)
    if p['fx'] == 'drops':
        for a in (-150, -110, -70, -30):
            q = add((0, -h), dirv(a, w * (0.55 + (1 - p['fxk']) * 0.3)))
            g.ell(q, 1.0, 1.0, body, line=False)
    if s.get('crown'):
        gd = ramp('#e8b840')
        g.poly([(w * 0.5, -h * 0.4), (w * 1.1, -h * 0.4 - 2), (w * 1.4, -h * 0.4 - 6), (w * 0.9, -h * 0.4 - 4), (w * 0.7, -h * 0.4 - 6)], gd, shade=0.3)


# ════════════════════════════════════════════════════════════════
# 미믹
# ════════════════════════════════════════════════════════════════
def _mimic_poses(s, anim, view):
    if anim == 'idle':
        return [P(lid=0.12, y=0), P(lid=0.15, y=0), P(lid=0.22, y=0, tongue=0.2), P(lid=0.15, y=0)]
    if anim == 'walk':
        return [P(lid=0.1, sx=1.06, sy=0.94), P(lid=0.25, y=-3, sy=1.05), P(lid=0.3, y=-5), P(lid=0.25, y=-4), P(lid=0.15, y=-1),
                P(lid=0.05, sx=1.08, sy=0.92)]
    if anim == 'attack':
        side = view == 'side'
        return [
            P(lid=0.35, sx=1.05, sy=0.92, eyes='angry'),
            P(lid=0.95, y=-1, eyes='angry', tongue=0.6),
            P(lid=1.15, x=5 if side else 0, y=-4, eyes='angry', tongue=1.0, fx='lines', fxk=1.0, lean=-8),
            P(lid=0.0, x=6 if side else 0, sx=1.08, sy=0.9, eyes='angry', fx='bite', fxk=1.0),
            P(lid=0.25, x=3 if side else 0, eyes='angry'),
            P(lid=0.15),
        ]
    return [
        P(lid=0.6, eyes='hurt', tongue=0.4, y=-2),
        P(lid=0.85, eyes='hurt', tongue=0.8, lean=-6),
        P(lid=1.1, eyes='x', tongue=1.0, lean=-14, sy=0.95, coins=2),
        P(lid=1.25, eyes='x', tongue=1.0, lean=-18, sy=0.92, coins=4),
        P(lid=1.3, eyes='x', tongue=0.9, lean=-20, sy=0.9, coins=6),
        P(lid=1.3, eyes='x', tongue=0.9, lean=-20, sy=0.9, coins=6),
    ]


def _mimic(cv, s, p, view, ox, oy):
    W, H, D = s['w'], s['h'], s['d']
    wood = ramp(s['wood'])
    wood_d = ramp(mixc(hexc(s['wood']), (20, 10, 10), 0.3))
    iron = ramp(s['iron'])
    gold = ramp('#e0b040')
    mouth = ramp('#5a1424')
    xf = Xf(ox + p['x'], oy + p['y'], rot=p['lean'] * (0.3 if view != 'side' else 0.0), sx=p['sx'], sy=p['sy'])
    g = Painter(cv, xf)
    side = view == 'side'
    back = view == 'back'
    lid = p['lid']
    # 동전 (사망)
    if p['coins']:
        gc = Painter(cv, Xf(ox, oy))
        for i in range(p['coins']):
            x = (i - p['coins'] / 2) * 3.2 + (2 if side else 0)
            gc.ell((x + W * 0.25, -1.2 - (i % 2)), 1.6, 1.1, gold)
    hw = (D if side else W) * 0.5
    body_h = H * 0.58
    lid_h = H * 0.42
    # 다리 (작은 발: 걷기용)
    for sg in (-1, 1):
        g.ell((sg * hw * 0.6, -0.8), 1.8, 1.2, wood_d)
    # 상자 아랫몸
    top = -body_h
    g.poly([(-hw, top), (hw, top), (hw, -1.2), (-hw, -1.2)], wood, shade=0.15, grad=((-0.4, -1), 0.4))
    # 판자 줄
    for t in (0.33, 0.66):
        y = top + body_h * t
        g.line((-hw + 1, y), (hw - 1, y), wood[1])
    # 쇠 테
    for x in (-hw + 1.2, hw - 1.2):
        g.poly([(x - 1.0, top), (x + 1.0, top), (x + 1.0, -1.2), (x - 1.0, -1.2)], iron, shade=0.2)
    # 입 안 (뚜껑이 열리면 보임)
    hinge_x = -hw if side else 0
    ang = -lid * 70  # 뚜껑 회전 (옆에서는 뒤 경첩 기준)
    if lid > 0.05 and not back:
        open_h = lid * lid_h * 1.6
        g.poly([(-hw + 0.8, top + 0.5), (hw - 0.8, top + 0.5), (hw - 0.8, top - open_h * 0.6), (-hw + 0.8, top - open_h * 0.6)], mouth, shade=-0.2)
        # 혀
        if p['tongue'] > 0:
            tl = p['tongue']
            tip = (hw * (0.6 if not side else 1.2) * tl + (0 if not side else 2), top + 2 + tl * 3)
            g.cap((0 if not side else -hw * 0.3, top - 0.5), tip, 1.6, ramp('#e05070'), r1=1.2)
        # 아랫니
        n = int(hw * 2 / 2.4)
        for i in range(n):
            x = -hw + 1.5 + i * 2.4
            g.poly([(x - 0.9, top + 0.3), (x + 0.9, top + 0.3), (x, top - 1.8)], ramp('#f4ecd8'), shade=0.3, line=False)
        # 안쪽 눈
        if not side or True:
            ex = 0 if not side else hw * 0.2
            eyc = ramp('#ffe040')
            for sg in ((-1, 1) if not side else (1,)):
                q = (ex + sg * hw * 0.35, top - open_h * 0.35)
                if p['eyes'] == 'x':
                    g.px((q[0] - 1, q[1] - 1), (30, 10, 10))
                    g.px((q[0] + 1, q[1] + 1), (30, 10, 10))
                    g.px((q[0] + 1, q[1] - 1), (30, 10, 10))
                    g.px((q[0] - 1, q[1] + 1), (30, 10, 10))
                else:
                    g.ell(q, 1.4, 1.0 if p['eyes'] != 'hurt' else 0.4, eyc, glow=True, line=False)
                    g.px((q[0], q[1]), (40, 10, 10), glow=True)
    # 뚜껑
    if side:
        piv = (-hw, top)
        def rp(v):
            a = math.radians(ang)
            dx, dy = v[0] - piv[0], v[1] - piv[1]
            return (piv[0] + dx * math.cos(a) - dy * math.sin(a), piv[1] + dx * math.sin(a) + dy * math.cos(a))
        lid_pts = [rp((-hw, top)), rp((hw + 0.6, top)), rp((hw + 0.6, top - lid_h * 0.6)), rp((hw * 0.5, top - lid_h)), rp((-hw * 0.5, top - lid_h)),
                   rp((-hw, top - lid_h * 0.6))]
        g.poly(lid_pts, wood, shade=0.3, grad=((-0.4, -1), 0.5))
        g.line(rp((-hw + 1, top - lid_h * 0.55)), rp((hw - 0.5, top - lid_h * 0.55)), iron[2])
        if lid > 0.05:
            n = int(hw * 2 / 2.4)
            for i in range(n):
                q = rp((-hw + 1.5 + i * 2.4, top))
                g.poly([(q[0] - 0.9, q[1] - 0.3), (q[0] + 0.9, q[1] - 0.3), (q[0], q[1] + 1.8)], ramp('#f4ecd8'), shade=0.3, line=False)
    else:
        # 앞·뒤: 뚜껑이 위로 들리며 (앞에서는) 짧아 보임
        lift = lid * lid_h * 1.4
        lh = lid_h * (1 - lid * 0.45) if not back else lid_h
        bot = top - (lift if not back else lift * 0.3)
        g.poly([(-hw - 0.6, bot), (hw + 0.6, bot), (hw + 0.6, bot - lh * 0.55), (hw * 0.6, bot - lh), (-hw * 0.6, bot - lh), (-hw - 0.6, bot - lh * 0.55)],
               wood, shade=0.3, grad=((-0.4, -1), 0.5))
        for x in (-hw + 1.2, hw - 1.2):
            g.poly([(x - 1.0, bot), (x + 1.0, bot), (x + 1.0, bot - lh * 0.75), (x - 1.0, bot - lh * 0.75)], iron, shade=0.25)
        if lid > 0.05 and not back:
            n = int(hw * 2 / 2.4)
            for i in range(n):
                x = -hw + 1.5 + i * 2.4
                g.poly([(x - 0.9, bot - 0.3), (x + 0.9, bot - 0.3), (x, bot + 1.8)], ramp('#f4ecd8'), shade=0.3, line=False)
        if not back:
            # 자물쇠
            g.poly([(-1.6, top - 0.5), (1.6, top - 0.5), (1.6, top + 3.2), (-1.6, top + 3.2)], gold, shade=0.35)
            g.px((0, top + 1.2), (40, 20, 10))
    # 효과
    if p['fx'] == 'lines':
        for i in range(3):
            y = -H * 0.5 + (i - 1) * 3
            g.line((-hw - 8, y), (-hw - 3, y), (255, 255, 240), glow=True)
    elif p['fx'] == 'bite':
        q = (hw + 2 if side else 0, top - 1 if side else top + 4)
        for a in range(0, 360, 45):
            g.line(add(q, dirv(a, 1)), add(q, dirv(a, 3.5 if a % 90 == 0 else 2.2)), (255, 244, 200), glow=True)
