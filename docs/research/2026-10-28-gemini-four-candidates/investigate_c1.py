"""Candidate 1: Gap-Down PUT, RSI 45-55, AtrPct<0.03, bearish EMA. Pure snapshot + 1m candle forward window.
Read-only. Reports target/stop/ambiguous/unresolved with bounds, clustering, and per-day/symbol split."""
import csv, sys, collections
f = sys.argv[1]
rows = list(csv.DictReader(open(f, encoding='utf-8-sig')))
def fl(x): return float(x) if x not in ('', None) else None
def cls(r, tgt, stp, win):
    ltp=fl(r['Ltp']); lo=fl(r[f'Fwd{win}Low']); hi=fl(r[f'Fwd{win}High'])
    if lo is None or hi is None: return 'unresolved'
    t = ltp-lo >= tgt; s = hi-ltp >= stp      # PUT
    return 'both' if t and s else 'target' if t else 'stop' if s else 'none'
sel=[]; tot=collections.Counter()
for r in rows:
    tot['all']+=1
    ltp,dopen,yc=fl(r['Ltp']),fl(r['DayOpen']),fl(r['YesterdayClose'])
    if not (dopen and yc): continue
    gap=(dopen-yc)/yc*100
    if gap>-0.30: continue
    tot['gapdown']+=1
    rsi=fl(r['Rsi5m']); atr=fl(r['AtrPct'])
    if rsi is None: tot['rsi5_null']+=1; continue
    r['_gap']=gap
    e1=fl(r['Ema20'])<fl(r['Ema50']); e5=fl(r['Ema20_5m'])<fl(r['Ema50_5m'])
    r['_e1'],r['_e5']=e1,e5
    if 45<=rsi<55 and atr<0.03 and e1 and e5: sel.append(r)
print('rows',dict(tot),'selected',len(sel))
days=collections.Counter((r['Symbol'],r['MinuteIst'][:10]) for r in sel)
print('symbol-days',len(days)); print(sorted(days.items(),key=lambda x:-x[1])[:12])
print('by symbol',collections.Counter(r['Symbol'] for r in sel))
# dedupe to non-overlapping entries: first minute, then skip 15 min after
def dedupe(rs,gap=15):
    out=[];last={}
    for r in sorted(rs,key=lambda r:(r['Symbol'],r['MinuteIst'])):
        k=r['Symbol'];t=r['MinuteIst']
        h=int(t[11:13])*60+int(t[14:16]);d=t[:10]
        if (k,d) in last and h-last[(k,d)]<gap: continue
        last[(k,d)]=h;out.append(r)
    return out
for label,rs in (('ALL minutes',sel),('non-overlap 15m',dedupe(sel))):
    for tg,sp,nm in ((20,20,'20pt/20pt'),):
        for win in (15,30):
            c=collections.Counter(cls(r,tg,sp,win) for r in rs)
            n=len(rs);res=c['target']+c['stop']
            print(f'{label} n={n} {nm} win{win}: {dict(c)} target-first(lower bound, both=stop)={c["target"]/n:.2%} '
                  f'upper(both=target)={(c["target"]+c["both"])/n:.2%} of resolved={c["target"]/max(res,1):.2%}')
# ATR-scaled: target 1.5 ATR, stop 1.25 ATR (live-like)
def clsA(r,win):
    a=fl(r['AtrRaw']); return cls(r,1.5*a,max(1.25*a,0),win)
for label,rs in (('ALL minutes',sel),('non-overlap 15m',dedupe(sel))):
    for win in (15,30):
        c=collections.Counter()
        for r in rs:
            a=fl(r['AtrRaw']);ltp=fl(r['Ltp']);lo=fl(r[f'Fwd{win}Low']);hi=fl(r[f'Fwd{win}High'])
            if lo is None: c['unresolved']+=1;continue
            t=ltp-lo>=1.5*a;s=hi-ltp>=1.25*a
            c['both' if t and s else 'target' if t else 'stop' if s else 'none']+=1
        n=len(rs);print(f'{label} ATRscaled win{win}: {dict(c)} target(lb)={c["target"]/n:.2%} ub={(c["target"]+c["both"])/n:.2%}')
# Staleness: ltp vs candle close lag is known; show ATR pts typical
import statistics
print('median AtrRaw pts by symbol',{s:statistics.median(fl(r['AtrRaw']) for r in sel if r['Symbol']==s) for s in set(r['Symbol'] for r in sel)})
