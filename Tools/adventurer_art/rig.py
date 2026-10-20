# 모험가 리그: 포즈 → 관절 → 부위별(몸·하의·상의·헤어·무기) × 층별 그리기
# 층(SUBS) 순서대로, 같은 층 안에서는 PARTS 순서대로 그려야 겹침이 맞음:
#   back(뒤로 늘어진 머리, 등 뒤 무기) → far(먼 쪽 팔다리) → mid(몸통·다리) → head(머리·얼굴·헤어) → near(가까운 팔·무기·효과)
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'monster_art'))
import humanoid  # noqa: E402
from core import Canvas, Painter, Xf, add, darker, dirv, hexc, ik, lerp, mixc, ramp, reach, rot  # noqa: E402

import parts as PD  # noqa: E402

SUBS = ['back', 'far', 'mid', 'head', 'near']
PARTS = ['body', 'bottom', 'top', 'hair', 'weapon']
KEY = [(255, 0, 255), (255, 50, 255), (255, 100, 255), (255, 150, 255), (255, 200, 255)]   # 다시 칠할 색 (명암 0~4)
KEY_FAR = [(255, 0, 255), (255, 0, 255), (255, 50, 255), (255, 100, 255), (255, 150, 255)]  # 먼 쪽: 한 단계 어둡게
LINE = (26, 14, 24)


class LCanvas(Canvas):
    """어느 픽셀을 새로 칠했는지(drawn) 기억하는 캔버스. 층·부위별로 잘라 내는 데 씀"""

    def __init__(self, w, h):
        super().__init__(w, h)
        self.drawn = np.zeros((h, w), bool)
        self.glow = np.zeros((h, w), bool)

    def fill(self, mask, shade, rmp, line=True, dither=True, levels=(0.38, 0.74, 0.97), glow=False, alpha=None):
        if not mask.any():
            return
        super().fill(mask, shade, rmp, line=line, dither=False, levels=(0.40, 0.76, 0.985), glow=glow)   # JRPG: 디더링 없이 깔끔한 명암
        self.drawn |= mask
        self.glow[mask] = glow

    def flat(self, mask, color, glow=False):
        super().flat(mask, color, glow)
        self.drawn |= mask
        self.glow[mask] = glow

    def px(self, x, y, color, glow=False):
        x, y = int(math.floor(x)), int(math.floor(y))
        if 0 <= x < self.w and 0 <= y < self.h:
            self.rgb[y, x] = color[:3]
            self.a[y, x] = True
            self.noline[y, x] = glow
            self.drawn[y, x] = True
            self.glow[y, x] = glow


# ════════════════════════════════════════════════════════════════
# 포즈
# ════════════════════════════════════════════════════════════════
def make_spec(gender, body):
    s = dict(PD.FEMALE if gender == 'f' else PD.MALE)
    s['leg'] += body.get('leg_add', 0.0)
    return s


def _wspec(s, job):
    d = dict(s)
    d['attack'] = PD.JOB_ATTACK[job]
    d['w_idle'] = PD.JOB_W_IDLE[job]
    d['weapon'] = 'sword'      # 대기·걷기 포즈를 직업과 상관없이 같게 (손 위치 공통)
    d['wlen'] = 12.0
    return d


def poses(s, job, anim, view):
    """anim: idle/walk/death/sit(공통) 또는 attack/skill(직업별)"""
    ws = _wspec(s, job)
    if anim == 'sit':
        return _sit(ws, view)
    if anim in ('idle', 'walk', 'death', 'attack'):
        ps = humanoid.poses(ws, anim, view)
        for i, p in enumerate(ps):
            p['anim'] = anim
            p['i'] = i
            p['sway'] = 0.0
            if anim == 'walk':
                p['sway'] = math.sin(i / 6 * math.tau) * 1.4
            if anim == 'attack':
                p['sway'] = -p['lean'] * 0.08
            if anim in ('idle', 'walk'):
                p['wpn'] = PD.JOB_W_IDLE[job] + (p['wpn'] - ws['w_idle'])
                if job in ('mage', 'healer'):
                    p['fx'] = 'orb'
                    p['fxk'] = 0.3 + (0.15 * math.sin(i / 4 * math.tau) if anim == 'idle' else 0)
                else:
                    p['fx'] = None
        return ps
    return _skill(ws, job, view)


# 앉기 (모닥불 휴식): 허리를 낮추고 무릎을 굽혀 발을 앞으로, 손은 무릎 위, 무기는 옆 바닥에 내려놓음. 숨 쉬듯 살짝 오르내리고 가끔 눈을 감음
SIT_HIP = 0.46      # 허리가 내려가는 정도 (다리 길이 대비)


def _sit(s, view):
    L, A = s['leg'], s['arm']
    big = s.get('bob_px', 1.0)
    out = []
    for i in range(4):
        breathe = [0, 0, 1, 1][i] * 0.5 * big
        if view == 'side':
            d = dict(bob=L * SIT_HIP + breathe, lean=5, footN=(L * 0.56, 0.0), footF=(L * 0.44, 0.0),
                     handN=(A * 0.62, A * 0.55), handF=(A * 0.5, A * 0.6))
        else:
            d = dict(bob=L * SIT_HIP + breathe, handN=(A * 0.32, A * 0.62), handF=(A * 0.3, A * 0.62))
        d['eyes'] = 'closed' if i == 3 else 'open'
        d['weapon_drop'] = True
        p = humanoid.P(s, **d)
        p['anim'] = 'sit'
        p['i'] = i
        p['sway'] = 0.0
        p['fx'] = None
        out.append(p)
    return out


def _skill(s, job, view):
    A, st = s['arm'], s['stride']
    side = view == 'side'
    kind = PD.JOB_SKILL[job]
    if kind == 'guard':
        if side:
            seq = [dict(lean=-3, handF=(A * 0.65, -A * 0.05), handN=(-A * 0.15, A * 0.6), wpn=110, eyes='angry', shield_front=True),
                   dict(lean=-4, bob=1, handF=(A * 0.75, -A * 0.1), handN=(-A * 0.2, A * 0.6), wpn=115, fx='aura', fxk=0.4, eyes='angry', shield_front=True)]
        else:
            seq = [dict(handF=(A * 1.3, A * 0.15), handN=(-A * 0.1, A * 0.6), wpn=100, eyes='angry', shield_front=True),
                   dict(bob=1, handF=(A * 1.4, A * 0.05), handN=(-A * 0.1, A * 0.6), wpn=100, fx='aura', fxk=0.4, eyes='angry', shield_front=True)]
        base = seq[1]
        for k in (0.8, 1.0, 0.7):
            seq.append(dict(base, bob=2 if k > 0.75 else 1, fxk=k))
        seq.append(dict(lean=0, eyes='open'))
    elif kind == 'stealth':
        c = dict(bob=3, lean=12 if side else 0, handN=(A * 0.3, A * 0.45), handF=(A * 0.1, A * 0.55), wpn=10,
                 footN=(st * 0.7, 0) if side else (0, 0), footF=(-st * 0.4, 0) if side else (0, 0), eyes='focus')
        seq = [dict(c, fx='smoke', fxk=0.3, bob=2), dict(c, fx='smoke', fxk=0.7, bob=4), dict(c, fx='smoke', fxk=1.0, bob=4),
               dict(c, fx='smoke', fxk=1.25, bob=4), dict(c, fx='smoke', fxk=0.85, bob=3), dict(c, fx='smoke', fxk=0.35, bob=1, eyes='open')]
    elif kind == 'rapid':
        if side:
            draw_ = dict(lean=-4, handN=(A * 0.95, -A * 0.12), handF=(-A * 0.15, -A * 0.12), wpn=-5, string='full', arrow=True, eyes='focus')
            rel = dict(lean=-7, handN=(A * 0.98, -A * 0.16), handF=(-A * 0.4, -A * 0.25), wpn=-7, string='rest', fx='lines', fxk=1.0, eyes='angry')
        else:
            draw_ = dict(handN=(A * 0.3, -A * 0.05), handF=(-A * 0.45, -A * 0.05), wpn=-90, string='full', arrow=True, eyes='focus')
            rel = dict(handN=(A * 0.3, -A * 0.1), handF=(-A * 0.6, -A * 0.2), wpn=-90, string='rest', fx='spark', fxk=1.0, eyes='angry')
        seq = [draw_, rel, draw_, rel, draw_, dict(rel, fxk=0.6)]
    elif kind == 'meteor':
        if side:
            up = dict(lean=-6, handN=(A * 0.1, -A * 0.95), handF=(A * 0.3, -A * 0.85), wpn=-92, eyes='focus')
            seq = [dict(up, fx='circle', fxk=0.3), dict(up, fx='circle', fxk=0.65, bob=1), dict(up, fx='circle', fxk=1.0, bob=1),
                   dict(lean=12, hipx=2, handN=(A * 0.95, -A * 0.35), handF=(A * 0.6, A * 0.1), wpn=-35, footN=(st, 0), fx='meteor', fxk=1.0,
                        eyes='angry'),
                   dict(lean=8, hipx=1, handN=(A * 0.85, -A * 0.2), handF=(A * 0.4, A * 0.4), wpn=-40, fx='meteor', fxk=0.5),
                   dict(lean=2)]
        else:
            up = dict(handN=(-A * 0.05, -A * 0.95), handF=(A * 0.4, -A * 0.8), wpn=-92, eyes='focus')
            seq = [dict(up, fx='circle', fxk=0.3), dict(up, fx='circle', fxk=0.65, bob=1), dict(up, fx='circle', fxk=1.0, bob=1),
                   dict(handN=(A * 0.25, -A * 0.4), wpn=-75, fx='meteor', fxk=1.0, eyes='angry'),
                   dict(handN=(A * 0.15, -A * 0.1), wpn=-82, fx='meteor', fxk=0.5), dict()]
    else:  # pray
        if side:
            pr = dict(handN=(A * 0.35, A * 0.1), handF=(A * 0.45, A * 0.25), wpn=-92, eyes='closed')
        else:
            pr = dict(handN=(A * 0.15, A * 0.15), handF=(A * 1.0, A * 0.35), wpn=-92, eyes='closed')
        seq = [dict(pr, fx='holy', fxk=0.3), dict(pr, fx='holy', fxk=0.6, bob=1), dict(pr, fx='holy', fxk=1.0, bob=1),
               dict(pr, fx='holy', fxk=1.3, bob=1), dict(pr, fx='holy', fxk=0.8), dict(pr, fx='holy', fxk=0.3, eyes='open')]
    out = []
    for i, d in enumerate(seq):
        p = humanoid.P(s, **d)
        p['anim'] = 'skill'
        p['i'] = i
        p['sway'] = -p['lean'] * 0.08
        p.setdefault('shield_front', d.get('shield_front', False))
        out.append(p)
    return out


# ════════════════════════════════════════════════════════════════
# 관절
# ════════════════════════════════════════════════════════════════
def joints(s, p, view):
    leg, tor, A = s['leg'], s['torso'], s['arm']
    hip = (p['hipx'], -leg + p['bob'])
    up = rot((0.0, -1.0), p['lean'])
    neck = add(hip, up, tor)
    hlean = p['lean'] * 0.6 + p['htilt']
    head = add(neck, rot((0.0, -1.0), hlean), s['head_ry'] * 0.86)
    side = view == 'side'
    if side:
        shN = add(neck, up, -1.3)
        shN = (shN[0] + 0.5, shN[1])
        shF = (shN[0] - 1.3, shN[1] + 0.3)
        hipN, hipF = (hip[0] + 0.6, hip[1]), (hip[0] - 0.8, hip[1])
        footN = (p['footN'][0] + p['hipx'] * 0.6, -p['footN'][1])
        footF = (p['footF'][0] + p['hipx'] * 0.3, -p['footF'][1])
        hN = add(shN, p['handN'])
        hF = add(shF, p['handF'])
        sgn = 1
    else:
        sgn = -1 if view == 'front' else 1
        sw = s['sh_w']
        shN = (neck[0] + sgn * sw, neck[1] + 1.3)
        shF = (neck[0] - sgn * sw, neck[1] + 1.3)
        hw = s['hip_w']
        hipN, hipF = (hip[0] + sgn * hw, hip[1]), (hip[0] - sgn * hw, hip[1])
        footN = (hipN[0] + sgn * 0.3, -p['footN'][1])
        footF = (hipF[0] - sgn * 0.3, -p['footF'][1])
        hN = (shN[0] + sgn * 1.0 - sgn * p['handN'][0], shN[1] + p['handN'][1])
        hF = (shF[0] - sgn * 1.0 + sgn * p['handF'][0] * 0.5, shF[1] + p['handF'][1])
    hN = reach(shN, hN, A * 0.98)
    hF = reach(shF, hF, A * 0.98)
    L1 = L2 = leg * 0.52
    if side:
        kneeN = ik(hipN, footN, L1, L2, -1)
        kneeF = ik(hipF, footF, L1, L2, -1)
    elif p.get('anim') == 'sit':
        kneeN = (hipN[0] + (hipN[0] - hip[0]) * 0.55, hipN[1] + leg * 0.16)
        kneeF = (hipF[0] + (hipF[0] - hip[0]) * 0.55, hipF[1] + leg * 0.16)
        footN = (kneeN[0], footN[1])
        footF = (kneeF[0], footF[1])
    else:
        kneeN = ((hipN[0] + footN[0]) / 2 + (hipN[0] - hip[0]) * 0.2, (hipN[1] + footN[1]) / 2)
        kneeF = ((hipF[0] + footF[0]) / 2 + (hipF[0] - hip[0]) * 0.2, (hipF[1] + footF[1]) / 2)
    elbN = ik(shN, hN, A * 0.5, A * 0.5, 1 if side else (1 if view == 'front' else -1))
    elbF = ik(shF, hF, A * 0.5, A * 0.5, 1 if side else (-1 if view == 'front' else 1))
    return dict(hip=hip, neck=neck, head=head, up=up, hlean=hlean, shN=shN, shF=shF, hN=hN, hF=hF, elbN=elbN, elbF=elbF,
                hipN=hipN, hipF=hipF, kneeN=kneeN, kneeF=kneeF, footN=footN, footF=footF, sgn=sgn)


# ════════════════════════════════════════════════════════════════
# 한 프레임 = 층 × 부위 그림
# ════════════════════════════════════════════════════════════════
class Ctx:
    def __init__(self, cv, g, s, J, p, view, job, gender):
        self.cv, self.g, self.s, self.J, self.p, self.view, self.job, self.gender = cv, g, s, J, p, view, job, gender
        self.side = view == 'side'

    def hr(self, v):
        """머리 기준 좌표 → 캐릭터 공간"""
        return add(self.J['head'], rot(v, self.J['hlean']))


def render(look, job, p, view, W, H, ox, oy, want=None):
    """look = dict(gender, body, hair, top, bottom, weapon) (각각 정의 dict). 돌려줌: {(sub, part): (mask, rgb, glow)}"""
    gender = look['gender']
    s = make_spec(gender, look['body'])
    cv = LCanvas(W, H)
    pivot = (0.0, -s['body_r'])
    xf = Xf(ox, oy + p['drop'], rot=p['rot'], pivot=pivot)
    g = Painter(cv, xf)
    J = joints(s, p, view)
    c = Ctx(cv, g, s, J, p, view, job, gender)
    c.ground = Painter(cv, Xf(ox, oy))
    out = {}
    for sub in SUBS:
        for part in PARTS:
            cv.drawn[:] = False
            DRAW[part](c, look[part], sub)
            if want is not None and part != want:
                continue
            if cv.drawn.any():
                m = cv.drawn.copy()
                out[(sub, part)] = (m, cv.rgb.copy(), cv.glow.copy())
    return out


# ─── 몸 ──────────────────────────────────────────────────────────
def draw_body(c, b, sub):
    g, s, J, p, view = c.g, c.s, c.J, c.p, c.view
    skin = ramp(b['skin'])
    skin_f = ramp(darker(hexc(b['skin']), 0.82))
    lr = s['limb']

    def arm(k, rmp):
        g.cap(J['sh' + k], J['elb' + k], lr * 1.0, rmp, r1=lr * 0.9)
        g.cap(J['elb' + k], J['h' + k], lr * 0.9, rmp, r1=lr * 0.8)
        g.ell(J['h' + k], s['hand_r'], s['hand_r'], rmp)

    def leg(k, rmp):
        g.cap(J['hip' + k], J['knee' + k], lr * 1.15, rmp, r1=lr * 0.95)
        g.cap(J['knee' + k], J['foot' + k], lr * 0.95, rmp, r1=lr * 0.75)
        f = J['foot' + k]
        g.ell((f[0] + s['foot_len'] * 0.3, f[1] - lr * 0.6), s['foot_len'] * 0.7, lr * 0.75, rmp)

    if c.side:
        if sub == 'far':
            arm('F', skin_f)
            leg('F', skin_f)
        elif sub == 'mid':
            _torso_skin(c, skin, b)
            leg('N', skin)
        elif sub == 'head':
            _head(c, b, skin)
        elif sub == 'near':
            arm('N', skin)
    else:
        arm_sub = 'near' if view == 'front' else 'mid'   # 뒷모습: 팔이 늘어진 머리 아래
        if sub == 'mid':
            leg('F', skin)
            leg('N', skin)
            _torso_skin(c, skin, b)
        elif sub == 'head':
            _head(c, b, skin)
        if sub == arm_sub:
            arm('F', skin)
            arm('N', skin)


def _torso_skin(c, skin, b):
    g, s, J = c.g, c.s, c.J
    W = s['torso_w'] if not c.side else s['torso_d']
    hip, neck, up = J['hip'], J['neck'], J['up']
    g.cap(add(hip, up, s['limb'] * 0.5), add(neck, up, -s['limb'] * 0.6), W * 0.85, skin, r1=W * 0.95)
    # 속옷 (상의가 덮지만 빈틈이 보일 때를 위해)
    under = ramp('#e8e0d6')
    g.ell(add(hip, up, 1.0), W * 0.9, 1.6, under, ang=c.p['lean'])


def _head(c, b, skin):
    g, s, J, p, view = c.g, c.s, c.J, c.p, c.view
    rx, ry = s['head_rx'], s['head_ry']
    hr = c.hr
    lean = J['hlean']
    # 목
    g.cap(add(J['neck'], J['up'], -0.5), add(J['neck'], J['up'], 2.2), s['limb'] * 1.0, skin)
    if b.get('elf') and view != 'front':
        _elf_ear(c, skin, behind=True)
    if c.side:
        g.ell(hr((0.3, -0.3)), rx * 0.92, ry * 0.95, skin, ang=lean)
        g.poly([hr((-rx * 0.2, ry * 0.3)), hr((rx * 0.95, ry * 0.15)), hr((rx * 0.55, ry * 0.85)), hr((0, ry * 0.95))], skin, shade=0.3, line=False)
    else:
        g.ell(hr((0, -0.4)), rx, ry * 0.92, skin, ang=lean)
        # 애니 턱선 (V자)
        g.poly([hr((-rx * 0.92, ry * 0.05)), hr((rx * 0.92, ry * 0.05)), hr((rx * 0.5, ry * 0.75)), hr((0, ry * 0.98)), hr((-rx * 0.5, ry * 0.75))],
               skin, shade=0.3, line=False, grad=((-0.6, -0.3), 0.4))
    if view == 'front':
        for sg in (-1, 1):
            g.ell(hr((sg * rx * 0.97, ry * 0.12)), 1.1, 1.6, skin)
        if b.get('elf'):
            _elf_ear(c, skin, behind=False)
        _face_front(c, b, skin)
    elif c.side:
        g.ell(hr((-rx * 0.15, ry * 0.12)), 1.3, 1.8, skin)
        if b.get('elf'):
            _elf_ear(c, skin, behind=False)
        _face_side(c, b, skin)


def _elf_ear(c, skin, behind):
    g, s, view = c.g, c.s, c.view
    rx, ry = s['head_rx'], s['head_ry']
    hr = c.hr
    if view == 'side':
        if behind:
            return
        g.poly([hr((-rx * 0.1, -ry * 0.05)), hr((-rx * 1.25, -ry * 0.75)), hr((-rx * 0.2, ry * 0.35))], skin, shade=0.3)
    else:
        for sg in (-1, 1):
            g.poly([hr((sg * rx * 0.85, -ry * 0.05)), hr((sg * rx * 1.75, -ry * 0.6)), hr((sg * rx * 0.85, ry * 0.35))], skin, shade=0.25)


def _eye(c, at, b, w, h, outer, mood):
    g = c.g
    ir = ramp(b['eye'])
    x, y = at
    if mood == 'x':
        for i in range(3):
            g.px((x + i, y + i + 0.5), LINE)
            g.px((x + 2 - i, y + i + 0.5), LINE)
        return
    if mood in ('hurt', 'closed'):
        # 감은 눈: 아래로 휜 선 (기도) / > < (아픔)
        if mood == 'closed':
            for i in range(w + 1):
                g.px((x + i - (1 if outer < 0 else 0), y + h * 0.55 + (0 if 0 < i < w else -1)), LINE)
        else:
            g.px((x, y + 1), LINE)
            for i in range(1, w):
                g.px((x + i, y + 2), LINE)
            g.px((x + w - 1, y + 3), LINE)
        return
    female = c.s['female']
    # 속눈썹 (위 선, 여성은 눈꼬리 쪽으로 한 칸 더)
    for i in range(-1 if (outer < 0 and female) else 0, w + (1 if (outer > 0 and female) else 0)):
        g.px((x + i, y), LINE)
    if female:
        g.px((x - 1 if outer < 0 else x + w, y - 1), LINE)
    for j in range(1, h):
        t = (j - 1) / max(1, h - 2)
        col = ir[3] if t < 0.3 else (ir[2] if t < 0.7 else ir[1])
        for i in range(w):
            g.px((x + i, y + j), col)
    # 동공 + 반짝임
    pc = x + w // 2
    for j in range(2, h - 1):
        g.px((pc, y + j), mixc(ir[0], LINE, 0.4))
    gx = x if outer < 0 else x + w - 1
    g.px((gx, y + 1), (255, 255, 250))
    if h >= 4:
        g.px((x + w - 1 if outer < 0 else x, y + h - 2), ir[4])
    if mood == 'angry':
        g.line((x - 1 if outer < 0 else x + w, y - 2), (x + w if outer < 0 else x - 1, y - 1), LINE)
    elif mood == 'focus':
        for i in range(w):
            g.px((x + i, y + 1), LINE)


def _face_front(c, b, skin):
    g, s, p = c.g, c.s, c.p
    rx, ry = s['head_rx'], s['head_ry']
    hr = c.hr
    w = 2
    h = b.get('eye_h', 4)
    ey = ry * 0.30
    for sg in (-1, 1):
        ex = sg * rx * 0.40
        at = hr((ex - w * 0.5 + (0.0 if sg < 0 else 0.0), ey - h * 0.5))
        _eye(c, at, b, w, h, sg, p['eyes'])
    # 눈썹 (남성은 굵게)
    if not s['female'] and p['eyes'] not in ('x',):
        for sg in (-1, 1):
            g.line(hr((sg * rx * 0.25, ey - h * 0.5 - 1.6)), hr((sg * rx * 0.62, ey - h * 0.5 - 1.2 - (0.8 if p['eyes'] != 'angry' else 0))),
                   skin[0])
    if s['female'] or b.get('blush', False):
        for sg in (-1, 1):
            g.px(hr((sg * rx * 0.55, ey + h * 0.5 + 1.0)), mixc(skin[2], (240, 110, 120), 0.5))
            g.px(hr((sg * rx * 0.55 + sg, ey + h * 0.5 + 1.0)), mixc(skin[2], (240, 110, 120), 0.35))
    if b.get('freckles'):
        for sx, sy in ((-0.5, 0.62), (-0.35, 0.7), (0.4, 0.66), (0.55, 0.6)):
            g.px(hr((rx * sx, ry * sy)), skin[1])
    if b.get('scar'):
        g.line(hr((rx * 0.45, -ry * 0.05)), hr((rx * 0.62, ry * 0.4)), mixc(skin[1], (160, 60, 60), 0.4))
    g.px(hr((0, ey + h * 0.5 + 0.8)), skin[1])   # 코
    my = ry * 0.74
    if p['eyes'] in ('angry', 'hurt'):
        g.line(hr((-1, my)), hr((1, my)), (90, 30, 40))
    elif p['eyes'] == 'x':
        g.ell(hr((0, my + 0.5)), 1.2, 1.0, ramp('#7a2030'), line=False)
    else:
        g.px(hr((-0.5, my)), mixc(skin[0], (120, 40, 50), 0.4))
        g.px(hr((0.5, my)), mixc(skin[0], (120, 40, 50), 0.4))


def _face_side(c, b, skin):
    g, s, p = c.g, c.s, c.p
    rx, ry = s['head_rx'], s['head_ry']
    hr = c.hr
    h = b.get('eye_h', 4)
    w = 2
    ey = ry * 0.30
    _eye(c, hr((rx * 0.45 - 1, ey - h * 0.5)), b, w, h, -1, p['eyes'])
    if not s['female'] and p['eyes'] != 'x':
        g.line(hr((rx * 0.25, ey - h * 0.5 - 1.5)), hr((rx * 0.7, ey - h * 0.5 - 1.3)), skin[0])
    if s['female']:
        g.px(hr((rx * 0.5, ey + h * 0.5 + 1.0)), mixc(skin[2], (240, 110, 120), 0.5))
    if b.get('freckles'):
        g.px(hr((rx * 0.6, ry * 0.62)), skin[1])
    g.px(hr((rx * 1.0, ey + h * 0.5 + 0.3)), skin[3])    # 코끝
    g.px(hr((rx * 0.98, ey + h * 0.5 + 1.2)), skin[1])
    g.px(hr((rx * 0.78, ry * 0.74)), (110, 40, 50) if p['eyes'] in ('angry', 'hurt') else skin[1])


# ─── 하의 ────────────────────────────────────────────────────────
def draw_bottom(c, d, sub):
    g, s, J, p, view = c.g, c.s, c.J, c.p, c.view
    lr = s['limb']
    boots = ramp(d['boots'])
    boots_f = ramp(darker(hexc(d['boots']), 0.8))
    socks = ramp(d['socks']) if d.get('socks') else None
    kind = d['kind']

    def legwear(k, far):
        key = KEY_FAR if far else KEY
        hip, knee, foot = J['hip' + k], J['knee' + k], J['foot' + k]
        bh = d.get('boot_h', 0.4)
        if kind in ('pants', 'baggy', 'hakama'):
            r = lr * (1.45 if kind == 'baggy' else 1.25)
            g.cap(hip, knee, r * 1.05, key, r1=r)
            g.cap(knee, lerp(knee, foot, 0.85), r * (1.15 if kind == 'baggy' else 1.0), key, r1=r * (1.2 if kind == 'baggy' else 0.95))
        elif kind == 'shorts':
            ln = d.get('length', 0.45)
            g.cap(hip, lerp(hip, knee, min(1.0, ln * 1.6)), lr * 1.3, key, r1=lr * 1.2)
            if ln > 0.6:
                g.cap(knee, lerp(knee, foot, (ln - 0.6) * 2.0), lr * 1.15, key)
        if socks is not None:
            sk = socks if not far else ramp(darker(hexc(d['socks']), 0.8))
            top_t = 0.15 if kind == 'shorts' else 0.0
            g.cap(lerp(knee, hip, 0.5 if kind == 'shorts' else -0.1), lerp(knee, foot, 0.9), lr * 1.02, sk, r1=lr * 0.82)
        if d.get('greaves'):
            gr = ramp(d['greaves'])
            g.cap(lerp(knee, foot, 0.05), lerp(knee, foot, 0.75), lr * 1.4, gr if not far else ramp(darker(hexc(d['greaves']), 0.8)))
            g.ell(knee, lr * 1.2, lr * 1.1, gr)
        # 신발·부츠
        b = boots_f if far else boots
        g.cap(lerp(foot, knee, bh), foot, lr * 1.12, b, r1=lr * 0.95)
        g.ell((foot[0] + s['foot_len'] * (0.35 if c.side else 0.0), foot[1] - lr * 0.65), s['foot_len'] * (0.8 if c.side else 0.6), lr * 0.85, b)

    def skirt():
        hip = J['hip']
        W = (s['torso_w'] if not c.side else s['torso_d']) * 1.15
        ln = d.get('length', 0.4) * s['leg']
        sway = p.get('sway', 0.0)
        spread = (J['footN'][0] - J['footF'][0]) * 0.2 if c.side else 0
        top_l = add(hip, rot((-W, -1.5), p['lean']))
        top_r = add(hip, rot((W, -1.5), p['lean']))
        hem_y = hip[1] + ln
        fl = 1.45 if kind == 'skirt' else 1.6
        bl = (hip[0] - W * fl + sway - abs(spread), hem_y)
        br = (hip[0] + W * fl + sway + abs(spread), hem_y)
        pts = [top_l, top_r, br]
        n = 5
        for i in range(1, n):
            t = i / n
            pts.append((br[0] + (bl[0] - br[0]) * t, hem_y + (0.8 if i % 2 else 0)))
        pts.append(bl)
        g.poly(pts, KEY, shade=0.2, grad=((-0.6, -0.5), 0.6))
        if d.get('pleats') or kind == 'hakama':
            for t in (0.25, 0.5, 0.75):
                a = lerp(top_l, top_r, t)
                bb = lerp(bl, br, t)
                g.line(lerp(a, bb, 0.3), bb, KEY[1])
        # 허리 밴드
        g.cap(top_l, top_r, 0.9, KEY)

    if kind in ('skirt',) or kind == 'hakama':
        if c.side:
            if sub == 'far':
                legwear('F', True)
            elif sub == 'mid':
                legwear('N', False)
                skirt()
        elif sub == 'mid':
            legwear('F', False)
            legwear('N', False)
            skirt()
        return
    if c.side:
        if sub == 'far':
            legwear('F', True)
        elif sub == 'mid':
            legwear('N', False)
            _waist(c)
    elif sub == 'mid':
        legwear('F', False)
        legwear('N', False)
        _waist(c)


def _waist(c):
    g, s, J, p = c.g, c.s, c.J, c.p
    W = (s['torso_w'] if not c.side else s['torso_d']) * 0.98
    hip, up = J['hip'], J['up']
    g.cap(add(hip, up, -0.2), add(hip, up, 1.8), W, KEY)


# ─── 상의 ────────────────────────────────────────────────────────
def draw_top(c, d, sub):
    g, s, J, p, view = c.g, c.s, c.J, c.p, c.view
    lr = s['limb']
    sl = d['sleeve']

    def sleeve(k, far):
        key = KEY_FAR if far else KEY
        sh, elb, hand = J['sh' + k], J['elb' + k], J['h' + k]
        if sl == 'none':
            return
        if sl in ('short', 'puff'):
            g.cap(sh, lerp(sh, elb, 0.75), lr * 1.3, key, r1=lr * 1.15)
            if sl == 'puff':
                g.ell(lerp(sh, elb, 0.3), lr * 1.9, lr * 1.6, key)
        else:
            g.cap(sh, elb, lr * 1.25, key, r1=lr * 1.1)
            if sl == 'long':
                g.cap(elb, lerp(elb, hand, 0.8), lr * 1.1, key, r1=lr * 1.0)
            else:  # wide: 손목 쪽이 넓게 퍼짐
                a = lerp(elb, hand, 0.85)
                dv = (a[0] - elb[0], a[1] - elb[1])
                ln = max(0.01, math.hypot(*dv))
                n = (-dv[1] / ln, dv[0] / ln)
                g.poly([add(elb, n, lr * 1.1), add(a, n, lr * 2.3), add(a, n, -lr * 2.3), add(elb, n, -lr * 1.1)], key, shade=0.2)
            if d.get('trim'):
                end = lerp(elb, hand, 0.8)
                g.ell(end, lr * (2.0 if sl == 'wide' else 1.15), lr * 0.7, ramp(d['trim']), ang=math.degrees(math.atan2(hand[1] - elb[1], hand[0] - elb[0])) + 90)
        if d.get('armor') == 'full':
            g.ell(add(sh, (0, -0.4)), lr * 2.0, lr * 1.5, ramp(d['metal']) if not far else ramp(darker(hexc(d['metal']), 0.8)))

    def torso():
        hip, neck, up = J['hip'], J['neck'], J['up']
        W = (s['torso_w'] if not c.side else s['torso_d']) * 1.08
        hem = d['hem']
        bottom = add(hip, up, {'waist': s['torso'] * 0.3, 'hip': -0.5, 'thigh': -0.5, 'knee': -0.5}[hem])
        g.cap(bottom, add(neck, up, -lr * 0.5), W * 0.95, KEY, r1=W * (1.08 if not s['female'] else 1.0))
        if s['female'] and view != 'back':
            # 가슴 볼륨 (살짝)
            cc = add(lerp(hip, neck, 0.68), (W * 0.35 if c.side else 0, 0))
            g.ell(cc, W * (0.75 if not c.side else 0.6), s['torso'] * 0.2, KEY, ang=p['lean'], line=False)
        if hem in ('thigh', 'knee'):
            ln = s['leg'] * (0.42 if hem == 'thigh' else 0.78)
            sway = p.get('sway', 0.0)
            spread = abs(J['footN'][0] - J['footF'][0]) * 0.25 if c.side else 0
            tl = add(hip, rot((-W * 0.95, -1.5), p['lean']))
            tr = add(hip, rot((W * 0.95, -1.5), p['lean']))
            hem_y = hip[1] + ln
            bl = (hip[0] - W * 1.35 + sway - spread, hem_y)
            br = (hip[0] + W * 1.35 + sway + spread, hem_y)
            pts = [tl, tr, br, ((bl[0] + br[0]) / 2, hem_y + 0.6), bl]
            if d.get('buttons') and view == 'front':
                pts = [tl, tr, br, ((bl[0] + br[0]) / 2 + 0.5, hem_y), ((bl[0] + br[0]) / 2 + 0.5, hip[1] + 1.0),
                       ((bl[0] + br[0]) / 2 - 0.5, hip[1] + 1.0), ((bl[0] + br[0]) / 2 - 0.5, hem_y), bl]
            g.poly(pts, KEY, shade=0.2, grad=((-0.6, -0.5), 0.6))
            g.line(lerp(tl, bl, 0.4), lerp(tl, bl, 0.95), KEY[1])
            if d.get('trim'):
                g.line((bl[0] + 0.5, hem_y - 0.3), (br[0] - 0.5, hem_y - 0.3), ramp(d['trim'])[3])
        # 고정색 장식
        if d.get('inner') and view == 'front':
            inr = ramp(d['inner'])
            top_c = add(neck, up, -1.0)
            g.poly([add(top_c, (-1.6, 0)), add(top_c, (1.6, 0)), add(bottom, (1.0, 0)), add(bottom, (-1.0, 0))], inr, shade=0.3)
        if d.get('vest'):
            vs = ramp(d['vest'])
            if view == 'front':
                for sg in (-1, 1):
                    g.poly([add(neck, (sg * 1.2, 1.5)), add(neck, (sg * W * 1.05, 2.0)), add(bottom, (sg * W * 1.0, 0.5)), add(bottom, (sg * 1.0, 0.5))],
                           vs, shade=0.2, grad=((-0.6, -0.5), 0.5))
            else:
                g.cap(add(bottom, up, 0.5), add(neck, up, -1.6), W * 0.98, vs)
        if d.get('armor'):
            mt = ramp(d['metal'])
            cc = lerp(hip, neck, 0.62)
            g.ell(cc, W * 1.05, s['torso'] * 0.36, mt, ang=p['lean'])
            if view == 'front':
                g.line(add(cc, (0, -s['torso'] * 0.28)), add(cc, (0, s['torso'] * 0.25)), mt[1])
        if d.get('belt'):
            bl_ = ramp(d['belt'])
            b0 = add(hip, up, s['torso'] * 0.16 if d['hem'] != 'waist' else s['torso'] * 0.32)
            g.cap(add(b0, rot((-W, 0), p['lean'])), add(b0, rot((W, 0), p['lean'])), 0.95, bl_)
            if view != 'back':
                g.px(add(b0, (W * 0.3 if c.side else 0, 0)), ramp('#e0b040')[3])
        if d.get('buttons') and view == 'front':
            for t in (0.3, 0.55, 0.8):
                g.px(lerp(bottom, neck, t), ramp(d['buttons'])[3])
        if d.get('trim') and d['hem'] in ('hip', 'waist'):
            g.line(add(bottom, rot((-W * 0.9, 0.3), p['lean'])), add(bottom, rot((W * 0.9, 0.3), p['lean'])), ramp(d['trim'])[3])
        # 깃
        col = d.get('collar')
        if col == 'high':
            g.cap(add(neck, up, -0.6), add(neck, up, 1.4), s['limb'] * 1.35, KEY)
        elif col == 'round' and view == 'front':
            g.line(add(neck, (-1.6, 0.6)), add(neck, (1.6, 0.6)), KEY[1])
        if d.get('trim') and col == 'high':
            g.line(add(neck, (-1.3, 1.0)), add(neck, (1.3, 1.0)), ramp(d['trim'])[3])

    if c.side:
        if sub == 'far':
            sleeve('F', True)
        elif sub == 'mid':
            torso()
        elif sub == 'near':
            sleeve('N', False)
    else:
        if sub == 'mid':
            torso()
        if sub == ('near' if view == 'front' else 'mid'):
            sleeve('F', False)
            sleeve('N', False)


# ─── 헤어 ────────────────────────────────────────────────────────
LEN = {0: 0.0, 'nape': 0.75, 'cheek': 0.55, 'chin': 1.0, 'shoulder': 1.5, 'chest': 2.1, 'waist': 3.0}


def _bez(a, ctl, b, n=6):
    return [((1 - t) ** 2 * a[0] + 2 * (1 - t) * t * ctl[0] + t * t * b[0], (1 - t) ** 2 * a[1] + 2 * (1 - t) * t * ctl[1] + t * t * b[1])
            for t in (i / n for i in range(n + 1))]


def _clump(g, ra, rb, tip, bend=0.0, rmp=None, shade=0.25, line=True):
    """머리 다발 하나: 뿌리 두 점(ra, rb)에서 끝(tip)으로 모이는 뾰족한 잎 모양. bend로 휘어짐"""
    rmp = rmp or KEY
    mx, my = (ra[0] + rb[0]) / 2, (ra[1] + rb[1]) / 2
    dx, dy = tip[0] - mx, tip[1] - my
    ln = max(0.01, math.hypot(dx, dy))
    nx, ny = -dy / ln, dx / ln
    c1 = (ra[0] + dx * 0.55 + nx * bend, ra[1] + dy * 0.55 + ny * bend)
    c2 = (rb[0] + dx * 0.55 + nx * bend, rb[1] + dy * 0.55 + ny * bend)
    pts = _bez(ra, c1, tip) + _bez(tip, c2, rb)[1:]
    g.poly(pts, rmp, shade=shade, grad=((-0.4, -1), 0.7), line=line)


def draw_hair(c, d, sub):
    g, s, J, p, view = c.g, c.s, c.J, c.p, c.view
    rx, ry = s['head_rx'], s['head_ry']
    hr = c.hr
    vol = d.get('vol', 1.0)
    R, RY = rx * 1.08 * vol, ry * 1.06 * vol
    sway = p.get('sway', 0.0)
    back_len = LEN[d['back']] * ry
    side_len = LEN[d['sides']] * ry
    extra = d.get('extra')
    wavy = d.get('wavy')
    bangs = d['bangs']
    brow = ry * 0.25 - 2.6          # 앞머리 끝 (눈 바로 위)

    def shine(y, w):
        """천사의 고리: 머리 위쪽 반짝이는 띠"""
        for i in range(-int(w), int(w) + 1):
            yy = y + (abs(i) / max(w, 1)) ** 2 * 1.5
            g.px(hr((i, yy)), KEY[4] if abs(i) < w * 0.55 else KEY[3])

    def cap(full=False):
        """정수리 덮개 (얼굴 쪽은 이마 위에서 끊김)"""
        pts = []
        for i in range(15):
            a = math.radians(180 + i * (180 / 14))
            pts.append(hr((math.cos(a) * R, math.sin(a) * RY - 0.5)))
        if full:
            pts += [hr((R * 0.98, ry * 0.2)), hr((R * 0.7, ry * 0.75)), hr((-R * 0.7, ry * 0.75)), hr((-R * 0.98, ry * 0.2))]
        else:
            pts += [hr((R * 1.0, ry * 0.25)), hr((rx * 0.72, -ry * 0.2)), hr((-rx * 0.72, -ry * 0.2)), hr((-R * 1.0, ry * 0.25))]
        g.poly(pts, KEY, shade=0.3, grad=((-0.5, -1), 0.8))

    def hang(xl, xr, top_y, length, n=5, curl=0.0):
        """늘어진 머리: 다발 n개를 나란히"""
        for i in range(n):
            t0, t1 = i / n, (i + 1) / n
            a = hr((xl + (xr - xl) * t0 - 0.3, top_y))
            b = hr((xl + (xr - xl) * t1 + 0.3, top_y))
            mid = xl + (xr - xl) * (t0 + t1) / 2
            ln = length * (0.9 + 0.06 * ((i * 7) % 3))
            tip = hr((mid + (mid / max(abs(xl), abs(xr), 1)) * 1.2 + sway * 0.7, top_y + ln))
            _clump(g, a, b, tip, bend=(curl + (1.2 if wavy else 0)) * (1 if i % 2 else -1), shade=0.15)

    def tail(root, length, ang, width=2.8, braid=False):
        """묶은 머리: 끈 + 통통한 다발 (트윈테일·포니테일), 땋은 머리는 마디 세 개"""
        a = ang + sway * 5
        tip = add(root, dirv(a, length))
        if braid:
            q = root
            for k in range(3):
                q2 = add(q, dirv(a + (8 if k % 2 else -8), length / 3))
                _clump(g, add(q, dirv(a + 90, width * 0.9)), add(q, dirv(a - 90, width * 0.9)), add(q2, dirv(a, 0.8)), shade=0.2)
                q = q2
        else:
            nv = dirv(a + 90, width)
            _clump(g, add(root, nv), add(root, nv, -1), tip, bend=width * 0.9, shade=0.2)
            _clump(g, add(root, nv, 0.4), add(root, nv, -0.6), add(add(root, dirv(a, length * 0.85)), dirv(a + 90, -1.2)), bend=-width * 0.5, shade=0.3)
        g.ell(root, width * 0.6, width * 0.55, ramp('#d83a48'))
        return tip

    def bang_set(front):
        """앞머리 다발 (front: 정면, 아니면 옆모습의 이마 쪽)"""
        if bangs == 'none':
            return
        if front:
            xs = [-R * 0.95, -rx * 0.55, -rx * 0.15, rx * 0.25, rx * 0.62, R * 0.95]
            top = -ry * 0.62
            for i in range(len(xs) - 1):
                xa, xb = xs[i], xs[i + 1]
                mid = (xa + xb) / 2
                L = brow - top
                if bangs == 'blunt':
                    tipx, L = mid, L + 0.4
                elif bangs == 'side':
                    tipx, L = mid - 1.6, L * (1.15 if i < 2 else 0.85)
                elif bangs == 'swept':
                    tipx, L = mid + 2.0, L * (0.7 + i * 0.12)
                elif bangs == 'parted':
                    tipx = mid + (1.5 if mid > 0 else -1.5)
                    L = L * (0.6 if abs(mid) < rx * 0.3 else 1.05)
                elif bangs == 'spiky':
                    tipx, L = mid + (0.8 if i % 2 else -0.8), L * (0.95 if i % 2 else 0.7)
                else:  # messy
                    tipx, L = mid + (1.0 if i % 2 else -0.6), L * (0.75 if i % 2 else 1.0)
                if i in (0, len(xs) - 2):
                    L += ry * 0.25    # 바깥 다발은 볼 옆으로 조금 더 길게
                _clump(g, hr((xa - 0.4, top)), hr((xb + 0.4, top)), hr((tipx, top + L)), bend=0.6 * (1 if i % 2 else -1), shade=0.3)
        else:
            top = -ry * 0.75
            for i, (xa, xb, l) in enumerate(((rx * 0.0, rx * 0.5, 0.9), (rx * 0.35, rx * 0.85, 1.0), (rx * 0.65, R * 1.02, 0.8))):
                L = (brow - top) * l * (0.75 if bangs in ('spiky', 'messy') and i == 1 else 1.0)
                tip = hr(((xa + xb) / 2 + (1.2 if bangs != 'swept' else 2.0), top + L + 0.5))
                _clump(g, hr((xa, top)), hr((xb, top)), tip, bend=0.5, shade=0.3)

    def spikes(angles):
        for a, ln in angles:
            base_c = (math.cos(math.radians(a)) * R * 0.8, math.sin(math.radians(a)) * RY * 0.8)
            tip = hr((math.cos(math.radians(a)) * (R + ln), math.sin(math.radians(a)) * (RY + ln) - 0.5))
            sv = dirv(a + 90, 2.4)
            _clump(g, hr(add(base_c, sv)), hr(add(base_c, sv, -1)), tip, bend=0.8, shade=0.3)

    def curls(a0, a1, step):
        for a in range(a0, a1, step):
            g.ell(hr((math.cos(math.radians(a)) * R * 0.92, math.sin(math.radians(a)) * RY * 0.92)), 2.4, 2.2, KEY)

    # ═══ 정면 ═══
    if view == 'front':
        if sub == 'back':
            if back_len > ry * 0.8:
                hang(-R * 1.0, R * 1.0, -ry * 0.1, back_len + ry * 0.5, n=6)
            if extra == 'twin':
                for sg in (-1, 1):
                    tail(hr((sg * R * 0.92, -ry * 0.5)), ry * 2.3, 90 - sg * 22)
            elif extra in ('pony_high', 'pony_low'):
                tail(hr((R * 0.3, -ry * (0.85 if extra == 'pony_high' else 0.1))), ry * 1.9, 70, width=2.6)
            elif extra == 'pony_side':
                tail(hr((R * 0.9, -ry * 0.55)), ry * 2.2, 72)
        elif sub == 'head':
            if extra == 'buns':
                for sg in (-1, 1):
                    g.ell(hr((sg * rx * 0.72, -ry * 0.92)), 3.0, 2.8, KEY)
            if extra == 'topknot':
                g.ell(hr((0, -ry * 1.22)), 2.6, 2.4, KEY)
            if extra == 'spikes':
                spikes(((-170, 3.5), (-140, 4.5), (-110, 4.8), (-80, 5.0), (-50, 4.6), (-20, 3.8)))
            if extra == 'curls':
                curls(180, 361, 30)
            cap()
            if extra == 'topknot':
                g.ell(hr((0, -ry * 0.98)), 1.8, 0.8, ramp('#d83a48'))
            if side_len > 0:
                for sg in (-1, 1):
                    tip = hr((sg * (R * 0.92 if d.get('straight_sides') else rx * 0.85) + sway * 0.3, ry * 0.2 + side_len))
                    _clump(g, hr((sg * R * 1.0, -ry * 0.25)), hr((sg * rx * 0.68, -ry * 0.1)), tip, bend=-sg * 0.6, shade=0.2)
            bang_set(True)
            if extra == 'slick':
                for t in (-0.45, -0.1, 0.25, 0.6):
                    g.line(hr((rx * t, -ry * 0.3)), hr((rx * t * 0.8, -ry * 0.95)), KEY[1])
            shine(-ry * 0.68, rx * 0.5)
            if extra == 'braid':
                tail(hr((R * 0.8, ry * 0.25)), ry * 1.9, 95, width=2.1, braid=True)
        return

    # ═══ 뒷모습 ═══
    if view == 'back':
        if sub != 'head':
            return
        if extra == 'buns':
            for sg in (-1, 1):
                g.ell(hr((sg * rx * 0.72, -ry * 0.92)), 3.0, 2.8, KEY)
        if extra == 'topknot':
            g.ell(hr((0, -ry * 1.22)), 2.6, 2.4, KEY)
        if extra == 'spikes':
            spikes(((-170, 3.5), (-140, 4.5), (-110, 4.8), (-80, 5.0), (-50, 4.6), (-20, 3.8)))
        if extra == 'curls':
            curls(180, 540, 36)
        cap(full=True)
        if back_len > 0 or not d.get('short'):
            hang(-R * 0.95, R * 0.95, ry * 0.1, max(back_len, ry * 0.45), n=5)
        shine(-ry * 0.62, rx * 0.55)
        if extra == 'twin':
            for sg in (-1, 1):
                tail(hr((sg * R * 0.9, -ry * 0.5)), ry * 2.3, 90 + sg * 22)
        elif extra in ('pony_high', 'pony_low'):
            tail(hr((0, -ry * (0.85 if extra == 'pony_high' else 0.15))), ry * 1.9, 95, width=2.8)
        elif extra == 'pony_side':
            tail(hr((-R * 0.9, -ry * 0.55)), ry * 2.2, 108)
        elif extra == 'braid':
            tail(hr((-R * 0.8, ry * 0.25)), ry * 1.9, 85, width=2.1, braid=True)
        return

    # ═══ 옆모습 (오른쪽을 봄) ═══
    if sub == 'back':
        if back_len > ry * 0.8:
            hang(-R * 1.05, -rx * 0.05, -ry * 0.1, back_len + ry * 0.4, n=3, curl=0.4)
        if extra == 'twin':
            tail(hr((-R * 0.35, -ry * 0.7)), ry * 2.3, 112)
        elif extra in ('pony_high', 'pony_low'):
            tail(hr((-R * 0.85, -ry * (0.65 if extra == 'pony_high' else 0.05))), ry * 1.9, 118, width=2.8)
        elif extra == 'pony_side':
            tail(hr((-R * 0.1, -ry * 0.8)), ry * 2.2, 100)
    elif sub == 'head':
        if extra == 'buns':
            g.ell(hr((-rx * 0.3, -ry * 0.98)), 3.0, 2.8, KEY)
        if extra == 'topknot':
            g.ell(hr((-rx * 0.15, -ry * 1.22)), 2.6, 2.4, KEY)
        if extra == 'spikes':
            spikes(((-175, 4.6), (-150, 5.0), (-122, 4.8), (-95, 4.2), (-65, 3.4)))
        if extra == 'curls':
            curls(150, 360, 30)
        # 뒤통수 쪽으로 넓은 덮개 (얼굴 쪽은 이마 위까지만)
        nape = ry * (0.25 if d.get('short') else 0.55) + min(back_len, ry * 0.3)
        pts = [hr((-R * 0.95, nape)), hr((-R * 1.1, ry * 0.1))]
        for i in range(10):
            a = math.radians(185 + i * 17)
            pts.append(hr((math.cos(a) * R * 0.98 - 0.3, math.sin(a) * RY - 0.5)))
        pts += [hr((rx * 0.95, -ry * 0.35)), hr((rx * 0.25, -ry * 0.45)), hr((-rx * 0.15, -ry * 0.05)), hr((-rx * 0.25, ry * 0.35)), hr((-rx * 0.5, nape))]
        g.poly(pts, KEY, shade=0.3, grad=((-0.5, -1), 0.8))
        if side_len > ry * 0.5:
            _clump(g, hr((-rx * 0.1, -ry * 0.2)), hr((-rx * 0.5, -ry * 0.15)), hr((-rx * 0.15 + sway * 0.3, ry * 0.15 + side_len)), bend=0.6, shade=0.2)
        bang_set(False)
        if extra == 'slick':
            for t in (-0.6, -0.2, 0.2):
                g.line(hr((rx * t, -ry * 0.85)), hr((rx * t - 2.0, -ry * 0.35)), KEY[1])
        shine(-ry * 0.68, rx * 0.4)
        if extra == 'braid':
            tail(hr((-rx * 0.05, ry * 0.45)), ry * 1.8, 95, width=2.1, braid=True)


# ─── 무기·효과 ───────────────────────────────────────────────────
def draw_weapon(c, d, sub):
    g, s, J, p, view, job = c.g, c.s, c.J, c.p, c.view, c.job
    side = c.side
    # 무기가 그려지는 층: 옆·앞은 가까운 손(near), 뒤에서는 몸 뒤(back)
    wsub = 'near' if view != 'back' else 'back'
    if view == 'front' and not p.get('wfront', True):
        wsub = 'back'
    ang = p['wpn']
    if view == 'front' and d['kind'] != 'bow':
        ang = 180 - ang
    if d['kind'] == 'bow' and view != 'side' and p.get('anim') in ('idle', 'walk'):
        ang = 180 if J['sgn'] < 0 else 0     # 앞·뒤 대기: 활을 몸 바깥쪽에 세워 듦
    if view == 'side' and p.get('anim') in ('idle', 'walk'):
        if d['kind'] == 'staff':
            ang = -64 + (p['wpn'] - PD.JOB_W_IDLE[c.job]) * 0.5     # 옆: 앞으로 기울여 얼굴 앞을 지나지 않게
        elif d['kind'] == 'bow':
            ang = 22                                                # 옆: 활을 아래로 내려 듦
    if d['kind'] == 'staff' and view != 'side' and p.get('anim') in ('idle', 'walk', 'death'):
        ang = (-104 if J['sgn'] < 0 else -76) + (p['wpn'] - PD.JOB_W_IDLE[c.job]) * 0.5   # 지팡이를 바깥으로 기울여 얼굴을 가리지 않게
    if p.get('weapon_drop') and p.get('anim') == 'sit':
        # 앉아 쉴 때: 무기를 몸 뒤쪽 바닥에 눕혀 둠 (다리를 가리지 않게)
        if sub == 'back':
            at = (-s['arm'] * 1.1, -0.6) if side else (s['arm'] * 1.25, -0.8)
            _weapon(c, c.ground, d, at, 0, dict(p, wlen=1.0, string='rest', arrow=False), 'side', dropped=True)
        return
    if p.get('weapon_drop'):
        if sub == 'near':
            gd = c.ground
            _weapon(c, gd, d, (s['arm'] * 0.9, -1.2), 0, dict(p, wlen=1.0, string='rest', arrow=False), 'side', dropped=True)
        return
    if d.get('shield'):
        if p.get('shield_front'):
            ssub = 'near'
        else:
            ssub = 'far' if side else ('near' if view == 'front' else 'back')
        if sub == ssub:
            _shield(c, d, J['hF'], view)
    if d.get('dual') and sub == ('far' if side else ('near' if view == 'front' else 'back')):
        fang = ang + (10 if side else 0)
        _weapon(c, g, d, J['hF'], fang if view != 'front' else 180 - p['wpn'] - 20, p, view, off=True)
    if sub == wsub:
        _weapon(c, g, d, J['hN'], ang, p, view)
    if sub == 'near':
        _fx(c, d, ang)


def _weapon(c, g, d, hand, ang, p, view, dropped=False, off=False):
    kind = d['kind']
    L = d['wlen'] * p.get('wlen', 1.0)
    if kind == 'staff' and view != 'side' and not dropped:
        hand = (hand[0] + c.J['sgn'] * 3.2, hand[1])
    dd = dirv(ang)
    n = (-dd[1], dd[0])
    at = lambda t, o=0.0: (hand[0] + dd[0] * t + n[0] * o, hand[1] + dd[1] * t + n[1] * o)
    if kind in ('sword', 'dagger', 'kukri'):
        bw = d.get('blade_w', 1.25 if kind == 'sword' else 1.0)
        g.cap(at(-2.0), at(0.4), 0.75, ramp(d['grip']))
        g.ell(at(-2.4), 0.9, 0.9, ramp(d['hilt']))
        g.cap(at(1.0, -2.1 if kind == 'sword' else -1.4), at(1.0, 2.1 if kind == 'sword' else 1.4), 0.7, ramp(d['hilt']))
        bl = ramp(d['blade'])
        if kind == 'kukri':
            g.poly([at(1.4, -bw * 0.8), at(L * 0.6, -bw * 1.4), at(L, bw * 0.6), at(L * 0.7, bw * 1.6), at(1.4, bw)], bl, shade=0.35)
        else:
            g.poly([at(1.4, -bw), at(L - 2.2, -bw), at(L, 0), at(L - 2.2, bw), at(1.4, bw)], bl, shade=0.35, grad=((n[0], n[1]), -0.5))
            g.line(at(2.0, 0), at(L - 2.4, 0), bl[4])
    elif kind == 'bow':
        R = L * 0.55
        cc = at(-R * 0.55)
        wd = ramp(d['wood'])
        po, pi = [], []
        for i in range(13):
            a = math.radians(ang - 62 + i * (124 / 12))
            po.append((cc[0] + math.cos(a) * R, cc[1] + math.sin(a) * R))
            pi.append((cc[0] + math.cos(a) * (R - 1.4), cc[1] + math.sin(a) * (R - 1.4)))
        g.poly(po + pi[::-1], wd, shade=0.3)
        if d.get('trim'):
            for q in (po[0], po[-1], po[6]):
                g.px(q, ramp(d['trim'])[3])
        t0, t1 = po[0], po[-1]
        sc = ramp(d['string'])[3]
        if p.get('string') in ('pull', 'full') and view == 'side':
            pull = c.J['hF']
            g.line(t0, pull, sc)
            g.line(pull, t1, sc)
            if p.get('arrow'):
                tip = at(R * 0.7)
                g.line(pull, tip, ramp('#8a6a40')[3])
                g.poly([add(tip, n, -1), add(tip, dd, 2.2), add(tip, n, 1)], ramp('#c8d0dc'), shade=0.4)
                g.px(add(pull, dd, 0.6), (220, 60, 50))
        else:
            g.line(t0, t1, sc)
            if p.get('arrow'):
                g.line(at(-R * 1.0), at(R * 0.6), ramp('#8a6a40')[3])
    elif kind == 'staff':
        wd = ramp(d['wood'])
        g.cap(at(-L * 0.42), at(L * 0.86), 0.85, wd)
        top = at(L * 0.95)
        gem = ramp(d['gem'])
        mt = ramp(d.get('metal', '#d4a840'))
        t = d['top']
        if t == 'orb':
            g.cap(add(top, (-1.6, 1.2)), add(top, (0, -1.0)), 0.6, wd)
            g.cap(add(top, (1.6, 1.2)), add(top, (0, -1.0)), 0.6, wd)
            g.ell(add(top, (0, -0.3)), 1.6, 1.6, gem, glow=True)
        elif t == 'crescent':
            m0, tt = c.cv.ring(g.xf(add(top, (0, -0.5))), 3.4, 2.0, g.xf.ang(ang) + 50, g.xf.ang(ang) + 310)   # 끝이 위로 열린 초승달
            c.cv.fill(m0, np.full(m0.shape, 0.3), mt)
            g.ell(add(top, (0, -0.5)), 1.3, 1.3, gem, glow=True)
        elif t == 'ring':
            m0, tt = c.cv.ring(g.xf(add(top, (0, -1.0))), 3.0, 1.8, 0, 359.9)
            c.cv.fill(m0, np.full(m0.shape, 0.3), mt)
            g.ell(add(top, (0, -1.0)), 1.1, 1.1, gem, glow=True)
        else:  # cross
            g.cap(add(top, (0, 1.5)), add(top, (0, -3.0)), 0.8, mt)
            g.cap(add(top, (-1.8, -1.2)), add(top, (1.8, -1.2)), 0.8, mt)
            g.ell(add(top, (0, -1.2)), 0.9, 0.9, gem, glow=True)


def _shield(c, d, hand, view):
    g = c.g
    wd = ramp(d['shield_wood'])
    mt = ramp(d['shield_metal'])
    em = ramp(d['emblem'])
    side = view == 'side'
    cc = (hand[0] + (1.3 if side else 0), hand[1] - 1)
    if d['shield'] == 'round':
        r = 4.6
        rx = r * (0.5 if side else 1.0)
        g.ell(cc, rx, r, wd)
        if view != 'back':
            g.ell(cc, rx * 0.4, r * 0.4, mt)
            if not side:
                g.line((cc[0] - rx * 0.75, cc[1]), (cc[0] - rx * 0.45, cc[1]), em[2])
                g.line((cc[0] + rx * 0.45, cc[1]), (cc[0] + rx * 0.75, cc[1]), em[2])
    else:
        h, w = 6.5, (2.0 if side else 4.2)
        g.poly([(cc[0] - w, cc[1] - h * 0.5), (cc[0] + w, cc[1] - h * 0.5), (cc[0] + w, cc[1] + h * 0.15), (cc[0], cc[1] + h * 0.6),
                (cc[0] - w, cc[1] + h * 0.15)], wd, shade=0.2, grad=((-0.6, -1), 0.6))
        if view != 'back':
            g.line((cc[0], cc[1] - h * 0.4), (cc[0], cc[1] + h * 0.45), em[3])
            g.line((cc[0] - w * 0.7, cc[1] - h * 0.1), (cc[0] + w * 0.7, cc[1] - h * 0.1), em[3])


def _fx(c, d, wang):
    g, cv, s, J, p, view = c.g, c.cv, c.s, c.J, c.p, c.view
    fx, k = p.get('fx'), p.get('fxk', 0.0)
    magic = ramp(d.get('magic', '#60c0ff'))
    hand = J['hN']
    # 휘두르기 잔상
    if p.get('smear'):
        a0, a1 = p['smear']
        a0 = max(a0, a1 - 125)
        lead_end = True
        if view == 'front':
            a0, a1 = 180 - a1, 180 - a0
            lead_end = False
        sh = J['shN']
        R = math.hypot(hand[0] - sh[0], hand[1] - sh[1]) + d['wlen']
        cc = g.xf(sh)
        lo, hi = g.xf.ang(min(a0, a1)), g.xf.ang(max(a0, a1))
        mring, t = cv.ring(cc, R + 0.6, R - 4.0, lo, hi)
        lead = t if lead_end else 1 - t
        dist = np.sqrt((cv.X - cc[0]) ** 2 + (cv.Y - cc[1]) ** 2)
        thick = 0.6 + lead * 3.4
        band = mring & (dist >= R + 0.6 - thick)
        cv.flat(band, (214, 228, 255), glow=True)
        cv.flat(band & (dist >= R + 0.6 - thick * 0.45), (255, 255, 255), glow=True)
    if not fx:
        return
    if fx in ('orb', 'charge', 'burst') and d['kind'] == 'staff':
        dd = dirv(wang)
        hh = hand if view == 'side' else (hand[0] + J['sgn'] * 3.2, hand[1])
        top = (hh[0] + dd[0] * d['wlen'] * 0.95, hh[1] + dd[1] * d['wlen'] * 0.95)
        if fx == 'orb':
            r = 1.4 + k * 1.5
            g.ell(top, r, r, magic, glow=True, line=False)
        elif fx == 'charge':
            r = 2.2 + k * 1.6
            g.ell(top, r, r, magic, glow=True, line=False)
            g.ell(top, r * 0.5, r * 0.5, [magic[4]] * 5, glow=True, line=False)
            for a in range(0, 360, 45):
                g.px(add(top, dirv(a + k * 30, r + 2 + (a % 90 == 0) * 1.5)), magic[3], glow=True)
        else:
            r = 3.5 + k * 3
            for a in range(0, 360, 30):
                g.line(add(top, dirv(a, r * 0.4)), add(top, dirv(a, r * (1.0 if a % 60 == 0 else 0.7))), magic[3], glow=True)
            g.ell(top, r * 0.45, r * 0.45, [magic[4]] * 5, glow=True, line=False)
    elif fx == 'lines':
        for i in range(3):
            y = hand[1] + (i - 1) * 2.5
            x1 = hand[0] - 2 - i
            g.line((x1 - 6 * k, y), (x1, y), (255, 248, 230), glow=True)
    elif fx == 'spark':
        dd = dirv(wang)
        tip = (hand[0] + dd[0] * d['wlen'] * p.get('wlen', 1.0), hand[1] + dd[1] * d['wlen'] * p.get('wlen', 1.0))
        for a in range(0, 360, 45):
            ln = 2.5 if a % 90 == 0 else 1.5
            g.line(add(tip, dirv(a, 0.8)), add(tip, dirv(a, ln * k + 0.8)), (255, 244, 200), glow=True)
        g.px(tip, (255, 255, 255), glow=True)
    elif fx == 'aura':
        # 방패 주위 빛 테두리
        hf = J['hF']
        cc = g.xf((hf[0] + (1.3 if c.side else 0), hf[1] - 1))
        r = 6.0 + k * 1.5
        m0, _ = cv.ring(cc, r, r - 1.2, 0, 359.9)
        cv.flat(m0, (150, 220, 255), glow=True)
        for a in range(0, 360, 60):
            g.px(add((hf[0], hf[1] - 1), dirv(a + k * 40, r + 1.5)), (220, 245, 255), glow=True)
    elif fx == 'smoke':
        sm = ramp('#8a8494')
        base_y = -2.0
        for i, (x, r) in enumerate(((-6, 3.2), (-2, 4.0), (3, 3.6), (7, 2.8), (0, 3.0))):
            rr = r * k
            if rr < 0.8:
                continue
            gy = base_y - (i % 2) * 3 - k * 3
            c.ground.ell((x * (0.6 + k * 0.5), gy), rr, rr * 0.85, sm, glow=True, line=False)
    elif fx == 'circle':
        # 머리 위 마법진
        top = add(J['head'], (0, -s['head_ry'] - 6))
        cc = g.xf(top)
        rx_ = 4 + k * 8
        ry_ = rx_ * 0.35
        m0, _ = cv.ellipse(cc, rx_, ry_)
        m1, _ = cv.ellipse(cc, rx_ - 1.2, ry_ - 0.8)
        cv.flat(m0 & ~m1, magic[3], glow=True)
        if k > 0.5:
            m2, _ = cv.ellipse(cc, rx_ * 0.55, ry_ * 0.55)
            m3, _ = cv.ellipse(cc, rx_ * 0.55 - 1, ry_ * 0.55 - 0.7)
            cv.flat(m2 & ~m3, magic[4], glow=True)
            for a in range(0, 360, 45):
                q = (cc[0] + math.cos(math.radians(a)) * rx_ * 0.78, cc[1] + math.sin(math.radians(a)) * ry_ * 0.78)
                cv.px(q[0], q[1], magic[4], glow=True)
    elif fx == 'meteor':
        # 불덩이가 대각선으로 떨어짐
        tgt = add(J['hN'], (14 if c.side else 0, 4 if c.side else 8))
        fr = ramp('#ff7030')
        for i in range(4):
            q = add(tgt, (-4 * i * k, -5 * i * k))
            r = (3.2 - i * 0.6) * (0.6 + 0.4 * k)
            if r > 0.6:
                g.ell(q, r, r, fr if i else ramp('#ffd060'), glow=True, line=False)
        g.ell(tgt, 1.6 * k + 0.5, 1.6 * k + 0.5, [(255, 255, 230)] * 5, glow=True, line=False)
    elif fx == 'holy':
        # 몸 주위 빛 기둥 + 떠오르는 빛 알갱이
        cc = (0.0, -s['leg'] - s['torso'] * 0.5)
        hol = ramp('#fff0a0')
        for a in range(-150, -20, 26):
            ln = 6 + k * 6
            g.line(add(cc, dirv(a, 9)), add(cc, dirv(a, 9 + ln)), hol[3] if a % 52 else hol[4], glow=True)
        for i in range(5):
            x = (i - 2) * 4
            y = -2 - ((i * 7 + int(k * 10)) % 14) - k * 6
            c.ground.px((x, y), hol[4], glow=True)
        if k > 0.9:
            m0, _ = cv.ring(g.xf((0, -0.5)), 10 + k, 8.8 + k, 180, 359.9)
            cv.flat(m0, hol[3], glow=True)


DRAW = {'body': draw_body, 'bottom': draw_bottom, 'top': draw_top, 'hair': draw_hair, 'weapon': draw_weapon}
