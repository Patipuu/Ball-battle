from PIL import Image, ImageDraw
import math, collections, sys
RIM=(20,18,28,255); CLEAR=(0,0,0,0)
WHITE=(255,255,255,255); MID=(240,234,248,255); SHADE=(214,206,228,255)
# logo-space shapes (x, y in the 1146x1022 logo); hand bbox x 425..762, y 216..975
HX0,HX1,HY0,HY1=425,762,216,975
def fingers():
    # (x0,x1,ytop,ysplit,ybottom, topcol, botcol)
    return [
     (425,493,297,430,560,(255,206,20,255),(255,140,30,255)),   # yellow -> orange
     (512,582,244,380,560,(30,150,230,255),(40,70,190,255)),    # blue -> deep blue
     (598,668,216,370,560,(40,215,95,255),(10,150,60,255)),     # green -> dark green
     (690,762,272,470,640,(255,170,205,255),(255,80,140,255)),  # pink -> hot pink
    ]
RED=(235,25,25,255); DRED=(185,10,20,255); WRIST=(110,0,12,255)
def build(S, hand_h=0.74, xstretch=1.3, laurel=True):
    k=8; N=S*k; img=Image.new('RGBA',(N,N),CLEAR); d=ImageDraw.Draw(img)
    c=N/2; R=N/2
    # disc with diagonal shading (light top-left)
    d.ellipse([0,0,N-1,N-1],fill=RIM)
    inner=[k,k,N-1-k,N-1-k]
    d.ellipse(inner,fill=MID)
    mask=Image.new('L',(N,N),0); md=ImageDraw.Draw(mask); md.ellipse(inner,fill=255)
    tri=Image.new('RGBA',(N,N),CLEAR); td=ImageDraw.Draw(tri)
    td.polygon([(0,0),(N*0.62,0),(0,N*0.62)],fill=WHITE)
    td.polygon([(N,N*0.55),(N,N),(N*0.55,N)],fill=SHADE)
    img.paste(tri,(0,0),Image.composite(tri.getchannel('A'),Image.new('L',(N,N),0),mask))
    # laurel: arc on the left inside the rim, teal at the bottom to green at the top, with leaves
    if laurel:
        ra=R-k*2.2
        for a in range(100,262,1):
            t=(a-100)/162
            col=(int(0+30*(1-t)),int(200-90*t),int(80+30*t),255)
            x=c+ra*math.cos(math.radians(a)); y=c-ra*math.sin(math.radians(a))
            d.ellipse([x-k*0.75,y-k*0.75,x+k*0.75,y+k*0.75],fill=col)
        nleaf=max(4,S//7)
        for i in range(nleaf):
            a=118+i*(128/(nleaf-1))
            t=(a-100)/162
            col=(int(0+30*(1-t)),int(200-90*t),int(80+30*t),255)
            # a leaf: a short thick stroke from the stem pointing inward and up
            for f in range(0,10):
                rr=ra-k*(0.6+f*0.16); aa=a+f*1.2
                x=c+rr*math.cos(math.radians(aa)); y=c-rr*math.sin(math.radians(aa))
                w=k*(0.95-abs(f-4)*0.12)
                d.ellipse([x-w,y-w,x+w,y+w],fill=col)
    # hand mapping
    sc=hand_h*N/(HY1-HY0); hw=(HX1-HX0)*sc*xstretch
    ox=c-hw/2+k*0.6; oy=c-hand_h*N/2+k*0.4
    def P(x,y): return (ox+(x-HX0)*sc*xstretch, oy+(y-HY0)*sc)
    def rrect(x0,y0,x1,y1,col,rad=0):
        a=P(x0,y0); b=P(x1,y1); d.rounded_rectangle([a[0],a[1],b[0],b[1]],radius=rad*sc,fill=col)
    # palm and wrist
    rrect(425,500,762,915,RED,rad=90)
    rrect(425,700,762,915,DRED,rad=90)
    rrect(484,880,705,975,WRIST)
    for x0,x1,yt,ys,yb,ct,cb in fingers():
        rrect(x0,yt,x1,yb,cb,rad=34)
        rrect(x0,yt,x1,ys,ct,rad=34)
        if yt<ys: rrect(x0,yt+34,x1,ys,ct)
    # white cross
    rrect(556,402,627,750,WHITE)
    rrect(484,478,700,545,WHITE)
    # downsample by majority
    out=Image.new('RGBA',(S,S),CLEAR); px=img.load()
    for y in range(S):
        for x in range(S):
            cnt=collections.Counter(px[x*k+i,y*k+j] for i in range(k) for j in range(k))
            out.putpixel((x,y),cnt.most_common(1)[0][0])
    # keep a clean 1 px rim: any opaque pixel touching transparency is rim
    o=out.load()
    for y in range(S):
        for x in range(S):
            if o[x,y][3]==0: continue
            for dx,dy in ((1,0),(-1,0),(0,1),(0,-1)):
                xx,yy=x+dx,y+dy
                if not(0<=xx<S and 0<=yy<S) or o[xx,yy][3]==0: o[x,y]=RIM; break
    return out
if __name__=='__main__':
    sizes=[32,38,43,48]; outs=[build(s) for s in sizes]
    for s,im in zip(sizes,outs): im.save(f'ball_chorus_{s}.png')
    W=sum(i.width*8+16 for i in outs)+8; prev=Image.new('RGBA',(W,48*8+16),(40,40,50,255)); x=8
    for i in outs:
        b=i.resize((i.width*8,i.height*8),Image.NEAREST); prev.paste(b,(x,8),b); x+=i.width*8+16
    prev.save('/tmp/claude-0/-home-claude/logo_prev.png')
