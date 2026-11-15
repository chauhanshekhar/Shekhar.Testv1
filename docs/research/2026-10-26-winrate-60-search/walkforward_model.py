import pandas as pd, numpy as np, warnings
from sklearn.ensemble import HistGradientBoostingClassifier as GB
warnings.filterwarnings('ignore')
D='docs/research/2026-10-26-winrate-60-search/'
LOT={'NIFTY':65,'BANKNIFTY':30,'SENSEX':20}; LOTS=4; DELTA=.45
F=pd.read_pickle(D+'wide-features.pkl').reset_index(drop=True)
FE=['mo','atrpct','d20','d50','gap2050','slope20','rsi1','rsi5','ema5gap','ret5','ret15','ret30','ret60','daychg','gapopen','dayrangepos','dist_hi','dist_lo','range_atr','body','bar_atr','upshare10','eff']
F['symc']=F.sym.map({'NIFTY':0,'BANKNIFTY':1,'SENSEX':2}); FE=FE+['symc']
cost=F.sym.map(lambda s:(2*1+2*20/(LOT[s]*LOTS))*LOT[s]*LOTS)
dates=sorted(F.d.unique()); print('sessions',len(dates))
def evalsel(df,side):
    # one position per symbol at a time (exit minute known), net rupees
    rows=[]
    for sym,g in df.groupby('sym'):
        busy=-1
        for t,(mo,r,x,a,cs) in enumerate(zip(g.mo,g['r_'+side],g['x_'+side],g.atr,cost[g.index])):
            if mo<=busy: continue
            busy=mo+x; rows.append((g.d.iloc[t],sym,r*a*DELTA*LOT[sym]*LOTS-cs))
    return pd.DataFrame(rows,columns=['d','sym','net'])
def stats(r):
    if len(r)==0: return dict(n=0,win=np.nan,pf=np.nan,net=0,days=0)
    g=r.net[r.net>0].sum(); l=-r.net[r.net<0].sum()
    return dict(n=len(r),win=(r.net>0).mean(),pf=g/l if l>0 else np.inf,net=r.net.sum(),days=r.d.nunique())
def walk(side,shuffle=False,seed=0,qs=(.02,.05,.10)):
    rng=np.random.default_rng(seed); res={q:[] for q in qs}
    start=30; step=6
    for b in range(start,len(dates),step):
        trd=dates[:b-1]; ted=dates[b:b+step]        # purge 1 session
        tr=F[F.d.isin(trd)]; te=F[F.d.isin(ted)]
        cs_tr=cost[tr.index]; y=((tr['r_'+side]*tr.atr*DELTA*tr.sym.map(lambda s:LOT[s]*LOTS)-cs_tr)>0).values
        if shuffle: y=rng.permutation(y)
        m=GB(max_depth=3,learning_rate=.05,max_iter=150,min_samples_leaf=200,l2_regularization=5.0).fit(tr[FE],y)
        te=te.assign(p=m.predict_proba(te[FE])[:,1])
        for q in qs:
            thr=te.p.quantile(1-q); res[q].append(evalsel(te[te.p>=thr].sort_values(['sym','d','mo']),side))
    return {q:pd.concat(v) for q,v in res.items()}
rows=[]
base={}
for side in ('CALL','PUT'):
    oos=F[F.d.isin(dates[30:])]
    base[side]=stats(evalsel(oos.sort_values(['sym','d','mo']),side)); print(side,'OOS-period unfiltered (1 pos at a time):',{k:round(v,3) for k,v in base[side].items()})
    real=walk(side); ctrl=[walk(side,True,s) for s in (1,2,3)]
    for q in real:
        rs=stats(real[q]); cs=[stats(c[q]) for c in ctrl]
        print(f" {side} top {int(q*100)}%: n={rs['n']} days={rs['days']} win={rs['win']:.1%} PF={rs['pf']:.2f} net={rs['net']:,.0f} | shuffled-label control win {[round(x['win'],3) for x in cs]} PF {[round(x['pf'],2) for x in cs]}")
        for sym,gg in real[q].groupby('sym'):
            st=stats(gg); print(f"     {sym}: n={st['n']} win={st['win']:.1%} PF={st['pf']:.2f} net={st['net']:,.0f} days={st['days']}")
        rows.append((side,q,rs))
