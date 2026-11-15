import pandas as pd, numpy as np, itertools
D='docs/research/2026-10-26-winrate-60-search/'
LOT={'NIFTY':65,'BANKNIFTY':30,'SENSEX':20}; LOTS=4; DELTA=0.45
t=pd.read_csv(D+'trades.csv'); t['side']=t.Configuration.str[:3].map({'CAL':'CALL','PUT':'PUT'})
t=t[t.Symbol.isin(LOT)].copy()
c=pd.read_csv(D+'candles-1m.csv'); c['sym']=c.Symbol.str.replace('_1m','')
c['ist']=pd.to_datetime(c.CandleTime)+pd.Timedelta(hours=5,minutes=30)
c['date']=c.ist.dt.strftime('%Y-%m-%d')
bars={}
for (s,dte),g in c.groupby(['sym','date']):
    g=g.sort_values('ist'); bars[(s,dte)]=(g.ist.dt.strftime('%Y-%m-%d %H:%M').values,g.High.values,g.Low.values,g.Close.values)
def prep(sym,entry,side,atrpct,rng=None):
    dte=entry[:10]; b=bars.get((sym,dte))
    if b is None: return None
    ts,H,L,C=b
    i=np.searchsorted(ts,entry)
    if rng is not None: i=int(rng.integers(30,max(31,len(ts)-30)))
    elif i>=len(ts) or ts[i]!=entry: return None
    if i>=len(ts)-1: return None
    e=C[i]; atr=atrpct/100*e
    if not atr>0: return None
    d=1 if side=='CALL' else -1
    h=H[i+1:];l=L[i+1:];cl=C[i+1:]
    fav=((h-e) if d>0 else (e-l))/atr; adv=((e-l) if d>0 else (h-e))/atr; end=((cl-e)*d)/atr
    return fav,adv,end,np.maximum.accumulate(fav),atr
def sim(p,S,T,H,BE):
    fav,adv,end,mf,_=p; n=len(fav); H=min(H,n)
    stop=np.full(n,-S)
    if BE: stop=np.where(np.concatenate(([0],mf[:-1]))>=BE,0.0,-S)   # armed after a prior bar reached BE
    hs=(adv[:H]>=-stop[:H]); ht=(fav[:H]>=T) if T else np.zeros(H,bool)
    si=np.argmax(hs) if hs.any() else H; ti=np.argmax(ht) if ht.any() else H
    if si==H and ti==H: return end[H-1]
    return stop[si] if si<=ti else T      # stop wins a same-bar tie (conservative)
CFG=[(S,T,H,BE) for S in (.5,.75,1,1.25,1.5,2) for T in (.25,.5,.75,1,1.5,2,3,None) for H in (30,999) for BE in (None,.5,1) if not(BE and BE>=(T or 99))]
def run(df,rng=None):
    P=[]
    for r in df.itertuples():
        p=prep(r.Symbol,r.EntryIst,r.side,r.AtrPct,rng)
        if p is not None: P.append((p,r.Symbol,r.EntryIst[:10]))
    return P

rows=[];rng=np.random.default_rng(7)
for (side,sym),df in t.groupby(['side','Symbol']):
    mult=DELTA*LOT[sym]*LOTS; cost=(2*1+2*20/(LOT[sym]*LOTS))*LOT[sym]*LOTS
    for label,P in (('real',run(df)),('control',run(df,rng))):
        atr=np.array([x[0][4] for x in P]); dt=np.array([x[2] for x in P])
        dates=sorted(set(dt)); mid=dates[len(dates)//2]; h1=dt<mid
        for cfg in CFG:
            g=np.array([sim(x[0],*cfg) for x in P])
            net=g*atr*mult-cost; w=net>0
            def pf(m): 
                a=net[m]; l=-a[a<0].sum(); return a[a>0].sum()/l if l>0 else np.inf
            rows.append(dict(side=side,sym=sym,arm=label,S=cfg[0],T=cfg[1],H=cfg[2],BE=cfg[3],n=len(net),days=len(dates),
              win=w.mean(),pf=pf(np.ones(len(net),bool)),net=net.sum(),pf1=pf(h1),pf2=pf(~h1),win1=w[h1].mean(),win2=w[~h1].mean(),n1=int(h1.sum()),n2=int((~h1).sum())))
R=pd.DataFrame(rows); R.to_csv(D+'exit-grid.csv',index=False)
pd.set_option('display.width',250)
for (side,sym),x in R.groupby(['side','sym']):
    a=x[x.arm=='real']; b=x[x.arm=='control']
    ref=a[(a.S==1.25)&(a.T==2)&(a.H==999)&(a.BE.isna())].iloc[0]
    print(f"== {side} {sym}  n={ref.n} days={ref.days}  reference S1.25/T2: win {ref.win:.1%} PF {ref.pf:.2f} net {ref.net:,.0f}")
    q=a[(a.win>=.60)&(a.pf>=1)&(a.net>0)&(a.pf1>1)&(a.pf2>1)]
    print(' configs with win>=60% and PF>1 in both halves:',len(q),'of',len(a))
    print(' best win-rate config with PF>=ref PF:'); 
    z=a[a.pf>=ref.pf].sort_values('win',ascending=False).head(3); print(z[['S','T','H','BE','win','pf','net','pf1','pf2']].round(2).to_string(index=False))
    print(' top PF configs:'); print(a.sort_values('pf',ascending=False).head(3)[['S','T','H','BE','win','pf','net','pf1','pf2']].round(2).to_string(index=False))
    print(' CONTROL (random entries): max win %.1f%%, max PF %.2f, cfgs win>=60%% & PF>1: %d'%(b.win.max()*100,b.pf.max(),len(b[(b.win>=.6)&(b.pf>1)])))
