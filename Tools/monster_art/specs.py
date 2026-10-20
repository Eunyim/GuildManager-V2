# 몬스터별 몸 비율·색·장비
# 길이 단위는 도트(1유닛 = 48도트). key는 Assets/Art/Monsters/{key}.png 이름이자 프리팹 연결 키
GOBLIN = dict(
    kind='humanoid',
    leg=8.0, torso=7.5, head_rx=7.0, head_ry=6.4, sh_w=3.6, hip_w=2.2, arm=8.0, limb=1.5, hand_r=1.3, foot_len=2.4,
    torso_w=4.0, torso_d=3.2, stride=3.0, lift=2.0, body_r=3.5,
    skin='#6aa84a', cloth='#7a5634', pants='#5a4632', boots='#4a3020', leather='#5a3a22', metal='#a8b0bc', wood='#7a4e2c',
    trim='#8a2a22',
    eye='#f0c020', ears='goblin', nose='long', grin=True, belt=True, belly='skin', mouth_y=0.6,
    weapon='rusty', wlen=10.5, w_idle=-55, attack='slash',
)


def variant(base, **kw):
    d = dict(base)
    d.update(kw)
    return d


SPECS = {
    'goblin': GOBLIN,
}

ORC = dict(
    kind='humanoid',
    leg=14.0, torso=15.0, head_rx=8.0, head_ry=7.4, sh_w=6.6, hip_w=3.6, arm=13.5, limb=2.6, hand_r=2.2, foot_len=3.6,
    torso_w=7.0, torso_d=5.4, stride=4.5, lift=2.5, body_r=6.0, bob_px=1.4, chest_k=1.15,
    skin='#7e9c58', cloth='#6a4a2e', pants='#4e3a2c', boots='#3a2a1e', leather='#5a3a22', metal='#9aa0ac', wood='#6e4628',
    hair='#2a2630', trim='#9a2a22',
    eye='#ff5030', ears='orc', tusks=True, jaw=1.05, belt=True, belly='skin', mouth_y=0.62, eye_w=3, eye_h=3, eye_y=0.0,
    hat='mohawk', pauldron=True, bracer='leather',
    weapon='axe', wlen=16.0, w_idle=-70, attack='smash',
)

HUMAN = dict(
    kind='humanoid',
    leg=11.0, torso=10.0, head_rx=6.4, head_ry=6.0, sh_w=4.4, hip_w=2.6, arm=10.0, limb=1.8, hand_r=1.4, foot_len=2.8,
    torso_w=4.6, torso_d=3.4, stride=3.6, lift=2.2, body_r=4.0,
    skin='#ecc098', cloth='#4a4a5c', pants='#3a3442', boots='#2e2420', leather='#5a3a24', metal='#b0b6c2', wood='#6e4a2c',
    cape='#383848', trim='#7a2a2a', hair='#4a2a1a',
    eye='#6a90d0', belt=True, tall_boots=True, eye_y=0.18,
)

SKELETON = dict(
    kind='humanoid', bony=True,
    leg=14.0, torso=13.0, head_rx=7.0, head_ry=6.8, sh_w=5.0, hip_w=3.0, arm=13.0, limb=2.0, hand_r=1.6, foot_len=3.0,
    torso_w=5.0, torso_d=3.6, stride=4.0, lift=2.5, body_r=4.0,
    bone='#e8dec6', cloth='#4a3a2e', pants='#3a2e26', boots='#2a2420', leather='#4a3424', metal='#9aa2ae', wood='#5e4228',
    eye='#ff5038', eye_y=0.0,
    weapon='sword', wlen=14.0, w_idle=-60, attack='slash',
)

SPECS.update({
    'goblin_warrior': variant(GOBLIN, hat='helmet', shield='round', weapon='sword', metal='#9ca4b2', cloth='#6a4630', eye='#ff8a20'),
    'goblin_hunter': variant(GOBLIN, hat='feather', weapon='spear', wlen=13.0, attack='thrust', w_idle=-82, cloth='#4e6a36', pants='#4a3a28',
                             skin='#78b04e'),
    'goblin_priest': variant(GOBLIN, robe='cloth2', cloth2='#6a3a8a', trim='#e0c060', weapon='staff', staff_top='skull', wlen=16.0,
                             attack='cast', magic='#90ff60', caster=True, w_idle=-92, robe_flare=1.35, grin=False, eye='#b0ff60', blush=False),
    'goblin_shaman_king': variant(GOBLIN, leg=14.0, torso=14.0, head_rx=10.0, head_ry=9.0, sh_w=5.6, hip_w=3.4, arm=12.5, limb=2.3, hand_r=1.9,
                                  foot_len=3.4, torso_w=6.2, torso_d=4.8, stride=4.0, body_r=5.0, bob_px=1.4,
                                  skin='#5a9a4c', robe='cloth2', cloth2='#8a2a2a', trim='#e8c050', hat='bone_crown', weapon='staff',
                                  staff_top='totem', wlen=24.0, attack='cast', magic='#40e0a0', caster=True, w_idle=-92, robe_flare=1.5,
                                  eye='#ff4040', eye_w=3, eye_h=4),
    'orc': ORC,
    'orc_grunt': variant(ORC, skin='#64844c', hat='helmet', metal='#7c828e', pauldron=False, cloth='#5a3a28', eye='#ffb030', jaw=1.0),
    'orc_centurion': variant(ORC, leg=17.0, torso=19.0, head_rx=9.0, head_ry=8.4, sh_w=9.0, hip_w=4.6, arm=17.0, limb=3.4, hand_r=2.8,
                             foot_len=4.4, torso_w=9.4, torso_d=7.0, stride=5.5, body_r=8.0, bob_px=1.8,
                             skin='#6a8a4e', armor=True, hat='horned', metal='#8a909e', trim='#a82a2a', weapon='double_axe', wlen=21.0,
                             eye='#ff3020'),
    'bandit': variant(HUMAN, hat='hood', mask=True, weapon='dagger', wlen=7.0, attack='stab', w_idle=15),
    'bandit_archer': variant(HUMAN, hat='hood', cape='#3e5a3a', cloth='#5a4a34', weapon='bow', wlen=14.0, attack='bow', w_idle=-5,
                             eye='#5aa060'),
    'bandit_warrior': variant(HUMAN, leg=11.0, torso=12.5, head_rx=6.6, head_ry=6.2, sh_w=6.2, hip_w=3.6, arm=11.5, limb=2.5, hand_r=1.9,
                              torso_w=6.6, torso_d=5.0, body_r=5.5, chest_k=1.15, skin='#d8a07a', cloth='#6a4a34', hat='topknot',
                              hair='#5a2a18', bracer='leather', weapon='club', wlen=13.0, attack='smash', w_idle=-75, scarf=True,
                              trim='#8a2a22', eye='#8a5a30'),
    'skeleton': SKELETON,
    'skeleton_warrior': variant(SKELETON, hat='helmet', shield='round', armor=True, pauldron=True, metal='#8a909c', wood='#6a4a30'),
    'skeleton_archer': variant(SKELETON, hat='hood', cape='#4a3a2a', cloth='#4a3a2a', weapon='bow', wlen=16.0, attack='bow', w_idle=-5,
                               eye='#ffb040'),
    'skeleton_tank': variant(SKELETON, leg=16.0, torso=15.0, head_rx=7.6, head_ry=7.2, sh_w=6.2, arm=14.0, limb=2.4, torso_w=6.2, torso_d=4.6,
                             armor=True, pauldron=True, hat='spiked', shield='tower', weapon='mace', wlen=13.0, attack='smash', w_idle=-70,
                             eye='#40c8ff', metal='#6c7488'),
    'skeleton_necromancer': variant(SKELETON, leg=16.0, torso=16.0, head_rx=7.6, head_ry=7.2, sh_w=5.6, arm=14.0, torso_w=5.6, torso_d=4.2,
                                    robe='cloth2', cloth2='#3a2a5a', trim='#a080e0', hat='cowl', weapon='staff', staff_top='skull', wlen=24.0,
                                    attack='cast', magic='#b060ff', caster=True, w_idle=-92, eye='#c060ff', boots='#2a2030', robe_flare=1.4),
})

WOLF = dict(
    kind='quadruped',
    body_len=34.0, body_ry=8.0, body_w=8.5, leg=12.0, leg_r=2.2, head_r=6.5, snout=6.0, tail=13.0, stride=5.0, lift=3.0,
    fur='#a48a64', belly='#e4d4b2', dark='#4a3a30', nose='#2a2024', eye='#f0c040', tail_tip='belly', tail_ang=168, tail_curve=-7, tail_r=3.2, saddle_mark='#7a6448',
)

DRAGON = dict(
    kind='dragon', dragon=True,
    body_len=52.0, body_ry=14.0, body_w=15.0, leg=18.0, leg_r=4.0, head_r=10.0, snout=9.0, neck=14.0, tail=34.0, tail_r=5.0,
    stride=6.0, lift=4.0, wing_len=44.0, breath_len=50.0, bob_px=1.5, tail_ang=-170, tail_curve=6,
    fur='#b8322a', belly='#e8c070', dark='#5a1a1a', horn='#e8dcc0', wing='#7a2228', claw='#f0e8d0', nose='#5a1a1a', eye='#ffd040',
)

SPECS.update({
    'prairie_wolf': WOLF,
    'alpha_wolf': variant(WOLF, body_len=48.0, body_ry=11.0, body_w=12.0, leg=17.0, leg_r=3.0, head_r=9.0, snout=8.0, tail=22.0, tail_r=4.2,
                          stride=7.0, lift=4.0, bob_px=1.5, fur='#5a5868', belly='#a8a4b0', dark='#2a2830', mane='#3a3844', scar='#c86060', saddle_mark='#3e3c4a',
                          eye='#ff4040'),
    'dragon': DRAGON,
    'baby_dragon': variant(DRAGON, body_len=30.0, body_ry=9.0, body_w=10.0, leg=9.0, leg_r=3.0, head_r=9.0, snout=5.0, neck=6.0, tail=18.0,
                           tail_r=3.4, stride=4.0, lift=2.5, wing_len=18.0, breath_len=24.0, bob_px=1.0, fur='#d8582e', wing='#a83a2a',
                           belly='#f0d080'),
    'dragon_hatchling': variant(DRAGON, body_len=20.0, body_ry=6.5, body_w=7.0, leg=6.0, leg_r=2.2, head_r=7.0, snout=3.5, neck=3.0, tail=12.0,
                                tail_r=2.4, stride=3.0, lift=2.0, wing_len=11.0, breath_len=16.0, bob_px=1.0, fur='#e87a3e', wing='#c04a30',
                                belly='#f4dc9c'),
    'orc_rider': dict(kind='rider', mount=WOLF,
                      rider=variant(ORC, leg=9.0, torso=11.0, head_rx=6.6, head_ry=6.0, sh_w=5.0, hip_w=2.8, arm=10.0, limb=2.0, hand_r=1.7,
                                    foot_len=2.8, torso_w=5.2, torso_d=4.0, body_r=4.0, bob_px=1.0, hat='horned', pauldron=False,
                                    weapon='spear', wlen=18.0, attack='thrust', w_idle=-75)),
})

SPECS.update({
    'slime': dict(kind='slime', r=17.0, h=26.0, color='#5ac84a'),
    'king_slime': dict(kind='slime', r=22.0, h=34.0, color='#4a8ae8', crown=True, face_line=(16, 22, 44)),
    'mimic': dict(kind='mimic', w=34.0, h=30.0, d=26.0, wood='#8a5a30', iron='#5a5e6a'),
})
