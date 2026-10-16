"""HUNTER ball sprite: a smooth cartoon portrait of Phu's friend, with proportions measured from his photo
(round wide face, full cheeks, ears, thick spiky black hair, big round thin-framed glasses, smiling eyes,
broad nose, wide toothy grin, red collar) on the ball's teal. Drawn at 8x and downsampled by majority."""
from PIL import Image, ImageDraw
import collections
RIM=(20,18,28,255); CLEAR=(0,0,0,0)
TEAL_L=(84,216,172,255); TEAL=(52,196,150,255); TEAL_D=(36,156,118,255)
SKIN=(246,196,164,255); SKIN_L=(252,214,186,255); SKIN_D=(222,162,130,255)
HAIR=(30,24,30,255); HAIR_L=(70,60,70,255)
FRAME=(206,178,160,255); LENS=(250,222,200,255); EYE=(64,36,34,255)
MOUTH=(120,30,36,255); TEETH=(255,252,246,255); NOSE=(226,156,124,255); BLUSH=(248,168,150,255)
SHIRT=(242,242,248,255); SHIRT_D=(208,210,224,255); TRIM=(222,38,46,255)
def build(S):
    k=8; N=S*k; img=Image.new('RGBA',(N,N),CLEAR); d=ImageDraw.Draw(img)
    c=N/2; R=N/2-k
    P=lambda x,y:(c+x*R,c+y*R)
    def ell(cx,cy,rx,ry,col): d.ellipse([*P(cx-rx,cy-ry),*P(cx+rx,cy+ry)],fill=col)
    def poly(pts,col): d.polygon([P(x,y) for x,y in pts],fill=col)
    def arc(cx,cy,rx,ry,a0,a1,col,w): d.arc([*P(cx-rx,cy-ry),*P(cx+rx,cy+ry)],a0,a1,fill=col,width=int(w*R))
    def ring(cx,cy,rx,ry,col,w): d.ellipse([*P(cx-rx,cy-ry),*P(cx+rx,cy+ry)],outline=col,width=int(w*R))
    # backdrop
    d.ellipse([0,0,N-1,N-1],fill=RIM)
    layer=Image.new('RGBA',(N,N),CLEAR); d=ImageDraw.Draw(layer)
    d.rectangle([0,0,N,N],fill=TEAL)
    poly([(-1.2,-1.2),(0.3,-1.2),(-1.2,0.3)],TEAL_L); poly([(1.2,0.3),(1.2,1.2),(0.3,1.2)],TEAL_D)
    # shirt: white with a red trim on the collar
    poly([(-1.1,0.80),(-0.42,0.70),(0.42,0.70),(1.1,0.80),(1.1,1.2),(-1.1,1.2)],SHIRT)
    poly([(0.42,0.70),(1.1,0.80),(1.1,1.2),(0.62,1.2)],SHIRT_D)
    poly([(-1.1,0.74),(-0.42,0.64),(0.42,0.64),(1.1,0.74),(1.1,0.84),(0.42,0.74),(-0.42,0.74),(-1.1,0.84)],TRIM)
    poly([(-0.30,0.70),(0.30,0.70),(0.0,0.96)],TRIM)
    poly([(-0.20,0.70),(0.20,0.70),(0.0,0.88)],SKIN_D)
    # face: wider at the cheeks and jaw than at the forehead
    ell(0.0,0.06,0.58,0.76,SKIN)
    poly([(0.46,0.30),(0.58,0.24),(0.50,0.56),(0.26,0.78),(0.06,0.82),(0.30,0.62)],SKIN_D)   # shade on the right jaw
    ell(-0.24,-0.42,0.26,0.14,SKIN_L)                                                        # light on the high forehead
    # hair: swept up and tall, a high hairline (forehead shows), short at the temples
    import math
    def edge(y): return 0.58*math.sqrt(max(0.0,1-((y-0.06)/0.76)**2))+0.02   # face outline at height y
    side=[(-edge(y)+0.01,y) for y in (-0.12,-0.25,-0.40,-0.50,-0.56)]
    front=[(-0.20,-0.62),(0.0,-0.60),(0.20,-0.62)]
    cap=[(0.72*math.cos(math.radians(t)),-0.36-0.52*math.sin(math.radians(t))) for t in range(-12,193,12)]
    hair=side+front+[(edge(y)-0.01,y) for y in (-0.56,-0.50,-0.40,-0.25,-0.12)]+cap
    poly(hair,HAIR)
    ell(-0.60,0.02,0.11,0.15,SKIN_D); ell(0.60,0.02,0.11,0.15,SKIN_D)                        # ears below the hair
    arc(-0.10,-0.62,0.36,0.18,200,300,HAIR_L,0.06)                                           # one soft shine on the hair
    # glasses: big round lenses, light translucent frames, smiling crescent eyes
    for cx in (-0.29,0.29):
        ell(cx,-0.14,0.27,0.22,LENS)
        ring(cx,-0.14,0.27,0.22,FRAME,0.08)
        arc(cx,-0.08,0.13,0.10,200,340,EYE,0.075)
    poly([(-0.04,-0.20),(0.04,-0.20),(0.04,-0.13),(-0.04,-0.13)],FRAME)
    # round apple cheeks, broad nose with nostrils, smile lines
    ell(-0.40,0.18,0.10,0.07,BLUSH); ell(0.40,0.18,0.10,0.07,BLUSH)
    ell(0.0,0.10,0.17,0.12,NOSE)
    # very wide grin: a broad row of upper teeth, a thin dark line under it
    poly([(-0.42,0.27),(0.42,0.27),(0.34,0.42),(0.18,0.53),(-0.18,0.53),(-0.34,0.42)],MOUTH)
    poly([(-0.36,0.30),(0.36,0.30),(0.30,0.39),(0.14,0.45),(-0.14,0.45),(-0.30,0.39)],TEETH)
    # chin
    d=ImageDraw.Draw(img)
    m=Image.new('L',(N,N),0); ImageDraw.Draw(m).ellipse([k,k,N-1-k,N-1-k],fill=255)
    img.paste(layer,(0,0),m)
    out=Image.new('RGBA',(S,S),CLEAR); px=img.load()
    for y in range(S):
        for x in range(S):
            cnt=collections.Counter(px[x*k+i,y*k+j] for i in range(k) for j in range(k))
            out.putpixel((x,y),cnt.most_common(1)[0][0])
    o=out.load()
    for _ in range(2):   # despeckle: a pixel with no neighbour of its colour takes its 4 neighbours' main colour
        for y in range(1,S-1):
            for x in range(1,S-1):
                me=o[x,y]
                if me[3]==0: continue
                if me in [o[x+i,y+j] for i in (-1,0,1) for j in (-1,0,1) if i or j]: continue
                c=collections.Counter(o[x+i,y+j] for i,j in ((1,0),(-1,0),(0,1),(0,-1))).most_common(1)[0][0]
                if c[3]: o[x,y]=c
    for y in range(S):
        for x in range(S):
            if o[x,y][3]==0: continue
            for dx,dy in ((1,0),(-1,0),(0,1),(0,-1)):
                xx,yy=x+dx,y+dy
                if not(0<=xx<S and 0<=yy<S) or o[xx,yy][3]==0: o[x,y]=RIM; break
    return out
if __name__=='__main__':
    sizes=[32,38,43,48]; outs=[build(s) for s in sizes]
    for s,i in zip(sizes,outs): i.save(f'ball_serpent_{s}.png')
    import os
    Wp=sum(i.width*8+16 for i in outs)+380; prev=Image.new('RGBA',(Wp,48*8+16),(40,40,50,255)); x=8
    for i in outs:
        b=i.resize((i.width*8,i.height*8),Image.NEAREST); prev.paste(b,(x,8),b); x+=i.width*8+16
    if os.path.exists('face_src.png'): prev.paste(Image.open('face_src.png').convert('RGB').resize((368,384)),(x,8))
    prev.save('/tmp/claude-0/-home-claude/chibi_prev.png')
