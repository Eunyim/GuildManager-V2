# 네발짐승 리그 (늑대·드래곤) + 늑대를 탄 오크 기수
# 옆(side, 오른쪽을 봄)·앞(front)·뒤(back). 사망은 옆으로 쓰러져 다리가 늘어지는 모습
import math

import humanoid
from core import Painter, Xf, add, darker, dirv, hexc, ik, lerp, mixc, ramp

ANIMS = {'idle': 4, 'walk': 6, 'attack': 6, 'death': 6}


def P(**kw):
    p = dict(bob=0.0, hipx=0.0, head=(0.0, 0.0), jaw=0.0, tail=0.0, legs=None, crouch=0.0, eyes='open', fx=None, fxk=0.0,
             wing=0.0, dead=0.0, lean=0.0, rider=None, rider_only=False)
    p.update(kw)
    return p


def _legs_cycle(s, ph, run=True):
    """다리 네 개 (앞 가까운, 앞 먼, 뒤 가까운, 뒤 먼)의 (x 오프셋, 들림)"""
    st, lf = s['stride'], s['lift']
    out = []
    for off in (0.0, math.pi, math.pi * (0.85 if run else 1.0), math.pi * (1.85 if run else 0.0)):
        a = ph + off
        out.append((math.sin(a) * st, max(0.0, math.cos(a)) * lf))
    return out


def poses(s, anim, view):
    if s.get('kind') == 'rider':
        return _rider_poses(s, anim, view)
    return _poses(s, anim, view)


def _rider_poses(s, anim, view):
    rider = s['rider']
    if anim == 'death':
        return [P(rider=r, rider_only=True) for r in humanoid.poses(rider, 'death', view)]
    mp = _poses(s['mount'], anim, view)
    if anim == 'attack':
        rp = humanoid.poses(rider, 'attack', view)
    else:
        idle = humanoid.poses(rider, 'idle', view)
        rp = [idle[i % len(idle)] for i in range(len(mp))]
    out = []
    for a, b in zip(mp, rp):
        b = dict(b)
        b['bob'] = b.get('bob', 0) * 0.5
        b['footN'] = b['footF'] = (0, 0)
        a['rider'] = b
        out.append(a)
    return out


def _poses(s, anim, view):
    if anim == 'idle':
        return [P(bob=[0, 0, 1, 1][i] * s.get('bob_px', 1.0), tail=math.sin(i / 4 * math.tau) * 8, wing=math.sin(i / 4 * math.tau) * 0.15,
                  head=(0, [0, 0, 0.5, 0.5][i]), fx='smoke' if s.get('dragon') else None, fxk=i / 4) for i in range(4)]
    if anim == 'walk':
        out = []
        for i in range(6):
            ph = i / 6 * math.tau
            out.append(P(bob=round(abs(math.sin(ph)) * s.get('bob_px', 1.0) * 1.5), legs=_legs_cycle(s, ph), tail=math.sin(ph) * 12,
                         head=(0.5, math.cos(ph) * 0.8), wing=math.sin(ph) * 0.35))
        return out
    if anim == 'attack':
        if s.get('dragon'):
            return [
                P(head=(-2, -2), crouch=1, wing=0.5, jaw=0.3, eyes='angry'),
                P(head=(-3, -3), crouch=2, wing=0.8, jaw=0.5, eyes='angry', fx='glow', fxk=0.6),
                P(head=(2, 1), hipx=1, wing=-0.3, jaw=1.0, eyes='angry', fx='breath', fxk=0.6),
                P(head=(3, 1.5), hipx=1, wing=-0.4, jaw=1.0, eyes='angry', fx='breath', fxk=1.0),
                P(head=(2.5, 1), hipx=1, wing=-0.2, jaw=0.8, eyes='angry', fx='breath', fxk=0.7),
                P(head=(0.5, 0), wing=0.1, jaw=0.2),
            ]
        st = s['stride']
        return [
            P(crouch=2, head=(-1.5, 2), hipx=-1.5, jaw=0.2, eyes='angry', tail=-6, legs=[(st * 0.2, 0), (-st * 0.1, 0), (-st * 0.3, 0), (-st * 0.5, 0)]),
            P(crouch=3, head=(-2, 3), hipx=-2.5, jaw=0.5, eyes='angry', tail=-10, legs=[(st * 0.1, 0), (-st * 0.2, 0), (-st * 0.4, 0), (-st * 0.6, 0)]),
            P(head=(5, -1), hipx=5, jaw=1.0, eyes='angry', tail=14, bob=-2, legs=[(st * 1.4, 3), (st * 1.0, 2), (-st * 1.2, 1), (-st * 1.4, 0)], fx='lines', fxk=1.0),
            P(head=(6, 1), hipx=6, jaw=0.0, eyes='angry', tail=10, bob=-1, legs=[(st * 1.3, 0), (st * 0.9, 0), (-st * 0.8, 0), (-st * 1.0, 0)], fx='bite', fxk=1.0),
            P(head=(3, 0.5), hipx=3, jaw=0.2, tail=6, legs=[(st * 0.8, 0), (st * 0.5, 0), (-st * 0.4, 0), (-st * 0.6, 0)]),
            P(head=(0.5, 0), hipx=0.5, tail=2),
        ]
    # death
    return [
        P(head=(-1, -3), eyes='hurt', tail=-14, jaw=0.6, bob=-1),
        P(head=(-1, 1), eyes='hurt', crouch=3, tail=-10, jaw=0.3, dead=0.25, wing=0.6),
        P(eyes='hurt', crouch=5, dead=0.55, jaw=0.4, wing=0.8),
        P(eyes='x', dead=0.85, jaw=0.5, wing=0.9, bob=1),
        P(eyes='x', dead=1.0, jaw=0.5, wing=1.0, bob=-0.5),
        P(eyes='x', dead=1.0, jaw=0.4, wing=1.0),
    ]


def mats(s):
    m = {}
    for k in ('fur', 'belly', 'dark', 'nose', 'horn', 'wing', 'claw', 'mane', 'scar'):
        if k in s:
            m[k] = ramp(s[k])
            m[k + '_far'] = ramp(darker(hexc(s[k]), 0.74))
    return m


def draw(cv, s, p, view, ox, oy):
    kind = s.get('kind')
    if kind == 'rider':
        return _draw_rider(cv, s, p, view, ox, oy)
    m = mats(s)
    g = Painter(cv, Xf(ox, oy))
    return _beast(g, cv, s, m, p, view)


def _beast(g, cv, s, m, p, view):
    if view == 'side':
        return _side(g, cv, s, m, p)
    return _frontback(g, cv, s, m, p, view)


# ─── 옆모습 ───────────────────────────────────────────────────
def _side(g, cv, s, m, p):
    L, ry = s['body_len'], s['body_ry']
    leg = s['leg']
    dead = p['dead']
    crouch = p['crouch']
    body_y = -leg - ry * 0.55 + p['bob'] + crouch + dead * (leg - ry * 0.2)
    c = (p['hipx'], body_y)
    chest = add(c, (L * 0.32, -ry * 0.1 + crouch * 0.3))
    hips = add(c, (-L * 0.34, 0))
    tilt = dead * 0.0
    legs = p['legs'] or [(s['stride'] * 0.25, 0), (-s['stride'] * 0.1, 0), (-s['stride'] * 0.15, 0), (s['stride'] * 0.15, 0)]
    if dead > 0:
        # 쓰러짐: 다리가 앞뒤로 늘어지며 바닥 쪽으로
        spread = dead
        legs = [(s['stride'] * (0.4 + spread * 1.4), -dead * 0), (s['stride'] * (0.2 + spread * 1.0), 0),
                (-s['stride'] * (0.4 + spread * 1.4), 0), (-s['stride'] * (0.2 + spread * 1.0), 0)]
    fur, fur_f = m['fur'], m['fur_far']

    def leg_at(root, foot_dx, lift, rmp, front, far):
        foot = (root[0] + foot_dx, -lift - (0 if dead < 0.5 else 0))
        if dead > 0:
            foot = (root[0] + foot_dx * 1.0, min(-0.5, root[1] + (leg * 0.9) * (1 - dead)))
        lr = s['leg_r']
        if front:
            # 앞다리: 어깨 → 팔꿈치(살짝 뒤) → 발목 → 발
            elbow = ik(root, foot, leg * 0.5, leg * 0.62, 1)
            g.cap(root, elbow, lr * 1.45, rmp, r1=lr * 1.05)
            wrist = lerp(elbow, foot, 0.82)
            g.cap(elbow, wrist, lr * 0.95, rmp, r1=lr * 0.75)
            g.cap(wrist, foot, lr * 0.75, rmp, r1=lr * 0.8)
        else:
            # 뒷다리: 굵은 넓적다리 → 무릎(앞) → 뒤꿈치(뒤로 꺾임) → 발
            hock = (foot[0] - leg * 0.22, foot[1] - leg * 0.36)
            knee = ik(root, hock, leg * 0.5, leg * 0.42, -1)
            g.ell(lerp(root, knee, 0.4), lr * 2.3, lr * 2.0, rmp, ang=60)
            g.cap(knee, hock, lr * 1.0, rmp, r1=lr * 0.75)
            g.cap(hock, foot, lr * 0.75, rmp, r1=lr * 0.8)
        paw = m['dark_far' if far else 'dark'] if s.get('dark_paws') else rmp
        g.ell((foot[0] + lr * 0.6, foot[1] - lr * 0.5), lr * 1.25, lr * 0.75, paw)
        if s.get('claw'):
            g.px((foot[0] + lr * 1.6, foot[1] - 0.4), m['claw'][3])

    # 먼 쪽 날개
    if s.get('dragon'):
        _wing(g, s, m, add(c, (L * 0.12, -ry * 0.8)), p, far=True)
    # 먼 쪽 다리
    leg_at(add(chest, (0, ry * 0.4)), legs[1][0], legs[1][1], fur_f, True, True)
    leg_at(add(hips, (0, ry * 0.3)), legs[3][0], legs[3][1], fur_f, False, True)
    # 꼬리
    _tail(g, s, m, hips, p)
    # 몸통: 깊은 가슴 + 잘록한 허리 + 엉덩이 (덩어리끼리는 선 없이 이어지게)
    g.cap(add(hips, (0, -ry * 0.1)), add(chest, (0, -ry * 0.25)), ry * 0.72, fur)
    g.ell(hips, L * 0.25, ry * 0.88, fur, line=False)
    g.ell(add(chest, (0, ry * 0.08)), L * 0.27, ry * 1.12, fur, line=False)
    if 'belly' in m:
        g.ell(add(chest, (L * 0.02, ry * 0.62)), L * 0.2, ry * 0.5, m['belly'], line=False, dither=False)
        g.cap(add(chest, (-L * 0.1, ry * 0.75)), add(hips, (L * 0.12, ry * 0.45)), ry * 0.22, m['belly'], line=False, dither=False)
    if s.get('dragon') and 'belly' in m:
        for i in range(5):
            q = lerp(add(chest, (L * 0.05, ry * 1.0)), add(hips, (L * 0.15, ry * 0.6)), i / 4)
            g.line(add(q, (-1.5, -1.6)), add(q, (0.5, 0.8)), m['belly'][1])
    if s.get('saddle_mark'):
        sm = ramp(s['saddle_mark'])
        g.cap(add(hips, (L * 0.02, -ry * 0.65)), add(chest, (-L * 0.08, -ry * 0.8)), ry * 0.38, sm, line=False, dither=False)
    if s.get('mane'):
        g.ell(add(chest, (-L * 0.02, -ry * 0.55)), L * 0.32, ry * 0.75, m['mane'])
        for i in range(4):
            q = add(chest, (-L * 0.2 + i * L * 0.12, -ry * 1.15))
            g.poly([add(q, (-1.5, 1)), add(q, (0, -2.6)), add(q, (1.5, 1))], m['mane'], shade=0.3)
    if s.get('dragon'):
        # 등 가시
        for i in range(5):
            q = add(c, (-L * 0.45 + i * L * 0.2, -ry * 0.92))
            g.poly([add(q, (-1.6, 1)), add(q, (0.5, -2.8)), add(q, (1.6, 1))], m['horn'], shade=0.3)
    if s.get('scar'):
        g.line(add(c, (-L * 0.1, -ry * 0.4)), add(c, (L * 0.1, ry * 0.1)), m['scar'][1])
    # 가까운 다리
    leg_at(add(hips, (0.5, ry * 0.35)), legs[2][0], legs[2][1], fur, False, False)
    leg_at(add(chest, (0.5, ry * 0.45)), legs[0][0], legs[0][1], fur, True, False)
    # 목·머리
    neck = s.get('neck', 0.0)
    hd = p['head']
    head_c = add(chest, (L * 0.24 + hd[0], -ry * 0.75 - neck + hd[1] + dead * (neck + ry * 0.5)))
    if neck > 0:
        g.cap(add(chest, (L * 0.05, -ry * 0.3)), add(head_c, (-1, 1)), s['head_r'] * 0.6, fur, r1=s['head_r'] * 0.55)
        if 'belly' in m:
            g.cap(add(chest, (L * 0.15, 0)), add(head_c, (0, 2)), s['head_r'] * 0.32, m['belly'], line=False)
    elif dead == 0:
        g.cap(chest, head_c, s['head_r'] * 0.85, fur)
    _head_side(g, s, m, head_c, p)
    if s.get('dragon'):
        _wing(g, s, m, add(c, (L * 0.05, -ry * 0.75)), p, far=False)
    _fx_side(g, cv, s, m, p, head_c)
    return dict(c=c, chest=chest, hips=hips, head=head_c, saddle=add(c, (L * 0.02, -ry * 0.9)))


def _tail(g, s, m, hips, p):
    tl = s['tail']
    base = add(hips, (-s['body_len'] * 0.22, -s['body_ry'] * 0.25))
    a = s.get('tail_ang', -150) + p['tail'] - p['dead'] * 30
    pts = [base]
    for i in range(1, 5):
        a2 = a + (i * s.get('tail_curve', 8))
        pts.append(add(pts[-1], dirv(a2, tl / 4)))
    r0 = s['tail_r']
    for i in range(4):
        rr = r0 * ([1.0, 1.15, 1.05, 0.75][i] if not s.get('dragon') else (1 - i * 0.18))
        rmp = m['fur'] if not s.get('tail_tip') or i < 3 else m[s['tail_tip']]
        g.cap(pts[i], pts[i + 1], rr, rmp, r1=rr * 0.85)
    if s.get('dragon'):
        tip = pts[-1]
        d = dirv(a + 32, 1)
        n = (-d[1], d[0])
        g.poly([add(tip, n, -2.6), add(tip, d, 4.5), add(tip, n, 2.6)], m['horn'], shade=0.2)


def _head_side(g, s, m, hc, p):
    r = s['head_r']
    fur = m['fur']
    jaw = p['jaw']
    sn = s['snout']
    # 귀 / 뿔 (뒤쪽)
    if s.get('dragon'):
        g.poly([add(hc, (-r * 0.6, -r * 0.4)), add(hc, (-r * 2.1, -r * 1.4)), add(hc, (-r * 0.1, -r * 0.8))], m['horn'], shade=0.3)
    else:
        g.poly([add(hc, (-r * 0.75, -r * 0.4)), add(hc, (-r * 0.5, -r * 1.75)), add(hc, (r * 0.15, -r * 0.65))], fur, shade=0.35, grad=((0, -1), 0.4))
        g.poly([add(hc, (-r * 0.5, -r * 0.6)), add(hc, (-r * 0.45, -r * 1.35)), add(hc, (-r * 0.05, -r * 0.7))], m['dark'], shade=-0.1, line=False, dither=False)
    # 아래턱 (벌어짐)
    jaw_a = 12 + jaw * 26
    jbase = add(hc, (r * 0.2, r * 0.35))
    jtip = add(jbase, dirv(jaw_a, sn * 1.05))
    g.cap(jbase, jtip, r * 0.38, m['belly'] if 'belly' in m else fur, r1=r * 0.26)
    if jaw > 0.3:
        # 입속 + 이빨
        utip = add(hc, (r * 0.4 + sn, r * 0.15))
        g.poly([add(hc, (r * 0.3, r * 0.25)), add(utip, (0, 0.5)), jtip], ramp('#8a2030'), shade=-0.2, line=False)
        for t in (0.45, 0.75):
            q = lerp(add(hc, (r * 0.4, r * 0.3)), utip, t)
            g.px((q[0], q[1] + 0.6), (250, 246, 232))
            q2 = lerp(jbase, jtip, t)
            g.px((q2[0], q2[1] - 0.8), (250, 246, 232))
    # 머리 + 주둥이
    g.ell(hc, r, r * 0.88, fur)
    g.cap(add(hc, (r * 0.3, r * 0.05)), add(hc, (r * 0.35 + sn, r * 0.18)), r * 0.48, fur, r1=r * 0.36)
    if 'belly' in m and not s.get('dragon'):
        g.cap(add(hc, (r * 0.2, r * 0.45)), add(hc, (r * 0.2 + sn * 0.9, r * 0.38)), r * 0.2, m['belly'], line=False)
    nose = add(hc, (r * 0.35 + sn + r * 0.25, r * 0.08))
    g.px(nose, m['nose'][1] if 'nose' in m else (30, 20, 24))
    g.px((nose[0] - 1, nose[1]), m['nose'][2] if 'nose' in m else (30, 20, 24))
    if s.get('dragon'):
        g.px(add(nose, (-1, -1)), (30, 12, 12))
        g.poly([add(hc, (-r * 0.2, -r * 0.7)), add(hc, (-r * 1.5, -r * 1.7)), add(hc, (r * 0.2, -r * 0.95))], m['horn'], shade=0.35)
    # 눈
    ex, ey = hc[0] + r * 0.32, hc[1] - r * 0.22
    eye = ramp(s['eye'])
    line = (26, 12, 20)
    if p['eyes'] == 'x':
        for i in range(3):
            g.px((ex - 1 + i, ey - 1 + i), line)
            g.px((ex + 1 - i, ey - 1 + i), line)
    elif p['eyes'] == 'hurt':
        g.line((ex - 1, ey), (ex + 1, ey + 1), line)
    else:
        big = r >= 6
        g.px((ex - 1, ey - 1), line)
        g.px((ex, ey - 1), line)
        if big:
            g.px((ex + 1, ey - 1), line)
        g.px((ex - 1, ey), eye[3])
        g.px((ex, ey), eye[2])
        g.px((ex, ey + 1), eye[1])
        g.px((ex - 1, ey + 1), eye[2])
        if big:
            g.px((ex + 1, ey), eye[1])
            g.px((ex + 1, ey + 1), line)
        g.px((ex - 1, ey), (255, 255, 240))
        if p['eyes'] == 'angry':
            g.line((ex - 2, ey - 2.5), (ex + 1.5, ey - 1.2), line)


def _wing(g, s, m, root, p, far):
    """박쥐 날개: 뼈대 세 갈래 + 막"""
    W = s['wing_len']
    up = p['wing']
    dead = p['dead']
    base_a = -125 + up * 40 + dead * 70
    wm = m['wing_far' if far else 'wing']
    bone = m['fur_far' if far else 'fur']
    root = add(root, (-2 if far else 0, -1 if far else 0))
    elbow = add(root, dirv(base_a + 30, W * 0.45))
    tips = [add(elbow, dirv(base_a - 20 + i * 38, W * (0.75 - i * 0.12))) for i in range(3)]
    sc = lambda a, b: lerp(lerp(a, b, 0.5), elbow, 0.32)   # 막 가장자리가 안으로 파인 물결 모양
    mem = [root, elbow, tips[0], sc(tips[0], tips[1]), tips[1], sc(tips[1], tips[2]), tips[2], lerp(add(root, (-W * 0.3, W * 0.1)), elbow, 0.2)]
    g.poly(mem, wm, shade=0.05 if far else 0.2, grad=((-0.4, -1), 0.5))
    g.cap(root, elbow, 1.4, bone)
    for t in tips:
        g.cap(elbow, t, 0.9, bone, r1=0.6)
    g.px(elbow, m['horn'][3])


def _fx_side(g, cv, s, m, p, hc):
    fx, k = p['fx'], p['fxk']
    r = s['head_r']
    mouth = add(hc, (r * 0.5 + s['snout'] + 1, r * 0.4))
    if fx == 'breath':
        _flame(g, mouth, 0, s['breath_len'] * (0.5 + 0.5 * k), k)
    elif fx == 'glow':
        g.ell(mouth, 1.5 + k * 2, 1.5 + k * 2, ramp('#ffa030'), glow=True, line=False)
    elif fx == 'smoke':
        q = add(hc, (r * 0.4 + s['snout'] + 1, -r * 0.2 - k * 4))
        g.ell(q, 1.2 + k, 1.0 + k * 0.8, ramp('#8a8088'), glow=True, line=False)
    elif fx == 'lines':
        for i in range(3):
            y = hc[1] + (i - 1) * 3
            g.line((hc[0] - s['body_len'] - 4, y), (hc[0] - s['body_len'] * 0.6, y), (255, 248, 230), glow=True)
    elif fx == 'bite':
        q = add(hc, (r * 0.4 + s['snout'] + 2, r * 0.3))
        for a in range(0, 360, 45):
            g.line(add(q, dirv(a, 1)), add(q, dirv(a, 3.5 if a % 90 == 0 else 2.2)), (255, 244, 200), glow=True)


def _flame(g, at, ang, length, k):
    """불길: 입에서 퍼지는 원뿔. 바깥 붉은색 → 안쪽 노란색 → 흰색"""
    d = dirv(ang)
    n = (-d[1], d[0])
    cols = [ramp('#d83a1a'), ramp('#f08a20'), ramp('#ffd860')]
    for layer, (wk, lk) in enumerate(((1.0, 1.0), (0.66, 0.85), (0.33, 0.62))):
        pts = []
        steps = 9
        for i in range(steps + 1):
            t = i / steps
            w = (1.2 + t * length * 0.38) * wk
            wob = (math.sin(t * 13 + k * 6 + layer) * 1.6 + math.sin(t * 29 + k * 11) * 0.9) * t
            pts.append(add(add(at, d, t * length * lk), n, w + wob))
        for i in range(steps, -1, -1):
            t = i / steps
            w = (1.2 + t * length * 0.38) * wk
            wob = (math.sin(t * 11 + k * 5 + layer * 2) * 1.6 + math.sin(t * 23 + k * 7) * 0.9) * t
            pts.append(add(add(at, d, t * length * lk), n, -w + wob))
        g.poly(pts, cols[layer], shade=0.3, glow=True, line=False)
    g.ell(add(at, d, 1.5), 1.6, 1.6, [(255, 255, 240)] * 5, glow=True, line=False)


# ─── 앞·뒤 ───────────────────────────────────────────────────
def _frontback(g, cv, s, m, p, view):
    L, ry = s['body_len'], s['body_ry']
    leg = s['leg']
    dead = p['dead']
    W = s.get('body_w', ry * 1.05)
    body_y = -leg - ry * 0.55 + p['bob'] + p['crouch'] + dead * (leg - ry * 0.2)
    c = (0.0, body_y)
    front = view == 'front'
    fur, fur_f = m['fur'], m['fur_far']
    legs = p['legs'] or [(0, 0)] * 4
    lr = s['leg_r']

    def pillar(x, lift, rmp, back_leg):
        top = (x, body_y + ry * 0.3)
        foot = (x * (1 + dead * 0.8), -lift)
        if dead > 0:
            foot = (x * (1 + dead * 1.2), -0.6)
        g.cap(top, foot, lr * (1.2 if back_leg else 1.05), rmp, r1=lr * 0.85)
        g.ell((foot[0], foot[1] - lr * 0.4), lr * 1.15, lr * 0.75, m['dark'] if s.get('dark_paws') else rmp)

    # 날개 (뒤에 있는 것부터)
    if s.get('dragon'):
        for sg in (-1, 1):
            _wing_fb(g, s, m, (sg * W * 0.5, body_y - ry * 0.6), sg, p, behind=front)
    hd = p['head']
    if front:
        # 뒷다리 (몸 뒤로 살짝)
        pillar(-W * 0.62, legs[3][1] * 0.6, fur_f, True)
        pillar(W * 0.62, legs[2][1] * 0.6, fur_f, True)
        # 꼬리 끝이 몸 위로 살짝
        tb = (W * 0.4, body_y - ry * 0.5)
        tt = add(tb, (W * 0.5 + p['tail'] * 0.15, -ry * 0.8))
        g.cap(tb, tt, s['tail_r'] * 0.8, fur_f, r1=s['tail_r'] * 0.5)
        g.ell(c, W, ry * 1.05, fur)
        if 'belly' in m:
            g.ell(add(c, (0, ry * 0.25)), W * 0.55, ry * 0.7, m['belly'], line=False)
            if s.get('dragon'):
                for i in range(4):
                    y = c[1] - ry * 0.25 + i * ry * 0.32
                    g.line((-W * 0.4, y), (W * 0.4, y), m['belly'][1])
        if s.get('mane'):
            g.ell(add(c, (0, -ry * 0.45)), W * 0.95, ry * 0.6, m['mane'])
        pillar(-W * 0.45, legs[0][1], fur, False)
        pillar(W * 0.45, legs[1][1], fur, False)
        hc = (hd[0] * 0.2, body_y - ry * 0.75 - s.get('neck', 0) + hd[1] + abs(hd[0]) * 0.5 + dead * (ry * 0.9 + s.get('neck', 0)))
        if s.get('neck', 0) > 0:
            g.cap(add(c, (0, -ry * 0.3)), hc, s['head_r'] * 0.55, fur)
            if 'belly' in m:
                g.cap(add(c, (0, -ry * 0.1)), add(hc, (0, 2)), s['head_r'] * 0.28, m['belly'], line=False)
        _head_front(g, s, m, hc, p)
        _fx_front(g, s, m, p, hc, True)
    else:
        hc = (hd[0] * 0.2, body_y - ry * 0.75 - s.get('neck', 0) + hd[1] + dead * (ry * 0.9 + s.get('neck', 0)))
        if s.get('neck', 0) > 0:
            g.cap(add(c, (0, -ry * 0.3)), hc, s['head_r'] * 0.55, fur_f)
        _head_back(g, s, m, hc, p)
        _fx_front(g, s, m, p, hc, False)
        pillar(-W * 0.45, legs[1][1] * 0.6, fur_f, False)
        pillar(W * 0.45, legs[0][1] * 0.6, fur_f, False)
        g.ell(c, W, ry * 1.05, fur)
        if s.get('dragon'):
            for i in range(3):
                q = add(c, (0, -ry * 0.9 + i * ry * 0.55))
                g.poly([add(q, (-1.8, 1)), add(q, (0, -2.6)), add(q, (1.8, 1))], m['horn'], shade=0.3)
        if s.get('mane'):
            g.ell(add(c, (0, -ry * 0.55)), W * 0.85, ry * 0.5, m['mane'])
        pillar(-W * 0.62, legs[2][1], fur, True)
        pillar(W * 0.62, legs[3][1], fur, True)
        # 꼬리 (가운데에서 위로 흔들림)
        tb = (0, body_y - ry * 0.2)
        tang = -90 + p['tail'] * 1.5 + dead * 70
        tt = add(tb, dirv(tang, s['tail'] * 0.6))
        g.cap(tb, tt, s['tail_r'], fur, r1=s['tail_r'] * 0.6)
        if s.get('tail_tip'):
            g.ell(tt, s['tail_r'] * 0.8, s['tail_r'] * 0.8, m[s['tail_tip']])
        if s.get('dragon'):
            g.poly([add(tt, (-2.4, 0)), add(tt, (0, -4)), add(tt, (2.4, 0))], m['horn'], shade=0.2)
    if s.get('dragon') and not front:
        for sg in (-1, 1):
            _wing_fb(g, s, m, (sg * W * 0.5, body_y - ry * 0.6), sg, p, behind=False)
    return dict(c=c, head=hc, saddle=(0.0, body_y - ry * 0.9))


def _wing_fb(g, s, m, root, sg, p, behind):
    W = s['wing_len']
    up = p['wing']
    dead = p['dead']
    a = -90 - sg * (55 - up * 35 - dead * 60) if sg > 0 else -90 - sg * (55 - up * 35 - dead * 60)
    a = -90 + sg * (60 - up * 30 + dead * 55)
    wm = m['wing_far'] if behind else m['wing']
    bone = m['fur_far'] if behind else m['fur']
    elbow = add(root, dirv(a, W * 0.5))
    tips = [add(elbow, dirv(a + sg * (10 + i * 28), W * (0.65 - i * 0.12))) for i in range(3)]
    sc = lambda a, b: lerp(lerp(a, b, 0.5), elbow, 0.32)
    g.poly([root, elbow, tips[0], sc(tips[0], tips[1]), tips[1], sc(tips[1], tips[2]), tips[2], add(root, (sg * W * 0.2, W * 0.2))],
           wm, shade=0.1, grad=((-0.4, -1), 0.5))
    g.cap(root, elbow, 1.4, bone)
    for t in tips:
        g.cap(elbow, t, 0.9, bone, r1=0.6)


def _head_front(g, s, m, hc, p):
    r = s['head_r']
    fur = m['fur']
    jaw = p['jaw']
    if s.get('dragon'):
        for sg in (-1, 1):
            g.poly([add(hc, (sg * r * 0.5, -r * 0.6)), add(hc, (sg * r * 1.5, -r * 1.8)), add(hc, (sg * r * 0.9, -r * 0.3))], m['horn'], shade=0.35)
    else:
        for sg in (-1, 1):
            g.poly([add(hc, (sg * r * 0.25, -r * 0.7)), add(hc, (sg * r * 0.85, -r * 1.85)), add(hc, (sg * r * 1.0, -r * 0.35))], fur, shade=0.35,
                   grad=((0, -1), 0.4))
            g.poly([add(hc, (sg * r * 0.45, -r * 0.75)), add(hc, (sg * r * 0.8, -r * 1.5)), add(hc, (sg * r * 0.85, -r * 0.55))], m['dark'],
                   shade=-0.1, line=False, dither=False)
    g.ell(hc, r * 1.05, r * 0.92, fur)
    if s.get('mane'):
        for sg in (-1, 1):
            g.poly([add(hc, (sg * r * 0.8, -r * 0.2)), add(hc, (sg * r * 1.5, r * 0.3)), add(hc, (sg * r * 0.8, r * 0.7))], m['mane'], shade=0.2)
    # 주둥이 (화면 쪽으로)
    mz = add(hc, (0, r * 0.45))
    if jaw > 0.3:
        g.ell(add(mz, (0, r * 0.35 + jaw)), r * 0.42, r * 0.3 + jaw * 1.2, ramp('#8a2030'))
        g.px(add(mz, (-1.5, r * 0.25)), (250, 246, 232))
        g.px(add(mz, (1.5, r * 0.25)), (250, 246, 232))
    g.ell(mz, r * 0.55, r * 0.4, m['belly'] if 'belly' in m else fur)
    g.ell(add(mz, (0, -r * 0.12)), r * 0.24, r * 0.16, ramp('#2a1a20') if 'nose' not in m else m['nose'])
    eye = ramp(s['eye'])
    line = (26, 12, 20)
    for sg in (-1, 1):
        ex, ey = hc[0] + sg * r * 0.42, hc[1] - r * 0.15
        if p['eyes'] == 'x':
            g.px((ex - 0.5, ey - 0.5), line)
            g.px((ex + 0.5, ey + 0.5), line)
            g.px((ex + 0.5, ey - 0.5), line)
            g.px((ex - 0.5, ey + 0.5), line)
            continue
        if p['eyes'] == 'hurt':
            g.line((ex - 1, ey), (ex + 1, ey), line)
            continue
        g.px((ex - 0.5, ey - 1), line)
        g.px((ex + 0.5, ey - 1), line)
        g.px((ex - 0.5, ey), eye[3])
        g.px((ex + 0.5, ey), eye[2])
        g.px((ex - 0.5, ey + 1), eye[2])
        g.px((ex + 0.5, ey + 1), eye[1])
        g.px((ex - 0.5 if sg < 0 else ex + 0.5, ey), (255, 255, 240))
        if p['eyes'] == 'angry':
            g.line((ex - sg * 1.5, ey - 2.2), (ex + sg * 1.2, ey - 1.4), line)


def _head_back(g, s, m, hc, p):
    r = s['head_r']
    fur = m['fur_far']
    if s.get('dragon'):
        for sg in (-1, 1):
            g.poly([add(hc, (sg * r * 0.5, -r * 0.6)), add(hc, (sg * r * 1.5, -r * 1.8)), add(hc, (sg * r * 0.9, -r * 0.3))], m['horn'], shade=0.3)
    else:
        for sg in (-1, 1):
            g.poly([add(hc, (sg * r * 0.25, -r * 0.7)), add(hc, (sg * r * 0.85, -r * 1.85)), add(hc, (sg * r * 1.0, -r * 0.35))], m['fur'], shade=0.3)
    g.ell(hc, r * 1.0, r * 0.9, m['fur'])


def _fx_front(g, s, m, p, hc, front):
    fx, k = p['fx'], p['fxk']
    r = s['head_r']
    if fx == 'breath':
        if front:
            # 화면 쪽으로 쏟아지는 불: 아래로 퍼짐
            _flame(g, add(hc, (0, r * 0.8)), 90, s['breath_len'] * 0.55 * (0.6 + 0.4 * k), k)
        else:
            _flame(g, add(hc, (0, -r * 0.6)), -90, s['breath_len'] * 0.45 * (0.6 + 0.4 * k), k)
    elif fx == 'glow':
        g.ell(add(hc, (0, r * 0.7 if front else -r * 0.5)), 1.5 + k * 2, 1.5 + k * 2, ramp('#ffa030'), glow=True, line=False)
    elif fx == 'smoke' and front:
        q = add(hc, (0, -r * 0.2 - k * 4))
        g.ell(q, 1.0 + k, 0.8 + k * 0.6, ramp('#8a8088'), glow=True, line=False)
    elif fx == 'bite' and front:
        q = add(hc, (0, r * 1.1))
        for a in range(0, 360, 45):
            g.line(add(q, dirv(a, 1)), add(q, dirv(a, 3.5 if a % 90 == 0 else 2.2)), (255, 244, 200), glow=True)


# ─── 오크 기수 ────────────────────────────────────────────────
def _draw_rider(cv, s, p, view, ox, oy):
    mount = s['mount']
    rider = s['rider']
    g = Painter(cv, Xf(ox, oy))
    if p.get('rider_only'):
        # 사망: 늑대에서 떨어지는 오크만 (늑대는 게임에서 따로 생겨남)
        humanoid.draw(cv, rider, p['rider'], view, ox, oy)
        return
    mm = mats(mount)
    if view == 'front':
        # 앞에서는 늑대 몸과 머리가 오크 아랫도리를 가림: 오크 먼저, 늑대 나중
        null = _Null(cv)
        J0 = _frontback(Painter(null, Xf(ox, oy)), null, mount, mm, p, view)
        _rider(cv, rider, p, view, ox, oy, J0['saddle'])
        _frontback(g, cv, mount, mm, p, view)
        return
    J = _side(g, cv, mount, mm, p) if view == 'side' else _frontback(g, cv, mount, mm, p, view)
    _rider(cv, rider, p, view, ox, oy, J['saddle'])


def _rider(cv, rider, p, view, ox, oy, saddle):
    rp = dict(p['rider'])
    leg = rider['leg']
    # 엉덩이가 안장 위에 오도록
    rp['bob'] = saddle[1] + leg + rp.get('bob', 0)
    rp['hipx'] = saddle[0] + rp.get('hipx', 0)
    sy = saddle[1] + leg * 0.55
    if view == 'side':
        rp['footN'] = (saddle[0] + 2.5 - rp['hipx'] * 0.6, -sy)
        rp['footF'] = (saddle[0] + 1.0 - rp['hipx'] * 0.3, -sy)
    else:
        rp['footN'] = (0, -sy)
        rp['footF'] = (0, -sy)
    humanoid.draw(cv, dict(rider, mounted=True), rp, view, ox, oy)


class _Null:
    """그리지 않고 좌표만 계산할 때 쓰는 빈 캔버스"""

    def __init__(self, cv):
        self.cv = cv
        self.w, self.h = cv.w, cv.h
        self.X, self.Y = cv.X, cv.Y

    def __getattr__(self, k):
        if k in ('ellipse', 'capsule', 'poly', 'ring'):
            return getattr(self.cv, k)
        return lambda *a, **kw: None
