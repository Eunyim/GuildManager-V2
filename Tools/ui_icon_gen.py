# 상단 정보줄 도트 아이콘 생성기 (16x16)
# 실행: python Tools/ui_icon_gen.py  → Assets/Resources/UI/Theme/icon_*.png
# 모양은 글자 격자로 그리고, 바깥 테두리(K)는 자동으로 둘러줌
import os
from PIL import Image

OUT = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'Resources', 'UI', 'Theme')

PAL = {
    'K': '1a0f0a',                                                # 테두리
    'L': 'fff1a8', 'Y': 'f2c94c', 'y': 'c8901e', 'd': '8a5a12',   # 금
    'Q': 'f0d0ff', 'P': 'b06ae0', 'p': '7a3cb0', 'q': '4a1f73',   # 보라 (영혼석)
    'C': 'bfe3ff', 'B': '6aa8e0', 'b': '3a6aa0',                  # 파랑 (명성)
    'W': 'f4e6c4', 'w': 'd6c094', 'x': 'a8895a',                  # 종이·천
    'R': 'd04a36', 'r': '8a2418',                                 # 빨강
    'N': 'b07a46', 'M': '8a5a32', 'm': '5f3c20',                  # 나무
    'S': 'e0e0e8', 's': 'a0a0b0', 'g': '6a6a78',                  # 돌·은
    'F': 'ffffff',
}

ICONS = {
    'gold': [  # 금화
        '................',
        '................',
        '.....YYYYY......',
        '....YLLLLYY.....',
        '...YLYYYYYyY....',
        '...YLYddddyY....',
        '...YLYdYYdyY....',
        '...YLYdYYdyY....',
        '...YLYdYYdyY....',
        '...YLYddddyY....',
        '...YYyyyyyyY....',
        '....YyyyyyY.....',
        '.....YYYYY......',
        '................',
        '................',
        '................',
    ],
    'spirit': [  # 영혼석 (보라 수정)
        '................',
        '.......Q........',
        '......QPp.......',
        '.....QPPpp......',
        '....QQPPppp.....',
        '...QQPPPpppq....',
        '...QPPPPpppq....',
        '...PPPPPppqq....',
        '...PPPPpppqq....',
        '....PPPppqq.....',
        '.....PPpqq......',
        '......Ppq.......',
        '.......q........',
        '................',
        '................',
        '................',
    ],
    'reputation': [  # 명성 (별)
        '................',
        '.......C........',
        '.......C........',
        '......CBB.......',
        '......CBB.......',
        '.CCCCCCBBBBBBb..',
        '..CBBBBBBBBBb...',
        '...CBBBBBBBb....',
        '....CBBBBBb.....',
        '....CBBbBBb.....',
        '...CBBb.bBBb....',
        '...CBb...bBb....',
        '..CBb.....bb....',
        '................',
        '................',
        '................',
    ],
    'day': [  # 날짜 (달력)
        '................',
        '....s.....s.....',
        '..RRsRRRRRsRR...',
        '..RRRRRRRRRRR...',
        '..rrrrrrrrrrr...',
        '..WWWWWWWWWWW...',
        '..WxWWxWWxWWW...',
        '..WWWWWWWWWWW...',
        '..WxWWxWWRWWW...',
        '..WWWWWWWWWWW...',
        '..WxWWxWWxWWW...',
        '..wwwwwwwwwww...',
        '................',
        '................',
        '................',
        '................',
    ],
    'reception': [  # 접수처 (종)
        '................',
        '.......YY.......',
        '......yYYy......',
        '.......YY.......',
        '.....YYYYYY.....',
        '....YLLYYYYy....',
        '...YLLYYYYYyy...',
        '...YLYYYYYYyy...',
        '...YYYYYYYyyy...',
        '..YYYYYYYYyyyy..',
        '..dddddddddddd..',
        '.mMMMMMMMMMMMMm.',
        '.mmmmmmmmmmmmmm.',
        '................',
        '................',
        '................',
    ],
    'questboard': [  # 퀘스트 보드 (두루마리)
        '................',
        '..NNNNNNNNNNNN..',
        '.NMMMMMMMMMMMMN.',
        '..mWWWWWWWWWWm..',
        '...WxxxxxxxxW...',
        '...WWWWWWWWWW...',
        '...WxxxxxxWWW...',
        '...WWWWWWWWWW...',
        '...WxxxxxxxxW...',
        '...WWWWWWWWWW...',
        '...WxxxxWWRRW...',
        '..mWWWWWWWRRm...',
        '.NMMMMMMMMMMMMN.',
        '..NNNNNNNNNNNN..',
        '................',
        '................',
    ],
    'inn': [  # 여관 (침대)
        '................',
        '................',
        '.N..............',
        '.N..............',
        '.NWWW...........',
        '.NWWWRRRRRRRR...',
        '.NwwwRRRRRRRRN..',
        '.NRRRRRRRRRRRN..',
        '.NrrrrrrrrrrrN..',
        '.NMMMMMMMMMMMN..',
        '.Nm.........N...',
        '.m..........m...',
        '................',
        '................',
        '................',
        '................',
    ],
    'pub': [  # 주점 (맥주잔)
        '................',
        '....FFFFFF......',
        '...FFFFFFFF.....',
        '...FWFFWFFF.....',
        '...NYYYYYYN.....',
        '...NYLYYYyNNN...',
        '...NYLYYYyN.N...',
        '...NYLYYYyN.N...',
        '...NYLYYYyN.N...',
        '...NYYYYYyNNN...',
        '...NYYYYYyN.....',
        '...MMMMMMMM.....',
        '................',
        '................',
        '................',
        '................',
    ],
    'temple': [  # 신전 (기둥 건물)
        '................',
        '.......YY.......',
        '......SSSS......',
        '....SSSSSSSS....',
        '..SSSSSSSSSSSS..',
        '..ssssssssssss..',
        '...S.sS..Ss.S...',
        '...S.sS..Ss.S...',
        '...S.sS..Ss.S...',
        '...S.sS..Ss.S...',
        '...S.sS..Ss.S...',
        '..SSSSSSSSSSSS..',
        '.gggggggggggggg.',
        '................',
        '................',
        '................',
    ],
}


def hexc(h):
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), 255)


def render(rows):
    im = Image.new('RGBA', (16, 16), (0, 0, 0, 0))
    px = im.load()
    filled = set()
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch != '.':
                px[x, y] = hexc(PAL[ch])
                filled.add((x, y))
    # 바깥 테두리: 칠해진 칸과 상하좌우로 맞닿은 빈칸
    for (x, y) in list(filled):
        for dx, dy in [(1, 0), (-1, 0), (0, 1), (0, -1)]:
            nx, ny = x + dx, y + dy
            if 0 <= nx < 16 and 0 <= ny < 16 and (nx, ny) not in filled and px[nx, ny][3] == 0:
                px[nx, ny] = hexc(PAL['K'])
    return im


os.makedirs(OUT, exist_ok=True)
for name, rows in ICONS.items():
    assert len(rows) == 16 and all(len(r) == 16 for r in rows), name
    render(rows).save(os.path.join(OUT, 'icon_' + name + '.png'))

# 미리보기 (확인용): PREVIEW=경로 python Tools/ui_icon_gen.py
if os.environ.get('PREVIEW'):
    sc = 8
    sheet = Image.new('RGBA', (len(ICONS) * (16 * sc + 8), 16 * sc), (40, 28, 20, 255))
    for i, name in enumerate(ICONS):
        im = Image.open(os.path.join(OUT, 'icon_' + name + '.png')).resize((16 * sc, 16 * sc), Image.NEAREST)
        sheet.alpha_composite(im, (i * (16 * sc + 8), 0))
    sheet.save(os.environ['PREVIEW'])
