# 사람형 몬스터 리그 (고블린, 오크, 도적, 스켈레톤)
# spec(몸 비율·색·장비) + pose(프레임마다 손발 목표 위치)로 앞(front)·뒤(back)·옆(side, 오른쪽을 봄) 세 방향을 그림
import math

from core import (Painter, Xf, add, darker, dirv, ik, lerp, mixc, ramp, reach, rot)

ANIMS = {'idle': 4, 'walk': 6, 'attack': 6, 'death': 6}


# ════════════════════════════════════════════════════════════════
# 포즈
# ════════════════════════════════════════════════════════════════
def base_pose(s):
    A = s['arm']
    return dict(bob=0.0, lean=0.0, hipx=0.0, rot=0.0, drop=0.0,
                footN=(s['stride'] * 0.15, 0.0), footF=(-s['stride'] * 0.15, 0.0),
                handN=(A * 0.18, A * 0.86), handF=(-A * 0.12, A * 0.86),
                wpn=s['w_idle'], wlen=1.0, wfront=True, smear=None, fx=None, fxk=0.0,
                eyes='open', htilt=0.0, weapon_drop=False, arrow=False, string=None)


def P(s, **kw):
    p = base_pose(s)
    p.update(kw)
    return p


def _hold_out(s, p, view):
    """앞·뒤에서 긴 지팡이·창은 팔에 가려지지 않게 몸 바깥쪽으로 들고 섬"""
    if view != 'side' and s['weapon'] in ('staff', 'spear'):
        A = s['arm']
        p['handN'] = (p['handN'][0] - A * 0.32, p['handN'][1] - A * 0.12)
    return p


def poses(s, anim, view):
    A = s['arm']
    st = s['stride']
    big = s.get('bob_px', 1.0)
    atk = s['attack']
    out = []

    if anim == 'idle':
        for i in range(4):
            b = [0, 0, 1, 1][i] * big
            sway = [0, 0.04, 0.06, 0.02][i] * A
            p = P(s, bob=b)
            p['handN'] = (p['handN'][0], p['handN'][1] - sway)
            p['handF'] = (p['handF'][0], p['handF'][1] - sway * 0.5)
            p['wpn'] = s['w_idle'] + [0, -2, -4, -2][i]
            if s.get('caster'):
                p['fx'] = 'orb'
                p['fxk'] = 0.4 + 0.2 * math.sin(i / 4 * math.tau)
            out.append(_hold_out(s, p, view))
        return out

    if anim == 'walk':
        for i in range(6):
            ph = i / 6 * math.tau
            lift = s['lift']
            sw = math.sin(ph)
            p = P(s, bob=round(abs(math.cos(ph)) * big * 1.2) - 0.0, lean=s.get('walk_lean', 5))
            if view == 'side':
                p['footN'] = (sw * st, max(0.0, math.cos(ph)) * lift)
                p['footF'] = (-sw * st, max(0.0, -math.cos(ph)) * lift)
                p['handN'] = (A * 0.18 - sw * A * 0.35, A * 0.84)
                p['handF'] = (-A * 0.12 + sw * A * 0.35, A * 0.84)
            else:
                p['footN'] = (0.0, max(0.0, math.cos(ph)) * lift * 1.2)   # 앞/뒤에서는 발이 번갈아 들림
                p['footF'] = (0.0, max(0.0, -math.cos(ph)) * lift * 1.2)
                p['handN'] = (0.0, A * 0.84 - sw * A * 0.12)
                p['handF'] = (0.0, A * 0.84 + sw * A * 0.12)
            p['wpn'] = s['w_idle'] + sw * 6
            if s.get('caster'):
                p['fx'] = 'orb'
                p['fxk'] = 0.4
            out.append(_hold_out(s, p, view))
        return out

    if anim == 'death':
        back = -1
        seq = [
            dict(lean=-16, bob=1, handN=(-A * 0.2, -A * 0.3), handF=(-A * 0.4, -A * 0.1), wpn=s['w_idle'] - 50, eyes='hurt', htilt=-10),
            dict(lean=-22, bob=3 * big, handN=(-A * 0.1, A * 0.2), handF=(-A * 0.4, A * 0.3), wpn=s['w_idle'] - 70, eyes='hurt', htilt=-14,
                 footN=(st * 0.5, 0), footF=(-st * 0.1, 0)),
            dict(rot=35 * back, bob=2 * big, lean=-10, handN=(-A * 0.3, -A * 0.5), handF=(-A * 0.5, -A * 0.2), weapon_drop=True, eyes='hurt', htilt=-10,
                 footN=(st * 0.7, 2), footF=(st * 0.1, 0)),
            dict(rot=70 * back, bob=1, lean=-4, handN=(-A * 0.4, -A * 0.6), handF=(-A * 0.6, -A * 0.3), weapon_drop=True, eyes='x',
                 footN=(st * 0.8, 3), footF=(st * 0.3, 1)),
            dict(rot=96 * back, drop=-1.5, handN=(-A * 0.5, -A * 0.4), handF=(-A * 0.6, -A * 0.1), weapon_drop=True, eyes='x',
                 footN=(st * 0.6, 1), footF=(st * 0.2, 0)),
            dict(rot=90 * back, handN=(-A * 0.4, -A * 0.15), handF=(-A * 0.5, A * 0.05), weapon_drop=True, eyes='x',
                 footN=(st * 0.5, 0), footF=(st * 0.2, 0)),
        ]
        if view != 'side':
            # 앞·뒤: 옆으로 쓰러짐 (화면 오른쪽으로)
            for d in seq:
                if 'rot' in d:
                    d['rot'] = -d['rot']
                d['handN'] = (0.0, d['handN'][1])
                d['handF'] = (0.0, d['handF'][1])
                d['footN'] = (0.0, d['footN'][1] if 'footN' in d else 0)
                d['footF'] = (0.0, d['footF'][1] if 'footF' in d else 0)
                d['lean'] = 0
        return [P(s, **d) for d in seq]

    # ─── 공격 ───
    if view == 'side':
        if atk == 'slash':
            seq = [
                dict(lean=-6, handN=(-A * 0.25, -A * 0.75), wpn=-125, footN=(st * 0.4, 0), eyes='angry'),
                dict(lean=-9, bob=1, handN=(-A * 0.35, -A * 0.85), wpn=-150, footN=(st * 0.5, 0), eyes='angry'),
                dict(lean=14, hipx=2, handN=(A * 0.85, A * 0.15), wpn=25, footN=(st * 1.1, 0), footF=(-st * 0.5, 0), smear=(-150, 25), eyes='angry'),
                dict(lean=16, hipx=2, bob=1, handN=(A * 0.6, A * 0.65), wpn=75, footN=(st * 1.1, 0), footF=(-st * 0.5, 0), smear=(-40, 75), eyes='angry'),
                dict(lean=10, hipx=1, bob=1, handN=(A * 0.5, A * 0.72), wpn=65, footN=(st * 0.9, 0), footF=(-st * 0.4, 0), eyes='angry'),
                dict(lean=3, handN=(A * 0.3, A * 0.8), wpn=s['w_idle'] + 20, footN=(st * 0.4, 0)),
            ]
        elif atk == 'smash':
            seq = [
                dict(lean=-8, bob=1, handN=(-A * 0.05, -A * 0.95), handF=(A * 0.05, -A * 0.85), wpn=-110, eyes='angry'),
                dict(lean=-12, bob=2 * big, handN=(-A * 0.25, -A * 0.95), handF=(-A * 0.1, -A * 0.9), wpn=-150, eyes='angry'),
                dict(lean=18, hipx=2, bob=1, handN=(A * 0.75, A * 0.35), handF=(A * 0.6, A * 0.4), wpn=55, footN=(st * 1.1, 0), smear=(-150, 55), eyes='angry'),
                dict(lean=22, hipx=2, bob=3 * big, handN=(A * 0.7, A * 0.7), handF=(A * 0.55, A * 0.75), wpn=88, footN=(st * 1.1, 0), fx='dust', fxk=1.0, eyes='angry'),
                dict(lean=16, hipx=1, bob=2 * big, handN=(A * 0.65, A * 0.75), handF=(A * 0.5, A * 0.8), wpn=85, footN=(st * 1.0, 0), fx='dust', fxk=0.5, eyes='angry'),
                dict(lean=4, handN=(A * 0.35, A * 0.8), wpn=s['w_idle'] + 15, footN=(st * 0.4, 0)),
            ]
        elif atk == 'thrust':
            seq = [
                dict(lean=-5, handN=(-A * 0.2, A * 0.35), handF=(-A * 0.5, A * 0.3), wpn=-4, eyes='angry'),
                dict(lean=-8, bob=1, handN=(-A * 0.4, A * 0.32), handF=(-A * 0.65, A * 0.3), wpn=-3, eyes='angry'),
                dict(lean=14, hipx=3, handN=(A * 1.0, A * 0.12), handF=(A * 0.55, A * 0.2), wpn=-2, footN=(st * 1.2, 0), footF=(-st * 0.6, 0), fx='lines', fxk=1.0, eyes='angry'),
                dict(lean=12, hipx=3, handN=(A * 0.95, A * 0.15), handF=(A * 0.5, A * 0.22), wpn=-2, footN=(st * 1.2, 0), footF=(-st * 0.6, 0), fx='spark', fxk=1.0, eyes='angry'),
                dict(lean=5, hipx=1, handN=(A * 0.4, A * 0.3), handF=(A * 0.05, A * 0.35), wpn=-3, footN=(st * 0.6, 0)),
                dict(lean=2, footN=(st * 0.3, 0)),
            ]
        elif atk == 'stab':
            seq = [
                dict(lean=-4, handN=(-A * 0.25, A * 0.4), wpn=-10, eyes='angry'),
                dict(lean=14, hipx=2, handN=(A * 0.95, A * 0.2), wpn=-5, footN=(st * 1.0, 0), fx='lines', fxk=0.8, eyes='angry'),
                dict(lean=2, hipx=1, handN=(-A * 0.1, A * 0.45), wpn=-10, footN=(st * 0.8, 0), eyes='angry'),
                dict(lean=16, hipx=3, handN=(A * 1.0, A * 0.0), wpn=-15, footN=(st * 1.2, 0), footF=(-st * 0.5, 0), fx='spark', fxk=1.0, eyes='angry'),
                dict(lean=8, hipx=2, handN=(A * 0.7, A * 0.3), wpn=-5, footN=(st * 1.0, 0)),
                dict(lean=2, footN=(st * 0.3, 0)),
            ]
        elif atk == 'bow':
            seq = [
                dict(lean=-2, handN=(A * 0.8, -A * 0.1), handF=(A * 0.45, -A * 0.05), wpn=-5, string='rest', eyes='angry'),
                dict(lean=-4, handN=(A * 0.95, -A * 0.12), handF=(A * 0.05, -A * 0.12), wpn=-5, string='pull', arrow=True, eyes='focus'),
                dict(lean=-5, handN=(A * 0.98, -A * 0.12), handF=(-A * 0.15, -A * 0.12), wpn=-5, string='full', arrow=True, eyes='focus'),
                dict(lean=-8, handN=(A * 0.98, -A * 0.18), handF=(-A * 0.4, -A * 0.3), wpn=-8, string='rest', fx='lines', fxk=1.0, eyes='angry'),
                dict(lean=-3, handN=(A * 0.85, -A * 0.05), handF=(-A * 0.2, A * 0.2), wpn=-4, string='rest'),
                dict(lean=0),
            ]
        else:  # cast
            seq = [
                dict(lean=-4, handN=(A * 0.25, -A * 0.65), handF=(A * 0.35, A * 0.1), wpn=-92, fx='orb', fxk=0.8, eyes='angry'),
                dict(lean=-8, bob=1, handN=(A * 0.15, -A * 0.85), handF=(A * 0.55, -A * 0.2), wpn=-96, fx='charge', fxk=1.0, eyes='focus'),
                dict(lean=-10, bob=1, handN=(A * 0.15, -A * 0.9), handF=(A * 0.65, -A * 0.3), wpn=-98, fx='charge', fxk=1.4, eyes='focus'),
                dict(lean=12, hipx=2, handN=(A * 0.95, -A * 0.25), handF=(A * 0.7, A * 0.1), wpn=-28, footN=(st * 1.0, 0), fx='burst', fxk=1.0, eyes='angry'),
                dict(lean=8, hipx=1, handN=(A * 0.8, -A * 0.1), handF=(A * 0.4, A * 0.4), wpn=-35, footN=(st * 0.8, 0), fx='burst', fxk=0.5),
                dict(lean=2, footN=(st * 0.3, 0)),
            ]
        return [P(s, **d) for d in seq]

    # 앞·뒤 공격 (무기 든 손이 front에서는 화면 왼쪽, back에서는 오른쪽)
    if atk in ('slash', 'smash'):
        seq = [
            dict(bob=1, handN=(-A * 0.15, -A * 0.95), wpn=-60, wfront=False, eyes='angry'),
            dict(bob=2 * big, handN=(-A * 0.05, -A * 1.0), wpn=-35, wfront=False, eyes='angry'),
            dict(bob=1, handN=(A * 0.25, A * 0.55), wpn=110, smear=(-80, 110), eyes='angry',
                 fx='dust' if atk == 'smash' else None, fxk=1.0),
            dict(bob=2 * big, handN=(A * 0.35, A * 0.7), wpn=120, eyes='angry', fx='dust' if atk == 'smash' else None, fxk=0.6),
            dict(bob=1, handN=(A * 0.2, A * 0.75), wpn=115),
            dict(bob=0),
        ]
    elif atk in ('thrust', 'stab'):
        seq = [
            dict(handN=(-A * 0.1, A * 0.5), wpn=-80, wlen=0.6, eyes='angry'),
            dict(bob=1, handN=(-A * 0.05, A * 0.45), wpn=-85, wlen=0.5, eyes='angry'),
            dict(bob=0, handN=(A * 0.3, A * 0.55), wpn=95, wlen=0.45, fx='spark', fxk=1.0, eyes='angry'),
            dict(handN=(A * 0.3, A * 0.55), wpn=95, wlen=0.45, fx='spark', fxk=0.6, eyes='angry'),
            dict(handN=(A * 0.1, A * 0.6), wpn=-80, wlen=0.6),
            dict(),
        ]
    elif atk == 'bow':
        seq = [
            dict(handN=(A * 0.2, A * 0.1), handF=(-A * 0.1, A * 0.3), wpn=-90, wlen=1.0, string='rest', eyes='angry'),
            dict(handN=(A * 0.3, -A * 0.05), handF=(-A * 0.3, A * 0.0), wpn=-90, string='pull', arrow=True, eyes='focus'),
            dict(handN=(A * 0.3, -A * 0.05), handF=(-A * 0.45, -A * 0.05), wpn=-90, string='full', arrow=True, eyes='focus'),
            dict(handN=(A * 0.3, -A * 0.1), handF=(-A * 0.6, -A * 0.2), wpn=-90, string='rest', fx='spark', fxk=1.0, eyes='angry'),
            dict(handN=(A * 0.2, A * 0.2), handF=(-A * 0.3, A * 0.5), wpn=-90, string='rest'),
            dict(),
        ]
    else:  # cast
        seq = [
            dict(handN=(-A * 0.1, -A * 0.6), wpn=-90, fx='orb', fxk=0.8, eyes='angry'),
            dict(bob=1, handN=(-A * 0.05, -A * 0.85), handF=(A * 0.2, -A * 0.4), wpn=-92, fx='charge', fxk=1.0, eyes='focus'),
            dict(bob=1, handN=(-A * 0.05, -A * 0.9), handF=(A * 0.3, -A * 0.5), wpn=-92, fx='charge', fxk=1.4, eyes='focus'),
            dict(handN=(A * 0.25, -A * 0.3), handF=(A * 0.3, A * 0.2), wpn=-70, fx='burst', fxk=1.0, eyes='angry'),
            dict(handN=(A * 0.1, A * 0.1), wpn=-80, fx='burst', fxk=0.5),
            dict(),
        ]
    return [P(s, **d) for d in seq]


# ════════════════════════════════════════════════════════════════
# 그리기
# ════════════════════════════════════════════════════════════════
def mats(s):
    """spec 색 → 색 띠. 먼 쪽 팔다리용 어두운 띠도 만듦"""
    m = {}
    for key in ('skin', 'cloth', 'cloth2', 'pants', 'boots', 'metal', 'leather', 'hair', 'bone', 'trim', 'wood', 'cape', 'gem'):
        if key in s:
            m[key] = ramp(s[key])
            m[key + '_far'] = ramp(darker(_c(s[key]), 0.78))
    return m


def _c(v):
    from core import hexc
    return hexc(v) if isinstance(v, str) else v


def draw(cv, s, p, view, ox, oy):
    """한 프레임 그리기. (ox, oy) = 캔버스에서 발 밑 기준점"""
    m = mats(s)
    pivot = (0.0, -s.get('body_r', s['torso_w']))
    xf = Xf(ox, oy + p['drop'], rot=p['rot'], pivot=pivot)
    g = Painter(cv, xf)

    leg, tor, A = s['leg'], s['torso'], s['arm']
    hip = (p['hipx'], -leg + p['bob'])
    up = rot((0.0, -1.0), p['lean'])
    neck = add(hip, up, tor)
    head_c = add(neck, rot((0.0, -1.0), p['lean'] * 0.6 + p['htilt']), s['head_ry'] * 0.82)
    side = view == 'side'
    W = s['torso_w']

    # 어깨·엉덩이 (앞에서는 좌우로 벌어짐)
    if side:
        shN = add(neck, up, -1.2)
        shN = (shN[0] + 0.6, shN[1])
        shF = (shN[0] - 1.4, shN[1] + 0.3)
        hipN, hipF = (hip[0] + 0.6, hip[1]), (hip[0] - 0.8, hip[1])
        footN = (p['footN'][0] + p['hipx'] * 0.6, -p['footN'][1])
        footF = (p['footF'][0] + p['hipx'] * 0.3, -p['footF'][1])
        hN = add(shN, p['handN'])
        hF = add(shF, p['handF'])
    else:
        sgn = -1 if view == 'front' else 1   # 무기 든 손(near)이 front에서는 화면 왼쪽
        sw = s['sh_w']
        shN = (neck[0] + sgn * sw, neck[1] + 1.2)
        shF = (neck[0] - sgn * sw, neck[1] + 1.2)
        hw = s['hip_w']
        hipN, hipF = (hip[0] + sgn * hw, hip[1]), (hip[0] - sgn * hw, hip[1])
        footN = (hipN[0] + sgn * 0.3, -p['footN'][1])
        footF = (hipF[0] - sgn * 0.3, -p['footF'][1])
        # 손: 팔을 몸 옆으로 내린 기본 위치 + 포즈의 x(몸 안쪽으로 +), y
        hN = (shN[0] + sgn * 0.8 - sgn * p['handN'][0], shN[1] + p['handN'][1])
        hF = (shF[0] - sgn * 0.8 + sgn * p['handF'][0] * 0.5, shF[1] + p['handF'][1])
    hN = reach(shN, hN, A * 0.98)
    hF = reach(shF, hF, A * 0.98)

    L1 = leg * 0.52
    L2 = leg * 0.52
    kneeN = ik(hipN, footN, L1, L2, -1 if side else (-1 if view == 'front' else 1) * 0.0 + (-1))
    kneeF = ik(hipF, footF, L1, L2, -1)
    if not side:
        # 앞뒤에서는 무릎이 살짝 바깥으로
        kneeN = ((hipN[0] + footN[0]) / 2 + (hipN[0] - hip[0]) * 0.25, (hipN[1] + footN[1]) / 2)
        kneeF = ((hipF[0] + footF[0]) / 2 + (hipF[0] - hip[0]) * 0.25, (hipF[1] + footF[1]) / 2)
    a1, a2 = A * 0.5, A * 0.5
    elbN = ik(shN, hN, a1, a2, 1 if side else (1 if view == 'front' else -1))
    elbF = ik(shF, hF, a1, a2, 1 if side else (-1 if view == 'front' else 1))

    J = dict(hip=hip, neck=neck, head=head_c, up=up, shN=shN, shF=shF, hN=hN, hF=hF,
             elbN=elbN, elbF=elbF, hipN=hipN, hipF=hipF, kneeN=kneeN, kneeF=kneeF, footN=footN, footF=footF)

    wang = p['wpn']
    if view == 'front':
        wang = 180 - wang if s['attack'] not in ('bow',) else wang  # 화면 왼쪽 손: 좌우 반전 각
    if view == 'back':
        wang = wang

    # ── 그리는 순서 ──
    if side:
        _arm(g, s, m, J, 'F', p, view, far=True)
        if s.get('shield'):
            _shield(g, s, m, J['hF'], view, far=True)
        if not s.get('mounted'):
            _leg(g, s, m, J, 'F', far=True)
        _torso(g, s, m, J, p, view)
        _leg(g, s, m, J, 'N', far=False)
        _robe(g, s, m, J, p, view)
        _head(g, s, m, J, p, view)
        if not p['weapon_drop']:
            _weapon(g, s, m, J['hN'], wang, p, view, J)
        _arm(g, s, m, J, 'N', p, view, far=False)
    elif view == 'front':
        if not p['wfront'] and not p['weapon_drop']:
            _weapon(g, s, m, J['hN'], wang, p, view, J)
        _leg(g, s, m, J, 'F', far=False)
        _leg(g, s, m, J, 'N', far=False)
        _torso(g, s, m, J, p, view)
        _robe(g, s, m, J, p, view)
        _head(g, s, m, J, p, view)
        _arm(g, s, m, J, 'F', p, view, far=False)
        if s.get('shield'):
            _shield(g, s, m, J['hF'], view, far=False)
        if p['wfront'] and not p['weapon_drop']:
            _weapon(g, s, m, J['hN'], wang, p, view, J)
        _arm(g, s, m, J, 'N', p, view, far=False)
    else:  # back
        if not p['weapon_drop']:
            _weapon(g, s, m, J['hN'], wang, p, view, J)
        if s.get('shield'):
            _shield(g, s, m, J['hF'], view, far=True)
        _arm(g, s, m, J, 'N', p, view, far=False)
        _arm(g, s, m, J, 'F', p, view, far=False)
        _leg(g, s, m, J, 'F', far=False)
        _leg(g, s, m, J, 'N', far=False)
        _torso(g, s, m, J, p, view)
        _robe(g, s, m, J, p, view)
        _head(g, s, m, J, p, view)
        if s.get('cape'):
            pass

    if p['weapon_drop']:
        # 땅에 떨어진 무기 (몸 회전과 상관없이 바닥에)
        gd = Painter(cv, Xf(ox, oy))
        _weapon(gd, s, m, (s['arm'] * 0.9, -1.2), 0 if view == 'side' else 10, dict(p, wlen=1.0, fx=None, string='rest', arrow=False), 'side', J, dropped=True)

    _fx(cv, g, s, p, J, wang, view)
    return J


# ─── 부위 ─────────────────────────────────────────────────────
def _leg(g, s, m, J, k, far):
    hip, knee, foot = J['hip' + k], J['knee' + k], J['foot' + k]
    if s.get('robe') and s.get('robe_hides_legs', True):
        # 긴 옷: 발만 살짝
        g.ell((foot[0] + s['foot_len'] * 0.3, foot[1] - s['limb'] * 0.7), s['foot_len'] * 0.75, s['limb'] * 0.85, m['boots_far' if far else 'boots'])
        return
    lr = s['limb']
    pants = m['pants_far' if far else 'pants']
    if s.get('bony'):
        bone = m['bone_far' if far else 'bone']
        g.cap(hip, knee, lr * 0.55, bone)
        g.cap(knee, foot, lr * 0.5, bone)
        g.ell(knee, lr * 0.75, lr * 0.75, bone)
        g.ell((foot[0] + s['foot_len'] * 0.35, foot[1] - lr * 0.45), s['foot_len'] * 0.7, lr * 0.55, bone)
        return
    g.cap(hip, knee, lr * 1.15, pants, r1=lr * 1.0)
    boots = m['boots_far' if far else 'boots']
    g.cap(knee, foot, lr * 1.0, pants if not s.get('tall_boots') else boots, r1=lr * 0.9)
    if s.get('tall_boots'):
        g.cap(lerp(knee, foot, 0.35), foot, lr * 1.08, boots)
    g.ell((foot[0] + s['foot_len'] * 0.35, foot[1] - lr * 0.75), s['foot_len'] * 0.8, lr * 0.95, boots)


def _arm(g, s, m, J, k, p, view, far):
    sh, elb, hand = J['sh' + k], J['elb' + k], J['h' + k]
    lr = s['limb'] * s.get('arm_k', 1.0)
    if s.get('bony'):
        bone = m['bone_far' if far else 'bone']
        g.cap(sh, elb, lr * 0.5, bone)
        g.cap(elb, hand, lr * 0.45, bone)
        g.ell(sh, lr * 0.85, lr * 0.85, m['metal' if s.get('pauldron') else ('bone_far' if far else 'bone')])
        g.ell(hand, s['hand_r'] * 0.8, s['hand_r'] * 0.8, bone)
        return
    sleeve = m[(s.get('sleeve', 'cloth')) + ('_far' if far else '')]
    skin = m['skin_far' if far else 'skin']
    g.cap(sh, elb, lr * 1.05, sleeve, r1=lr * 0.95)
    fore = m[(s.get('glove', 'skin')) + ('_far' if far else '')]
    g.cap(elb, hand, lr * 0.95, fore if s.get('long_sleeve') is None else sleeve, r1=lr * 0.85)
    if s.get('bracer'):
        g.cap(lerp(elb, hand, 0.35), lerp(elb, hand, 0.85), lr * 1.05, m[s['bracer'] + ('_far' if far else '')])
    if s.get('pauldron') and not far:
        g.ell(add(sh, (0, -0.5)), lr * 1.7, lr * 1.35, m['metal'])
    g.ell(hand, s['hand_r'], s['hand_r'], m[(s.get('glove', 'skin')) + ('_far' if far else '')])


def _torso(g, s, m, J, p, view):
    hip, neck, up = J['hip'], J['neck'], J['up']
    side = view == 'side'
    W = s['torso_w'] if not side else s['torso_d']
    if s.get('bony'):
        bone = m['bone']
        # 등뼈 + 갈비뼈 + 골반
        g.cap(hip, neck, W * 0.22, bone)
        rib_c = lerp(hip, neck, 0.62)
        g.ell(rib_c, W * 0.95, s['torso'] * 0.36, bone, ang=p['lean'])
        cv = g.cv
        # 갈비뼈 사이 틈
        for i in range(3):
            y = -s['torso'] * 0.22 + i * s['torso'] * 0.17
            a = add(rib_c, rot((-W * 0.75, y), p['lean']))
            b = add(rib_c, rot((W * 0.75, y), p['lean']))
            if side:
                a = add(rib_c, rot((-W * 0.2, y), p['lean']))
            g.line(a, b, m['bone'][0])
        g.ell(add(hip, up, 0.8), W * 0.75, s['torso'] * 0.16, bone, ang=p['lean'])
        if s.get('armor'):
            _armor(g, s, m, J, p, view)
        return
    body = m[s.get('body_mat', 'cloth')]
    g.cap(add(hip, up, s['limb'] * 0.6), add(neck, up, -s['limb'] * 0.6), W * 0.9, body, r1=W * s.get('chest_k', 1.05))
    # 배 (살짝 나온 배: 오크·고블린)
    if s.get('belly'):
        bc = add(lerp(hip, neck, 0.32), (W * 0.15 if side else 0, 0))
        g.ell(bc, W * (0.75 if not side else 0.7), s['torso'] * 0.28, m[s['belly']])
    if s.get('armor'):
        _armor(g, s, m, J, p, view)
    # 허리띠
    if s.get('belt'):
        b0 = add(hip, up, s['torso'] * 0.18)
        ww = W * 0.95
        a, b = add(b0, rot((-ww, 0), p['lean'])), add(b0, rot((ww, 0), p['lean']))
        g.cap(a, b, 1.0, m['leather'])
        if view != 'back':
            g.px(add(b0, rot((W * 0.25 if side else 0, 0), p['lean'])), ramp(s.get('buckle', '#d4a84a'))[3])
    # 목도리·스카프
    if s.get('scarf'):
        sc = add(neck, up, -0.6)
        g.ell(sc, W * 0.85, 1.8, m['trim'], ang=p['lean'])


def _armor(g, s, m, J, p, view):
    hip, neck, up = J['hip'], J['neck'], J['up']
    side = view == 'side'
    W = (s['torso_w'] if not side else s['torso_d']) * 1.05
    c = lerp(hip, neck, 0.6)
    g.ell(c, W, s['torso'] * 0.42, m['metal'], ang=p['lean'])
    if view == 'front':
        g.line(add(c, rot((0, -s['torso'] * 0.35), p['lean'])), add(c, rot((0, s['torso'] * 0.3), p['lean'])), m['metal'][1])


def _robe(g, s, m, J, p, view):
    if not s.get('robe'):
        return
    hip, neck, up = J['hip'], J['neck'], J['up']
    side = view == 'side'
    W = s['torso_w'] if not side else s['torso_d']
    rb = m[s['robe']]
    top_y = s['torso'] * 0.15
    flare = s.get('robe_flare', 1.6)
    sway = (J['footN'][0] - J['footF'][0]) * 0.25 if side else 0.0
    lift = (J['footN'][1] + J['footF'][1]) * 0.25
    tl = add(hip, rot((-W * 0.95, -top_y), p['lean']))
    tr = add(hip, rot((W * 0.95, -top_y), p['lean']))
    hem = -s['limb'] * 0.9 + lift
    bl = (hip[0] - W * flare + sway * 0.5, hem)
    br = (hip[0] + W * flare + sway, hem)
    g.poly([tl, tr, br, (hip[0] + W * 0.3 + sway, hem + 0.8), (hip[0] - W * 0.5, hem + 0.6), bl], rb, shade=0.2, grad=((-0.6, -0.4), 0.55))
    # 옷 주름
    for t in (0.35, 0.68):
        a = lerp(tl, tr, t)
        b = lerp(bl, br, t)
        g.line(lerp(a, b, 0.35), lerp(a, b, 0.92), rb[1])
    if s.get('trim'):
        g.line((bl[0] + 0.5, hem - 0.5), (br[0] - 0.5, hem - 0.5), m['trim'][3])
    # 윗옷(상체)
    g.cap(add(hip, up, s['limb'] * 0.4), add(neck, up, -s['limb'] * 0.5), W * 0.85, rb, r1=W * s.get('chest_k', 1.0))
    if s.get('trim') and view == 'front':
        g.line(add(neck, (0, 0.5)), add(hip, (0, 2)), m['trim'][3])


def _head(g, s, m, J, p, view):
    c = J['head']
    rx, ry = s['head_rx'], s['head_ry']
    side = view == 'side'
    lean = p['lean'] * 0.6 + p['htilt']
    skin = m['bone'] if s.get('bony') else m['skin']
    hr = lambda v: add(c, rot(v, lean))   # 머리 기준 좌표 (기울기 반영)

    # 뒤쪽 장식 (뒤로 넘어간 귀, 뒷머리, 망토 두건 꼬리)
    ears = s.get('ears')
    if ears and (side or view == 'back'):
        _ears(g, s, m, hr, view, behind=True)
    if s.get('hair') == 'mohawk':
        pass

    # 머리 본체
    if s.get('bony'):
        g.ell(hr((0.5 if side else 0, 0)), rx * (0.9 if side else 1.0), ry, skin, ang=lean)
        # 턱
        jaw = hr((rx * 0.35 if side else 0, ry * 0.75))
        g.ell(jaw, rx * (0.55 if side else 0.62), ry * 0.38, skin, ang=lean)
    else:
        g.ell(hr((0, 0)), rx * (0.92 if side else 1.0), ry, skin, ang=lean)
        if s.get('jaw'):
            g.ell(hr((rx * 0.3 if side else 0, ry * 0.55)), rx * s['jaw'] * (0.7 if side else 1.0), ry * 0.42, skin, ang=lean)

    if ears and view == 'front':
        _ears(g, s, m, hr, view, behind=False)

    # 얼굴
    if view == 'front':
        _face_front(g, s, m, hr, p)
    elif side:
        _face_side(g, s, m, hr, p)
        if ears:
            _ears(g, s, m, hr, view, behind=False)

    # 머리 위 (두건·투구·왕관·머리카락)
    hat = s.get('hat')
    if hat:
        _hat(g, s, m, hr, view, lean, hat)


def _ears(g, s, m, hr, view, behind):
    rx, ry = s['head_rx'], s['head_ry']
    kind = s['ears']
    skin = m['skin'] if not behind else m['skin_far']
    if kind == 'goblin':
        L = rx * 1.2
        inner = ramp(mixc(_c(s['skin']), (200, 110, 110), 0.35))
        def one(sg, rmp, tilt):
            base_t = hr((sg * rx * 0.7, -ry * 0.28))
            tip = hr((sg * (rx + L), -ry * (0.55 + tilt)))
            base_b = hr((sg * rx * 0.68, ry * 0.38))
            mid_b = hr((sg * (rx + L * 0.45), ry * 0.05))
            g.poly([base_t, tip, mid_b, base_b], rmp, shade=0.25, grad=((0, -1), 0.5))
            g.poly([lerp(base_t, base_b, 0.25), lerp(base_t, tip, 0.8), lerp(base_b, mid_b, 0.7)], inner, shade=-0.2, line=False, dither=False)
        if view == 'front':
            one(-1, m['skin'], 0.0)
            one(1, m['skin'], 0.0)
        elif view == 'side':
            base_t = hr((-rx * 0.05, -ry * 0.35))
            tip = hr((-rx - L * 0.9, -ry * 0.8))
            base_b = hr((-rx * 0.15, ry * 0.4))
            mid_b = hr((-rx - L * 0.3, ry * 0.0))
            g.poly([base_t, tip, mid_b, base_b], m['skin'], shade=0.3, grad=((0, -1), 0.5))
            g.poly([lerp(base_t, base_b, 0.3), lerp(base_t, tip, 0.8), lerp(base_b, mid_b, 0.7)], inner, shade=-0.2, line=False, dither=False)
        else:
            one(-1, skin, 0.0)
            one(1, skin, 0.0)
    elif kind == 'orc':
        if view == 'side':
            g.poly([hr((-rx * 0.2, -ry * 0.2)), hr((-rx * 1.1, -ry * 0.45)), hr((-rx * 0.25, ry * 0.25))], m['skin'], shade=0.2)
        else:
            for sg in (-1, 1):
                g.poly([hr((sg * rx * 0.85, -ry * 0.2)), hr((sg * rx * 1.45, -ry * 0.5)), hr((sg * rx * 0.85, ry * 0.25))], skin, shade=0.2)


def _eye(g, at, s, p, glint=True, w=2, h=3, iris=None, outer=-1):
    """애니 눈: 위 속눈썹 선 + 위는 밝고 아래는 짙은 홍채 + 동공 + 반짝임 두 개. outer: 눈꼬리 쪽 (-1 왼쪽, +1 오른쪽)"""
    iris = iris or s['eye']
    ir = ramp(iris)
    line = (26, 14, 24)
    x, y = at
    mood = p['eyes']
    if mood == 'x':
        for i in range(3):
            g.px((x + i, y + i), line)
            g.px((x + 2 - i, y + i), line)
        return
    if mood == 'hurt':
        # 질끈 감은 눈 (> <)
        g.px((x, y + 1), line)
        for i in range(1, w):
            g.px((x + i, y + 2), line)
        g.px((x + w - 1, y + 1), line)
        return
    for i in range(-1 if outer < 0 else 0, w + (1 if outer > 0 else 0)):
        g.px((x + i, y), line)
    for j in range(1, h):
        t = (j - 1) / max(1, h - 2)
        col = ir[3] if t < 0.34 else (ir[2] if t < 0.75 else ir[1])
        for i in range(w):
            g.px((x + i, y + j), col)
    # 동공
    pc = w // 2 if w > 2 else (0 if outer > 0 else w - 1)
    for j in range(max(1, h - 3), h - 1 if h > 3 else h):
        g.px((x + pc, y + j), mixc(ir[0], line, 0.5))
    if glint:
        gx = x if outer < 0 else x + w - 1
        g.px((gx, y + 1), (255, 255, 246))
        if h >= 4 and w >= 2:
            g.px((x + w - 1 - (gx - x), y + h - 1), ir[4])
    if mood == 'angry':
        # 찌푸린 눈썹: 안쪽이 내려감
        inner = x + w if outer < 0 else x - 1
        outer_x = x - 1 if outer < 0 else x + w
        g.px((inner, y - 0), line)
        g.px(((inner + outer_x) / 2, y - 1), line)
    elif mood == 'focus':
        for i in range(w):
            g.px((x + i, y + 1), line)


def _face_front(g, s, m, hr, p):
    rx, ry = s['head_rx'], s['head_ry']
    ex = s.get('eye_x', 0.44) * rx
    ey = s.get('eye_y', 0.12) * ry
    ew, eh = s.get('eye_w', 3), s.get('eye_h', 4)
    if s.get('bony'):
        for sg in (-1, 1):
            c = hr((sg * ex, ey))
            g.ell(c, 1.6, 1.8, ramp('#1a1018'))
            glow = ramp(s['eye'])
            if p['eyes'] != 'x':
                g.px((c[0] - 0.5, c[1] - 0.2), glow[4], glow=True)
                g.px((c[0] + 0.3, c[1] - 0.2), glow[3], glow=True)
        g.px(hr((0, ey + 2.2)), (30, 18, 26))
        for i in range(-2, 3):
            g.px(hr((i, ry * 0.72)), m['bone'][1] if i % 2 else m['bone'][3])
        return
    for sg in (-1, 1):
        at = hr((sg * ex - ew * 0.5, ey - eh * 0.5))
        _eye(g, at, s, p, w=ew, h=eh, outer=sg)
    # 볼 홍조 (애니 느낌)
    if s.get('blush'):
        for sg in (-1, 1):
            g.px(hr((sg * (ex + 1.2), ey + eh * 0.7)), mixc(m['skin'][2], (230, 90, 90), 0.45))
    # 코
    if s.get('nose') == 'long':
        g.poly([hr((-0.8, ey + 0.5)), hr((0.8, ey + 0.5)), hr((0, ey + 3.2))], m['skin'], shade=0.35)
    else:
        g.px(hr((0, ey + 2.0)), m['skin'][1])
    # 입
    my = ry * s.get('mouth_y', 0.55)
    mw = s.get('mouth_w', 2)
    if p['eyes'] in ('angry', 'hurt', 'focus') or s.get('grin'):
        for i in range(-mw, mw + 1):
            g.px(hr((i, my)), (40, 14, 20))
        if s.get('grin'):
            g.px(hr((-mw + 1, my - 0.9)), (250, 246, 230))
            g.px(hr((mw - 1, my - 0.9)), (250, 246, 230))
    else:
        g.line(hr((-mw * 0.5, my)), hr((mw * 0.5, my)), m['skin'][0])
    if s.get('tusks'):
        for sg in (-1, 1):
            g.px(hr((sg * (mw + 0.5), my - 0.5)), (246, 236, 210))
            g.px(hr((sg * (mw + 0.5), my - 1.5)), (220, 210, 186))
    if s.get('mask'):
        # 도적 복면: 코 아래를 덮음
        g.poly([hr((-rx * 0.98, ey + 1.6)), hr((rx * 0.98, ey + 1.6)), hr((rx * 0.8, ry * 0.8)), hr((0, ry * 1.02)), hr((-rx * 0.8, ry * 0.8))],
               m['trim'], shade=0.1, grad=((-0.5, -1), 0.5))


def _face_side(g, s, m, hr, p):
    rx, ry = s['head_rx'], s['head_ry']
    ex = s.get('eye_x_side', 0.5) * rx
    ey = s.get('eye_y', 0.12) * ry
    ew, eh = max(1, s.get('eye_w', 3) - 1), s.get('eye_h', 4)
    if s.get('bony'):
        c = hr((ex, ey))
        g.ell(c, 1.4, 1.7, ramp('#1a1018'))
        glow = ramp(s['eye'])
        if p['eyes'] != 'x':
            g.px((c[0] + 0.2, c[1] - 0.2), glow[4], glow=True)
        g.px(hr((rx * 0.85, ey + 2.2)), (30, 18, 26))
        for i in range(3):
            g.px(hr((rx * 0.45 + i, ry * 0.72)), m['bone'][1] if i % 2 else m['bone'][3])
        return
    _eye(g, hr((ex - ew * 0.5, ey - eh * 0.5)), s, p, w=ew, h=eh, outer=-1)
    if s.get('nose') == 'long':
        g.poly([hr((rx * 0.75, ey + 0.2)), hr((rx * 1.55, ey + 2.4)), hr((rx * 0.8, ey + 2.6))], m['skin'], shade=0.3)
    else:
        g.px(hr((rx * 0.95, ey + 1.5)), m['skin'][3])
    my = ry * s.get('mouth_y', 0.55)
    if p['eyes'] in ('angry', 'hurt', 'focus') or s.get('grin'):
        g.line(hr((rx * 0.45, my)), hr((rx * 0.85, my)), (40, 14, 20))
        if s.get('grin'):
            g.px(hr((rx * 0.65, my - 0.9)), (250, 246, 230))
    else:
        g.px(hr((rx * 0.7, my)), m['skin'][0])
    if s.get('tusks'):
        g.px(hr((rx * 0.75, my - 0.5)), (246, 236, 210))
        g.px(hr((rx * 0.75, my - 1.5)), (220, 210, 186))
    if s.get('mask'):
        g.poly([hr((-rx * 0.1, ey + 1.6)), hr((rx * 1.02, ey + 1.6)), hr((rx * 0.85, ry * 0.85)), hr((-rx * 0.1, ry * 0.9))], m['trim'], shade=0.1)


def _hat(g, s, m, hr, view, lean, hat):
    rx, ry = s['head_rx'], s['head_ry']
    side = view == 'side'
    back = view == 'back'
    if hat == 'hood':
        cl = m['cape' if 'cape' in m else 'cloth']
        if back:
            g.ell(hr((0, -0.3)), rx * 1.12, ry * 1.08, cl, ang=lean)
            g.poly([hr((-rx * 0.8, ry * 0.4)), hr((rx * 0.8, ry * 0.4)), hr((0, ry * 1.5))], cl, shade=0.0)
            return
        if side:
            g.poly([hr((-rx * 1.15, -ry * 0.2)), hr((-rx * 0.6, -ry * 1.12)), hr((rx * 0.55, -ry * 1.1)), hr((rx * 1.0, -ry * 0.35)),
                    hr((rx * 0.35, -ry * 0.4)), hr((-rx * 0.1, ry * 0.2)), hr((-rx * 0.6, ry * 1.0)), hr((-rx * 1.3, ry * 0.9))],
                   cl, shade=0.15, grad=((-0.5, -1), 0.6))
            return
        # 앞: 얼굴 둘레를 감싸는 두건
        g.poly([hr((-rx * 1.18, ry * 0.8)), hr((-rx * 1.2, -ry * 0.3)), hr((-rx * 0.7, -ry * 1.1)), hr((0, -ry * 1.25)), hr((rx * 0.7, -ry * 1.1)),
                hr((rx * 1.2, -ry * 0.3)), hr((rx * 1.18, ry * 0.8)), hr((rx * 0.85, ry * 0.75)), hr((rx * 0.85, -ry * 0.2)), hr((rx * 0.4, -ry * 0.55)),
                hr((-rx * 0.4, -ry * 0.55)), hr((-rx * 0.85, -ry * 0.2)), hr((-rx * 0.85, ry * 0.75))], cl, shade=0.15, grad=((-0.5, -1), 0.6))
        return
    if hat in ('helmet', 'horned', 'spiked'):
        mt = m['metal']
        if side:
            g.poly([hr((-rx * 1.05, ry * 0.15)), hr((-rx * 1.0, -ry * 0.6)), hr((-rx * 0.3, -ry * 1.12)), hr((rx * 0.5, -ry * 1.05)), hr((rx * 1.05, -ry * 0.4)),
                    hr((rx * 1.05, -ry * 0.1)), hr((rx * 0.3, -ry * 0.05)), hr((-rx * 0.2, ry * 0.35))], mt, shade=0.15, grad=((-0.6, -1), 0.7))
        else:
            g.poly([hr((-rx * 1.08, ry * 0.15)), hr((-rx * 1.0, -ry * 0.65)), hr((-rx * 0.45, -ry * 1.12)), hr((rx * 0.45, -ry * 1.12)), hr((rx * 1.0, -ry * 0.65)),
                    hr((rx * 1.08, ry * 0.15)), hr((rx * 0.8, -ry * 0.12 if not back else ry * 0.2)), hr((-rx * 0.8, -ry * 0.12 if not back else ry * 0.2))],
                   mt, shade=0.15, grad=((-0.6, -1), 0.7))
            if view == 'front':
                g.cap(hr((0, -ry * 0.2)), hr((0, ry * 0.25)), 0.6, mt)  # 코 가리개
        g.line(hr((-rx * 0.95, -ry * 0.15)), hr((rx * 0.95, -ry * 0.15)) if not side else hr((rx * 1.0, -ry * 0.15)), mt[1])
        if hat == 'horned':
            hn = ramp('#e8dcc0')
            for sg in ((-1, 1) if not side else (1,)):
                bx = sg * rx * 0.85 if not side else -rx * 0.1
                g.poly([hr((bx - 1.0, -ry * 0.55)), hr((bx + 1.2, -ry * 0.75)), hr((bx + sg * 3.2 if not side else bx - 3.0, -ry * 1.75))], hn, shade=0.3)
        if hat == 'spiked':
            g.poly([hr((-1.0, -ry * 1.05)), hr((1.0, -ry * 1.05)), hr((0, -ry * 1.7))], mt, shade=0.4)
        return
    if hat == 'crown':
        gd = ramp('#e0b040')
        base = -ry * 0.75
        if side:
            pts = [hr((-rx * 0.7, base + 1.5)), hr((-rx * 0.7, base - 2.5)), hr((-rx * 0.3, base - 1)), hr((0, base - 3.2)), hr((rx * 0.35, base - 1)),
                   hr((rx * 0.75, base - 2.5)), hr((rx * 0.75, base + 1.5))]
        else:
            pts = [hr((-rx * 0.85, base + 1.5)), hr((-rx * 0.9, base - 2.8)), hr((-rx * 0.45, base - 1)), hr((0, base - 3.6)), hr((rx * 0.45, base - 1)),
                   hr((rx * 0.9, base - 2.8)), hr((rx * 0.85, base + 1.5))]
        g.poly(pts, gd, shade=0.35, grad=((-0.6, -1), 0.6))
        if not back:
            g.px(hr((0, base - 0.2)), ramp('#d83040')[3])
        return
    if hat == 'bone_crown':
        bn = ramp('#e8dcc0')
        for i, x in enumerate((-0.8, -0.4, 0, 0.4, 0.8) if not side else (-0.6, -0.2, 0.2, 0.6)):
            h = 3.5 if i % 2 == 0 else 2.2
            g.poly([hr((rx * x - 0.9, -ry * 0.7)), hr((rx * x + 0.9, -ry * 0.7)), hr((rx * x, -ry * 0.7 - h))], bn, shade=0.3)
        g.cap(hr((-rx * 0.9 if not side else -rx * 0.7, -ry * 0.68)), hr((rx * 0.9 if not side else rx * 0.7, -ry * 0.68)), 1.0, ramp('#7a3a2a'))
        if not back:
            g.ell(hr((0 if not side else rx * 0.2, -ry * 0.95)), 1.4, 1.2, ramp('#40e0a0'), glow=True)
        return
    if hat == 'mohawk':
        hrm = m['hair']
        if side:
            g.poly([hr((-rx * 0.8, -ry * 0.6)), hr((-rx * 0.2, -ry * 1.35)), hr((rx * 0.5, -ry * 1.2)), hr((rx * 0.4, -ry * 0.85))], hrm, shade=0.2)
        else:
            g.poly([hr((-1.3, -ry * 0.85)), hr((0, -ry * 1.45)), hr((1.3, -ry * 0.85))], hrm, shade=0.25)
        return
    if hat == 'topknot':
        hrm = m['hair']
        if back or not side:
            g.ell(hr((0, -ry * 0.75)), rx * 0.95, ry * 0.4, hrm)
        g.ell(hr((0 if not side else -rx * 0.3, -ry * 1.15)), 1.8, 1.6, hrm)
        return
    if hat == 'cowl':
        # 사령술사 두건: 뾰족하고 길게
        cl = m['robe_far'] if 'robe_far' in m else m['cloth']
        cl = m[s['robe']] if s.get('robe') else m['cloth']
        if back:
            g.poly([hr((-rx * 1.2, ry * 0.9)), hr((-rx * 1.1, -ry * 0.6)), hr((0, -ry * 1.7)), hr((rx * 1.1, -ry * 0.6)), hr((rx * 1.2, ry * 0.9))],
                   cl, shade=0.1, grad=((-0.5, -1), 0.6))
            return
        if side:
            g.poly([hr((-rx * 1.25, ry * 0.95)), hr((-rx * 1.2, -ry * 0.4)), hr((-rx * 0.5, -ry * 1.75)), hr((rx * 0.75, -ry * 1.0)), hr((rx * 1.05, -ry * 0.2)),
                    hr((rx * 0.4, -ry * 0.45)), hr((-rx * 0.15, ry * 0.4)), hr((-rx * 0.4, ry * 1.0))], cl, shade=0.1, grad=((-0.5, -1), 0.6))
            return
        g.poly([hr((-rx * 1.25, ry * 0.95)), hr((-rx * 1.2, -ry * 0.4)), hr((0, -ry * 1.75)), hr((rx * 1.2, -ry * 0.4)), hr((rx * 1.25, ry * 0.95)),
                hr((rx * 0.8, ry * 0.85)), hr((rx * 0.75, -ry * 0.3)), hr((0, -ry * 0.7)), hr((-rx * 0.75, -ry * 0.3)), hr((-rx * 0.8, ry * 0.85))],
               cl, shade=0.1, grad=((-0.5, -1), 0.6))
        return
    if hat == 'feather':
        # 사냥꾼: 가죽 모자 + 깃털
        lt = m['leather']
        g.ell(hr((0, -ry * 0.6)), rx * 1.05, ry * 0.55, lt)
        g.cap(hr((-rx * 1.25 if not side else -rx * 0.9, -ry * 0.35)), hr((rx * 1.25 if not side else rx * 1.3, -ry * 0.35)), 0.9, lt)
        fr = ramp('#c83a2a')
        g.cap(hr((-rx * 0.4, -ry * 0.8)), hr((-rx * 1.2, -ry * 1.9)), 0.9, fr, r1=0.4)
        return


def _shield(g, s, m, hand, view, far):
    kind = s['shield']
    mt = m['metal_far' if far else 'metal']
    wd = m['wood_far' if far else 'wood'] if 'wood' in m else mt
    if kind == 'round':
        r = s['arm'] * 0.55
        c = (hand[0] + (1.5 if view == 'side' else 0), hand[1] - 1)
        rx = r * (0.55 if view == 'side' else 1.0)
        g.ell(c, rx, r, wd)
        g.ell(c, rx * 0.35, r * 0.35, mt)
        if view != 'back':
            g.ell(c, rx * 0.86, r * 0.86, wd, line=False) if False else None
    elif kind == 'tower':
        h = s['arm'] * 1.25
        w = s['arm'] * (0.35 if view == 'side' else 0.62)
        c = (hand[0] + (1.5 if view == 'side' else 0), hand[1] - 1)
        g.poly([(c[0] - w, c[1] - h * 0.55), (c[0] + w, c[1] - h * 0.55), (c[0] + w, c[1] + h * 0.35), (c[0], c[1] + h * 0.55), (c[0] - w, c[1] + h * 0.35)],
               mt, shade=0.15, grad=((-0.6, -1), 0.6))
        if view != 'back':
            g.line((c[0], c[1] - h * 0.45), (c[0], c[1] + h * 0.45), mt[1])
            g.line((c[0] - w * 0.8, c[1] - h * 0.1), (c[0] + w * 0.8, c[1] - h * 0.1), mt[1])
            g.ell((c[0], c[1] - h * 0.1), 1.4, 1.4, ramp('#c03030'))


def _weapon(g, s, m, hand, ang, p, view, J, dropped=False):
    kind = s['weapon']
    L = s['wlen'] * p['wlen']
    d = dirv(ang)
    n = (-d[1], d[0])
    mt = m['metal']
    wd = m['wood'] if 'wood' in m else ramp('#7a4a2a')
    at = lambda t, o=0.0: (hand[0] + d[0] * t + n[0] * o, hand[1] + d[1] * t + n[1] * o)
    if kind in ('sword', 'dagger', 'rusty'):
        bw = s.get('blade_w', 1.3)
        g.cap(at(-2.2), at(0.5), 0.8, ramp('#5a3420'))
        g.ell(at(-2.6), 0.9, 0.9, ramp('#c8a040'))
        g.cap(at(1.0, -2.4), at(1.0, 2.4), 0.75, ramp('#c8a040'))
        blade = ramp('#9a8a70') if kind == 'rusty' else mt
        g.poly([at(1.4, -bw), at(L - 2.5, -bw), at(L, 0), at(L - 2.5, bw), at(1.4, bw)], blade, shade=0.35, grad=((n[0], n[1]), -0.5))
        g.line(at(2.0, 0), at(L - 2.5, 0), blade[4] if kind != 'rusty' else blade[3])
        if kind == 'rusty':
            for t in (0.4, 0.7):
                g.px(at(L * t, bw * 0.5), ramp('#8a4a20')[2])
    elif kind == 'spear':
        g.cap(at(-L * 0.45), at(L * 0.85), 0.75, wd)
        g.poly([at(L * 0.82, -1.5), at(L + 3.0, 0), at(L * 0.82, 1.5)], mt, shade=0.35)
        g.cap(at(L * 0.78, -1.1), at(L * 0.78, 1.1), 0.6, ramp('#c83a2a'))
    elif kind in ('axe', 'double_axe'):
        g.cap(at(-L * 0.25), at(L * 0.95), 0.85, wd)
        sides = (1,) if kind == 'axe' else (1, -1)
        for sg in sides:
            hb = L * 0.72
            g.poly([at(hb + 1.5, sg * 0.8), at(hb + 3.0, sg * 4.6), at(hb - 0.5, sg * 6.2), at(hb - 3.5, sg * 4.4), at(hb - 1.5, sg * 0.8)],
                   mt, shade=0.3, grad=((n[0] * sg, n[1] * sg), 0.4))
            g.line(at(hb + 2.8, sg * 4.6), at(hb - 3.2, sg * 4.6), mt[4])
    elif kind == 'club':
        g.cap(at(-1.5), at(L), 1.0, wd, r1=2.6)
        for t, o in ((0.55, 2.2), (0.75, -2.6), (0.92, 2.8), (0.7, 0.4)):
            g.px(at(L * t, o), mt[3])
            g.px(at(L * t, o * 1.25), mt[1])
    elif kind == 'mace':
        g.cap(at(-1.5), at(L * 0.8), 0.8, wd)
        c = at(L * 0.92)
        g.ell(c, 2.6, 2.6, mt)
        for a in range(0, 360, 60):
            q = add(c, dirv(a, 3.4))
            g.px(q, mt[1])
    elif kind == 'staff':
        g.cap(at(-L * 0.42), at(L * 0.86), 0.85, wd)
        top = at(L * 0.95)
        orn = s.get('staff_top', 'orb')
        if orn == 'skull':
            g.ell(top, 2.2, 2.0, ramp('#e8dcc0'))
            g.px(add(top, (-0.8, 0)), (30, 12, 20))
            g.px(add(top, (0.8, 0)), (30, 12, 20))
        elif orn == 'totem':
            g.cap(add(top, (-2.5, -1)), add(top, (2.5, -1)), 1.2, ramp('#8a5a3a'))
            g.ell(add(top, (0, -2.5)), 2.0, 2.0, ramp('#e8dcc0'))
        else:
            g.cap(add(top, (-1.6, 1)), add(top, (0, -1.2)), 0.6, wd)
            g.cap(add(top, (1.6, 1)), add(top, (0, -1.2)), 0.6, wd)
        gem = ramp(s.get('magic', '#50c0ff'))
        g.ell(add(top, (0, -0.3 if orn != 'totem' else -2.5)), 1.4, 1.4, gem, glow=True)
    elif kind == 'bow':
        # 활대: 손을 지나 조준 방향에 수직인 호
        R = L * 0.55
        c = at(-R * 0.55)
        bow = wd
        pts_out, pts_in = [], []
        for i in range(13):
            a = math.radians(ang - 62 + i * (124 / 12))
            pts_out.append((c[0] + math.cos(a) * R, c[1] + math.sin(a) * R))
            pts_in.append((c[0] + math.cos(a) * (R - 1.4), c[1] + math.sin(a) * (R - 1.4)))
        g.poly(pts_out + pts_in[::-1], bow, shade=0.3)
        t0, t1 = pts_out[0], pts_out[-1]
        if p['string'] in ('pull', 'full') and view == 'side':
            pull = J['hF']
            g.line(t0, pull, (230, 226, 210))
            g.line(pull, t1, (230, 226, 210))
            if p['arrow']:
                tip = at(R * 0.7)
                g.line(pull, tip, ramp('#8a6a40')[3])
                g.poly([add(tip, n, -1), add(tip, d, 2.2), add(tip, n, 1)], mt, shade=0.4)
                g.px(add(pull, d, 0.6), (220, 60, 50))
        else:
            g.line(t0, t1, (230, 226, 210))
            if p['arrow']:
                tip = at(R * 0.6)
                g.line(at(-R * 1.0), tip, ramp('#8a6a40')[3])


def _fx(cv, g, s, p, J, wang, view):
    fx = p['fx']
    k = p['fxk']
    hand = J['hN']
    if p['smear'] and not p['weapon_drop']:
        import numpy as np
        a0, a1 = p['smear']
        a0 = max(a0, a1 - 125)            # 마지막 125도만 (초승달)
        lead_end = True
        if view == 'front':
            a0, a1 = 180 - a1, 180 - a0
            lead_end = False
        sh = J['shN']
        R = math.hypot(J['hN'][0] - sh[0], J['hN'][1] - sh[1]) + s['wlen'] * p['wlen']
        c = g.xf(sh)
        lo, hi = g.xf.ang(min(a0, a1)), g.xf.ang(max(a0, a1))
        if g.xf.flip:
            lo, hi = hi, lo
        mring, t = cv.ring(c, R + 0.6, R - 4.0, lo, hi)
        lead = t if lead_end else 1 - t
        dist = np.sqrt((cv.X - c[0]) ** 2 + (cv.Y - c[1]) ** 2)
        thick = 0.6 + lead * 3.6            # 앞쪽(칼끝이 도착한 쪽)일수록 두꺼움
        band = mring & (dist >= R + 0.6 - thick)
        cv.rgb[band] = np.array([214, 228, 255], np.float32)
        core_ = band & (dist >= R + 0.6 - thick * 0.45)
        cv.rgb[core_] = np.array([255, 255, 255], np.float32)
        cv.a[band] = True
        cv.noline[band] = True
    if not fx:
        return
    magic = ramp(s.get('magic', '#50c0ff'))
    if fx in ('orb', 'charge', 'burst') and s['weapon'] == 'staff' and not p['weapon_drop']:
        d = dirv(wang)
        top = (hand[0] + d[0] * s['wlen'] * 0.95, hand[1] + d[1] * s['wlen'] * 0.95)
        if s.get('staff_top') == 'totem':
            top = add(top, (0, -2.5))
        if fx == 'orb':
            r = 1.6 + k
            g.ell(top, r, r, magic, glow=True, line=False)
        elif fx == 'charge':
            r = 2.2 + k * 1.6
            g.ell(top, r, r, magic, glow=True, line=False)
            g.ell(top, r * 0.5, r * 0.5, [magic[4]] * 5, glow=True, line=False)
            for a in range(0, 360, 45):
                q = add(top, dirv(a + k * 30, r + 2 + (a % 90 == 0) * 1.5))
                g.px(q, magic[3], glow=True)
        else:
            r = 3.5 + k * 3
            for a in range(0, 360, 30):
                g.line(add(top, dirv(a, r * 0.4)), add(top, dirv(a, r * (1.0 if a % 60 == 0 else 0.7))), magic[3], glow=True)
            g.ell(top, r * 0.45, r * 0.45, [magic[4]] * 5, glow=True, line=False)
    elif fx == 'dust':
        gd = Painter(cv, Xf(g.xf.ox, g.xf.oy))
        dust = ramp('#b8a888')
        x0 = J['hN'][0] + s['wlen'] * 0.6
        for i, (dx, r) in enumerate(((-3, 1.8 * k + 0.5), (0, 2.4 * k + 0.6), (3.5, 1.6 * k + 0.5), (6, 1.2 * k))):
            if r < 0.6:
                continue
            gd.ell((x0 + dx * (1.4 - k * 0.3), -r * 0.7), r * 1.2, r * 0.8, dust, glow=True, line=False)
    elif fx == 'lines':
        # 속도선
        for i in range(3):
            y = J['hN'][1] + (i - 1) * 2.5
            x1 = J['hN'][0] - 2 - i
            g.line((x1 - 6 * k, y), (x1, y), (255, 248, 230), glow=True)
    elif fx == 'spark':
        d = dirv(wang)
        tip = (hand[0] + d[0] * s['wlen'] * p['wlen'], hand[1] + d[1] * s['wlen'] * p['wlen'])
        for a in range(0, 360, 45):
            ln = 2.5 if a % 90 == 0 else 1.5
            g.line(add(tip, dirv(a, 0.8)), add(tip, dirv(a, ln * k + 0.8)), (255, 244, 200), glow=True)
        g.px(tip, (255, 255, 255), glow=True)
