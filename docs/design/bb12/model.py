import random, statistics
# Level costs to go from L(n) to L(n+1), n=1..9
SHARDS=[5,10,15,25,35,50,65,85,110]
GOLD  =[10,20,35,50,70,90,120,150,190]
# chests by run result: (played-ball shards, random shards, gold)
CHEST={"wood":(4,2,20),"silver":(8,6,50),"gold":(15,15,120)}
# player profiles: P(chest tier) per run
PROFILES={"new":{"wood":.75,"silver":.22,"gold":.03},
          "regular":{"wood":.45,"silver":.40,"gold":.15},
          "strong":{"wood":.20,"silver":.40,"gold":.40}}
def sim(profile, balls=8, main_share=.6, runs=600, seed=1):
    rnd=random.Random(seed); lv=[1]*balls; sh=[0]*balls; gold=0
    first={}  # level -> run when main reached it
    for r in range(1,runs+1):
        tier=rnd.choices(list(PROFILES[profile]),weights=PROFILES[profile].values())[0]
        p,q,g=CHEST[tier]
        main=0 if rnd.random()<main_share else rnd.randrange(balls)
        sh[main]+=p
        for _ in range(q): sh[rnd.randrange(balls)]+=1
        gold+=g
        # upgrade greedily: main first
        for b in [0]+list(range(1,balls)):
            while lv[b]<10 and sh[b]>=SHARDS[lv[b]-1] and gold>=GOLD[lv[b]-1]:
                sh[b]-=SHARDS[lv[b]-1]; gold-=GOLD[lv[b]-1]; lv[b]+=1
                if b==0 and lv[b] not in first: first[lv[b]]=r
        if all(l==10 for l in lv): return first, r
    return first, None
for prof in PROFILES:
    res=[sim(prof,seed=s) for s in range(200)]
    def med(level):
        v=[f.get(level) for f,_ in res if f.get(level)]
        return statistics.median(v) if v else None
    allmax=[r for _,r in res if r]
    print(prof, {L:med(L) for L in (2,3,5,7,10)}, "all8 max median", statistics.median(allmax) if allmax else ">400")
print("total per ball", sum(SHARDS), "shards", sum(GOLD), "gold")
