"""UI-2 mock: menu + card pick in the 'minimal roguelite' style, at the game's 270x480, saved x3.
No boxes around everything: big type, whitespace, one strong colour per action, chunky slabs with a drop shadow."""
import sys
sys.path.insert(0, '/tmp/claude-0/web/trailer')
from PIL import Image, ImageDraw
import pixeltext as pt

W, H, S = 270, 480, 3
BG = (17, 16, 24, 255)
INK = (12, 10, 18, 255)
WHITE = (240, 240, 248, 255)
DIM = (120, 116, 140, 255)
FAINT = (40, 38, 54, 255)
GOLD = (236, 178, 64, 255)
GOLD_DK = (150, 100, 30, 255)
PINK, GREEN, BLUE, YELLOW, RED = (255, 120, 190, 255), (110, 220, 110, 255), (80, 150, 255, 255), (255, 214, 70, 255), (240, 70, 60, 255)
RAIN = [(240, 80, 80, 255), (250, 170, 60, 255), (240, 220, 90, 255), (110, 220, 110, 255), (80, 150, 255, 255), (170, 110, 240, 255), (240, 110, 190, 255)]
SPR = '/mnt/user-data/uploads/ball-battle/BallBattleUnity/Assets/Art/Sprites/ball_{}.png'


def canvas():
    im = Image.new('RGBA', (W, H), BG)
    return im, ImageDraw.Draw(im)


def text(im, s, x, y, scale, col, align='l', shadow=True):
    w, _ = pt.measure(s, scale)
    if align == 'c': x = x - w // 2
    if align == 'r': x = x - w
    pt.draw(im, s, x, y, scale, col, shadow=INK if shadow else None)
    return w


def label_in(im, s, box, scale, col):
    """Centre the real ink of a label (marks included) inside the slab face box = (x0, y0, x1, y1)."""
    tmp = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    pt.draw(tmp, s, 0, 0, scale, col, shadow=None)
    l, t, r, b = tmp.getbbox()
    x = (box[0] + box[2] + 1 - (r - l)) // 2 - l
    y = (box[1] + box[3] + 1 - (b - t)) // 2 - t
    pt.draw(im, s, x, y, scale, col, shadow=None)


def slab(d, x0, y0, x1, y1, fill, shade):
    """A chunky button: flat colour, 3 px darker lip at the bottom, no outline."""
    d.rectangle([x0, y0 + 3, x1, y1 + 3], fill=INK)
    d.rectangle([x0, y0, x1, y1], fill=shade)
    d.rectangle([x0, y0, x1, y1 - 3], fill=fill)


def ball(im, bid, x, y):
    sp = Image.open(SPR.format(bid)).convert('RGBA')
    im.alpha_composite(sp, (x, y))


def gear(d, cx, cy, col):
    for dx, dy in [(0, -5), (0, 5), (-5, 0), (5, 0), (-4, -4), (4, 4), (-4, 4), (4, -4)]:
        d.rectangle([cx + dx - 1, cy + dy - 1, cx + dx + 1, cy + dy + 1], fill=col)
    d.ellipse([cx - 4, cy - 4, cx + 4, cy + 4], fill=col)
    d.ellipse([cx - 2, cy - 2, cx + 2, cy + 2], fill=BG)


def chevron(d, x, y, col):
    for i in range(4):
        d.point((x + i, y + i), fill=col); d.point((x + i, y + 7 - i), fill=col)
        d.point((x + i + 1, y + i), fill=col); d.point((x + i + 1, y + 7 - i), fill=col)


def menu():
    im, d = canvas()
    gear(d, 252, 18, DIM)
    # title: big rainbow letters, ball parade under it
    t = 'BALL BATTLE'
    cols, k = [], 0
    for ch in t:
        cols.append(RAIN[k % 7] if ch != ' ' else WHITE); k += ch != ' '
    w, _ = pt.measure(t, 3)
    pt.draw(im, t, (W - w) // 2, 46, 3, cols, outline=True)
    ids = ['blade', 'fang', 'pike', 'chorus', 'brawler', 'volley', 'venom']
    x0 = (W - (len(ids) * 32 + (len(ids) - 1) * 4)) // 2
    for i, b in enumerate(ids):
        ball(im, b, x0 + i * 36, 104 + (4 if i % 2 else 0))
    # primary action: one gold slab, the only filled thing on the screen
    slab(d, 24, 168, 246, 222, GOLD, GOLD_DK)
    label_in(im, 'VƯỢT ẢI', (24, 168, 246, 219), 4, INK)
    text(im, 'TRẬN 5/8 - ĐÃ LƯU', 135, 230, 1, DIM, 'c')
    # next goal: no box, a label, one line, a thin bar
    text(im, 'MỤC TIÊU', 24, 258, 1, DIM)
    text(im, '0/4', 246, 258, 1, DIM, 'r')
    text(im, 'HẠ VUA VỎ CỨNG', 24, 272, 2, WHITE)
    d.rectangle([24, 302, 246, 303], fill=FAINT)
    d.rectangle([24, 302, 24 + 0, 303], fill=GOLD)
    text(im, 'MỞ KHÓA BGLGXLQ', 24, 308, 1, PINK)
    # secondary actions: a plain list, big type, a colour tick each, chevrons; the first is 'hovered'
    items = [('CÁC BÓNG', YELLOW, True), ('SO TÀI', RED, False), ('SỔ TAY', BLUE, False), ('CÁCH CHƠI', GREEN, False)]
    y = 340
    for i, (label, col, badge) in enumerate(items):
        if i == 0:
            d.rectangle([16, y - 4, 254, y + 25], fill=(26, 25, 36, 255))
            d.rectangle([16, y - 4, 18, y + 25], fill=col)
        else:
            d.rectangle([24, y + 6, 27, y + 13], fill=col)
        text(im, label, 34, y - 2, 2, WHITE if i == 0 else (205, 203, 220, 255))
        if badge:
            bw = pt.measure(label, 2)[0]
            d.rectangle([38 + bw, y + 2, 51 + bw, y + 15], fill=YELLOW)
            text(im, '!', 45 + bw, y + 3, 1, INK, 'c', shadow=False)
        chevron(d, 238, y + 6, DIM if i else col)
        y += 32
    text(im, 'V1.0', 246, 466, 1, FAINT, 'r', shadow=False)
    return im


def icon_plus(d, x, y, col):
    d.rectangle([x + 9, y + 3, x + 14, y + 20], fill=col); d.rectangle([x + 3, y + 9, x + 20, y + 14], fill=col)


def icon_speed(d, x, y, col):
    for k in (0, 7):
        for i in range(8):
            d.rectangle([x + 4 + i, y + 12 - i + k, x + 5 + i, y + 14 - i + k], fill=col)
            d.rectangle([x + 18 - i, y + 12 - i + k, x + 19 - i, y + 14 - i + k], fill=col)


def icon_sword(d, x, y, col):
    for i in range(15):
        d.rectangle([x + 6 + i, y + 16 - i, x + 9 + i, y + 19 - i], fill=col)
    for i in range(7):
        d.rectangle([x + 2 + i, y + 12 + i, x + 4 + i, y + 13 + i], fill=GOLD)
    d.rectangle([x + 1, y + 20, x + 6, y + 24], fill=GOLD_DK)


def cards():
    im, d = canvas()
    text(im, 'TRẬN 1/8', 24, 22, 1, DIM)
    ball(im, 'blade', 214, 12)
    text(im, 'CHỌN 1 THẺ', 24, 36, 3, WHITE)
    text(im, 'MIỄN PHÍ', 24, 80, 1, GOLD)
    data = [('MÁU', '+15', GREEN, icon_plus, 'THƯỜNG'), ('TỐC ĐỘ', '+10%', BLUE, icon_speed, 'THƯỜNG'), ('CÔNG', '+15%', RED, icon_sword, 'HIẾM')]
    cw, ch, gap = 74, 132, 9
    x = (W - 3 * cw - 2 * gap) // 2
    for i, (name, val, col, icon, rar) in enumerate(data):
        sel = i == 2
        y = 106 - (10 if sel else 0)
        d.rectangle([x + 2, y + 4, x + cw + 2, y + ch + 4], fill=INK)               # shadow
        d.rectangle([x, y, x + cw, y + ch], fill=(30, 29, 42, 255) if sel else (24, 23, 34, 255))
        d.rectangle([x, y, x + cw, y + 5], fill=col)                                # colour band, no outline
        if sel:
            d.rectangle([x - 2, y - 2, x + cw + 2, y + ch + 2], outline=GOLD, width=2)
        icon(d, x + cw // 2 - 12, y + 22, col)
        text(im, name, x + cw // 2, y + 58, 1, (205, 203, 220, 255), 'c')
        text(im, val, x + cw // 2, y + 72, 3 if len(val) < 4 else 2, WHITE, 'c')
        text(im, rar, x + cw // 2, y + ch - 18, 1, GOLD if rar == 'HIẾM' else DIM, 'c')
        x += cw + gap
    # the chosen card explained, plain text on the background
    y = 268
    text(im, 'CÔNG +15%', 24, y, 2, WHITE)
    text(im, 'ĐÒN CẬN CHIẾN, VA CHẠM, ĐẠN', 24, y + 34, 1, (205, 203, 220, 255))
    text(im, 'MẠNH THÊM 15%', 24, y + 48, 1, (205, 203, 220, 255))
    w = pt.measure('MẠNH THÊM ', 1)[0]
    text(im, '15%', 24 + w, y + 48, 1, GOLD)
    text(im, 'TÍNH CẢ ĐỘC CỦA SPORE', 24, y + 62, 1, DIM)
    # actions: one gold slab, the reroll is just text
    slab(d, 24, 392, 246, 438, GOLD, GOLD_DK)
    label_in(im, 'VÀO TRẬN', (24, 392, 246, 435), 3, INK)
    text(im, 'ĐỔI THẺ  1', 24, 452, 1, DIM)
    text(im, 'MENU', 246, 452, 1, DIM, 'r')
    return im


def save(im, path):
    im.resize((W * S, H * S), Image.NEAREST).convert('RGB').save(path)


if __name__ == '__main__':
    out = sys.argv[1]
    m, c = menu(), cards()
    save(m, out + '/ui2-menu.png'); save(c, out + '/ui2-cards.png')
    both = Image.new('RGBA', (W * 2 + 10, H), (0, 0, 0, 255))
    both.paste(m, (0, 0)); both.paste(c, (W + 10, 0))
    both.resize(((W * 2 + 10) * 2, H * 2), Image.NEAREST).convert('RGB').save(out + '/ui2-mock.png')
