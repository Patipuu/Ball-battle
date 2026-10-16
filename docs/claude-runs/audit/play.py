"""Drive the WebGL build like a player. usage: play.py <url> <outdir> <profile_dir|-> <W>x<H> <mobile:0|1> action...
actions: wait:<ms>  click:<x>,<y> (native 270x480 coords)  shot:<name>  reload  hold:<x>,<y>,<ms>"""
import sys, json, time, os
from playwright.sync_api import sync_playwright
url,out,prof,size,mobile=sys.argv[1:6]; acts=sys.argv[6:]
W,H=map(int,size.split('x')); os.makedirs(out,exist_ok=True)
log=open(os.path.join(out,'console.txt'),'a')
with sync_playwright() as p:
    args=dict(viewport={'width':W,'height':H},is_mobile=mobile=='1',has_touch=mobile=='1',device_scale_factor=1)
    if prof!='-': ctx=p.chromium.launch_persistent_context(prof,headless=True,args=['--use-gl=swiftshader','--enable-unsafe-swiftshader','--autoplay-policy=no-user-gesture-required'],**args)
    else: ctx=p.chromium.launch(headless=True,args=['--use-gl=swiftshader','--enable-unsafe-swiftshader']).new_context(**args)
    pg=ctx.pages[0] if prof!='-' and ctx.pages else ctx.new_page()
    pg.on('console',lambda m: log.write(f'[{m.type}] {m.text}\n'))
    pg.on('pageerror',lambda e: log.write(f'[pageerror] {e}\n'))
    t0=time.time(); pg.goto(url); 
    def canvas():
        b=pg.locator('canvas').first.bounding_box(); return b
    def to_page(x,y):
        b=canvas(); s=min(b['width']/270,b['height']/480); s=max(1,int(s)) if s>=1 else s
        ox=b['x']+(b['width']-270*s)/2; oy=b['y']+(b['height']-480*s)/2
        return ox+x*s, oy+y*s
    for a in acts:
        k,_,v=a.partition(':')
        if k=='wait': pg.wait_for_timeout(int(v))
        elif k=='ready':   # wait until the Unity loading bar is gone
            pg.wait_for_function("document.querySelector('#unity-loading-bar')==null || getComputedStyle(document.querySelector('#unity-loading-bar')).display=='none'",timeout=180000)
            log.write(f'[audit] loaded in {time.time()-t0:.1f}s\n')
        elif k=='click':
            x,y=map(float,v.split(',')); px,py=to_page(x,y)
            if mobile=='1': pg.touchscreen.tap(px,py)
            else: pg.mouse.click(px,py)
        elif k=='hold':
            x,y,ms=v.split(','); px,py=to_page(float(x),float(y)); pg.mouse.move(px,py); pg.mouse.down(); pg.wait_for_timeout(int(ms)); pg.mouse.up()
        elif k=='shot': pg.screenshot(path=os.path.join(out,v+'.png'))
        elif k=='canvas': log.write(f'[audit] canvas {canvas()} page {W}x{H}\n')
        elif k=='reload': pg.reload()
        elif k=='idb':
            r=pg.evaluate('''async()=>{const dbs=indexedDB.databases?await indexedDB.databases():[];const out=[];
              for(const d of dbs){ if(!d.name) continue; const db=await new Promise((ok,no)=>{const q=indexedDB.open(d.name);q.onsuccess=()=>ok(q.result);q.onerror=()=>no(q.error)});
                for(const st of db.objectStoreNames){ const keys=await new Promise(ok=>{const t=db.transaction(st).objectStore(st).getAllKeys();t.onsuccess=()=>ok(t.result)}); out.push(d.name+'/'+st+': '+keys.map(String).filter(k=>!k.includes('Build')).join(' | ')); } db.close(); }
              return out.join(' ;; ');}''')
            log.write('[audit] idb '+v+'\n'+r+'\n')
        elif k=='portrait':   # stand-in for a portrait template: force the canvas to 540x960
            pg.evaluate("()=>{const c=document.querySelector('#unity-canvas'); c.style.width='540px'; c.style.height='960px'; const d=document.querySelector('#unity-container'); d.style.width='540px'; d.style.height='960px';}")
            pg.wait_for_timeout(800)
    ctx.close()
