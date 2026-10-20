# 모험가 부위 정의 (체형·헤어·상의·하의·무기)
# 헤어·상의·하의의 바탕색은 KEY(명암 단계만 기록)로 그리고, 게임에서 모험가마다 고른 색으로 바꿔 칠함
# 고정색(가죽 벨트, 금속, 단추 등)은 그대로 남음

# ─── 몸 비율 ───────────────────────────────────────────────────────
FEMALE = dict(   # JRPG 치비 (약 2.5등신): 큰 머리, 짧고 통통한 몸
    leg=8.4, torso=8.2, head_rx=8.0, head_ry=7.4, sh_w=3.6, hip_w=2.3, arm=7.8, limb=1.8, hand_r=1.5, foot_len=2.6,
    torso_w=3.9, torso_d=3.1, stride=3.0, lift=2.0, body_r=3.2, female=True,
)
MALE = dict(
    leg=9.0, torso=9.0, head_rx=7.8, head_ry=7.2, sh_w=4.2, hip_w=2.5, arm=8.4, limb=2.0, hand_r=1.6, foot_len=2.8,
    torso_w=4.5, torso_d=3.5, stride=3.2, lift=2.1, body_r=3.6, female=False,
)

# ─── 체형 (피부·눈·얼굴 특징). 성별마다 5종 ─────────────────────────
BODIES = {
    'f': [
        dict(skin='#f8d6be', eye='#4a7ae0', eye_h=5),
        dict(skin='#f0c6a2', eye='#38a058', eye_h=5, freckles=True),
        dict(skin='#dca476', eye='#e09020', eye_h=4, leg_add=0.3),
        dict(skin='#9c6646', eye='#a060e0', eye_h=5, leg_add=0.4),
        dict(skin='#fde6d8', eye='#d03a58', eye_h=5, elf=True, leg_add=0.5),
    ],
    'm': [
        dict(skin='#f6d2b8', eye='#3a6ad0', eye_h=4),
        dict(skin='#eec09a', eye='#7a4a2a', eye_h=3, leg_add=0.3, scar=True),
        dict(skin='#d49a6a', eye='#3a9a5a', eye_h=4, leg_add=0.5),
        dict(skin='#7c4c34', eye='#e0a020', eye_h=4, leg_add=0.4),
        dict(skin='#f8e0d0', eye='#20a8a8', eye_h=4, elf=True, leg_add=0.6),
    ],
}

# ─── 헤어 (성별마다 10종) ─────────────────────────────────────────
#  bangs: blunt / side / parted / spiky / messy / swept / none
#  back : 뒷머리 길이 (0 = 없음, nape, chin, shoulder, waist)
#  sides: 옆머리 길이 (0, cheek, chin, chest)
#  extra: twin / pony_high / pony_low / pony_side / buns / braid / topknot / spikes / curls / slick
HAIRS = {
    'f': [
        dict(name='long_straight', bangs='blunt', back='waist', sides='chest'),
        dict(name='twin_tails', bangs='side', back='nape', sides='cheek', extra='twin'),
        dict(name='ponytail', bangs='side', back='nape', sides='cheek', extra='pony_high'),
        dict(name='bob', bangs='blunt', back='chin', sides='chin'),
        dict(name='pixie', bangs='messy', back='nape', sides='cheek', vol=0.95),
        dict(name='side_braid', bangs='parted', back='nape', sides='cheek', extra='braid'),
        dict(name='wavy_long', bangs='parted', back='waist', sides='chest', wavy=True, vol=1.08),
        dict(name='buns', bangs='blunt', back='nape', sides='cheek', extra='buns'),
        dict(name='hime', bangs='blunt', back='waist', sides='chin', straight_sides=True),
        dict(name='side_tail', bangs='swept', back='nape', sides='cheek', extra='pony_side'),
    ],
    'm': [
        dict(name='spiky', bangs='spiky', back='nape', sides='cheek', extra='spikes', vol=1.08),
        dict(name='messy', bangs='messy', back='nape', sides='cheek', vol=1.04),
        dict(name='slick_back', bangs='none', back='nape', sides=0, extra='slick'),
        dict(name='long_tied', bangs='parted', back='nape', sides='chin', extra='pony_low'),
        dict(name='buzz', bangs='none', back=0, sides=0, vol=0.9, short=True),
        dict(name='center_part', bangs='parted', back='nape', sides='cheek'),
        dict(name='wolf', bangs='messy', back='shoulder', sides='chin', shaggy=True),
        dict(name='side_swept', bangs='swept', back='nape', sides='cheek'),
        dict(name='topknot', bangs='side', back=0, sides='cheek', extra='topknot'),
        dict(name='curly', bangs='messy', back='nape', sides='cheek', extra='curls', vol=1.12),
    ],
}

# ─── 상의 (성별마다 5종) ─────────────────────────────────────────
#  hem: waist / hip / thigh / knee     sleeve: none / short / puff / long / wide
#  고정색: vest(조끼), armor(breast/full), belt, trim(테두리), buttons, inner(앞이 트인 옷 안쪽)
TOPS = {
    'f': [
        dict(name='blouse_corset', hem='hip', sleeve='puff', collar='round', vest='#4a2e22', trim='#f4ece0'),
        dict(name='tunic_dress', hem='thigh', sleeve='short', collar='round', belt='#6a4424'),
        dict(name='robe_top', hem='hip', sleeve='wide', collar='high', trim='#e0b850'),
        dict(name='light_armor', hem='hip', sleeve='short', collar='round', armor='breast', metal='#b8c2d0', belt='#5a3a22'),
        dict(name='jacket', hem='waist', sleeve='long', collar='high', inner='#f2eadc', buttons='#e0b040'),
    ],
    'm': [
        dict(name='tunic', hem='hip', sleeve='short', collar='round', belt='#6a4424'),
        dict(name='jerkin', hem='hip', sleeve='long', collar='round', vest='#6a4426', belt='#3e2a1c'),
        dict(name='plate', hem='hip', sleeve='short', collar='round', armor='full', metal='#a8b2c0', belt='#4a3020'),
        dict(name='long_coat', hem='knee', sleeve='long', collar='high', buttons='#e0b040', belt='#3a2a20'),
        dict(name='robe_top', hem='thigh', sleeve='wide', collar='high', trim='#e0b850', belt='#5a3a22'),
    ],
}

# ─── 하의 (성별마다 5종) ─────────────────────────────────────────
#  kind: pants / shorts / skirt / long_skirt / baggy / hakama      boots/socks/greaves: 고정색
BOTTOMS = {
    'f': [
        dict(name='short_skirt', kind='skirt', length=0.38, boots='#5a3424', boot_h=0.45),
        dict(name='long_skirt', kind='skirt', length=0.85, boots='#3a2a24', boot_h=0.12),
        dict(name='shorts', kind='shorts', socks='#2a2834', boots='#4a2e22', boot_h=0.35),
        dict(name='pants', kind='pants', boots='#3e2a20', boot_h=0.5),
        dict(name='pleated', kind='skirt', length=0.42, pleats=True, socks='#f0ece4', boots='#3a2420', boot_h=0.1),
    ],
    'm': [
        dict(name='pants', kind='pants', boots='#4a3022', boot_h=0.45),
        dict(name='knee_pants', kind='shorts', length=0.75, boots='#3a2a20', boot_h=0.55),
        dict(name='baggy', kind='baggy', boots='#2e2420', boot_h=0.5),
        dict(name='greaves', kind='pants', greaves='#9aa4b2', boots='#3a2a22', boot_h=0.35),
        dict(name='hakama', kind='hakama', boots='#2a2220', boot_h=0.12),
    ],
}

# ─── 무기 (직업마다 2종) ─────────────────────────────────────────
WEAPONS = {
    'warrior': [
        dict(name='longsword', kind='sword', blade='#c8d0dc', hilt='#d4a840', grip='#5a3420', wlen=10.0, shield='round',
             shield_wood='#8a5a30', shield_metal='#8a929e', emblem='#c83a2a'),
        dict(name='broadsword', kind='sword', blade='#d8dee8', hilt='#7a8494', grip='#3a2a22', wlen=11.0, blade_w=1.8, shield='kite',
             shield_wood='#2e5aa8', shield_metal='#e0e4ea', emblem='#f0d060'),
    ],
    'rogue': [
        dict(name='twin_daggers', kind='dagger', blade='#c8d0dc', hilt='#5a5e6a', grip='#3a2a22', wlen=5.5, dual=True),
        dict(name='kukri', kind='kukri', blade='#9aa0b0', hilt='#a03a2a', grip='#2a2028', wlen=6.0, dual=True),
    ],
    'archer': [
        dict(name='short_bow', kind='bow', wood='#8a5a30', string='#ece6d8', wlen=12.0),
        dict(name='long_bow', kind='bow', wood='#ece4d0', string='#f6f0e0', trim='#d4a840', wlen=14.0),
    ],
    'mage': [
        dict(name='orb_staff', kind='staff', wood='#6a4428', top='orb', gem='#50b0ff', wlen=16.0, magic='#60c0ff'),
        dict(name='crescent_staff', kind='staff', wood='#3a2a3a', top='crescent', gem='#ff4060', metal='#d4a840', wlen=16.5,
             magic='#ff7040'),
    ],
    'healer': [
        dict(name='ring_staff', kind='staff', wood='#f0ead8', top='ring', gem='#60e0a0', metal='#e0b840', wlen=16.0, magic='#fff0a0'),
        dict(name='scepter', kind='staff', wood='#d4a840', top='cross', gem='#80c0ff', metal='#f0f0f4', wlen=12.0, magic='#fff6c0'),
    ],
}

JOBS = ['warrior', 'rogue', 'archer', 'mage', 'healer']
JOB_ATTACK = {'warrior': 'slash', 'rogue': 'stab', 'archer': 'bow', 'mage': 'cast', 'healer': 'cast'}
JOB_SKILL = {'warrior': 'guard', 'rogue': 'stealth', 'archer': 'rapid', 'mage': 'meteor', 'healer': 'pray'}
JOB_W_IDLE = {'warrior': -55, 'rogue': 15, 'archer': -70, 'mage': -92, 'healer': -92}
