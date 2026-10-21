# TREE-LAB light check summary: run next to results-light-3001/ (python3 analyze_light.py)
import csv,glob,os,collections
base=os.path.join(os.path.dirname(os.path.abspath(__file__)),'results-light-3001')
W=["blade","fang","pike","brawler","volley","venom","aegis","rig"]
N={"blade":"EMBER","fang":"MANTIS","pike":"ANGLER","brawler":"WRECKER","volley":"SPARKLER","venom":"SPORE","aegis":"MIRROR","rig":"HIVE"}
lv10=collections.defaultdict(dict); lv=collections.defaultdict(dict)
for f in sorted(glob.glob(base+'/*.csv')):
    tag=os.path.basename(f)[3:-4]
    for r in csv.DictReader(open(f,encoding='utf-8-sig')):
        key=f"V{r['vitality']}P{r['power']}G{r['growth']}S{r['start']} {r['skill']}{r['final']}"
        if tag=='lv10': lv10[r['weapon']][key]=float(r['win_pct'])
        else: lv[r['weapon']][tag]=float(r['win_pct'])
best={}
for w in W:
    v=lv10[w]; b=max(v,key=v.get); best[w]=v[b]
    print(f"{N[w]:9} min {min(v.values()):5.1f} mean {sum(v.values())/len(v):5.1f} max {v[b]:5.1f} ({b})")
print("spread of bests", round(max(best.values())-min(best.values()),1))
for w in W:
    for k,a in lv10[w].items():
        if k.endswith("A3A"):
            kb=k[:-3]+"B3B"
            if kb in lv10[w] and abs(lv10[w][kb]-a)>8: print(f"A/B>8 {N[w]} {k[:-4]}: A {a} B {lv10[w][kb]}")
for w in W:
    l=lv[w]; g=lambda *t: max(l.get(x,0) for x in t)
    print(f"{N[w]:9} LV0 {l.get('lv0',0):5.1f} LV1 {g('lv1v','lv1p','lv1g'):5.1f} LV4 {g('lv4a','lv4b'):5.1f} LV7 {g('lv7a','lv7b','lv7s'):5.1f} LV10 {best[w]:5.1f}")
