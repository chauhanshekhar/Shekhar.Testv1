import pandas as pd,numpy as np,sys
pd.set_option('display.width',250)
d=pd.read_csv(sys.argv[1]+'/sig.csv',parse_dates=['CreatedOn','ExitTime']); d=d[d.Status=='CLOSED'].copy()
c=pd.read_csv('docs/research/2026-10-26-winrate-60-search/candles-1m.csv',parse_dates=['CandleTime']); c['t']=c.CandleTime+pd.Timedelta(hours=5,minutes=30)
C={k[:-3]:g.set_index('t').sort_index() for k,g in c.groupby('Symbol')}
D={}
for s,g in C.items():
    g=g.copy(); g['day']=g.index.date
    dd=g.groupby('day').agg(h=('High','max'),l=('Low','min'),cl=('Close','last'),op=('Open','first'))
    dd['pdh']=dd.h.shift(); dd['pdl']=dd.l.shift(); dd['pcl']=dd.cl.shift(); D[s]=dd
def sim(r,maxmin=45):
    g=C[r.Symbol]; pe=r.OptionType=='PE'
    R=abs(r.EntryPrice-r.StopLoss); T=abs(r.Target1-r.EntryPrice)
    if not (R>0 and T>0): return None
    w=g[g.index>r.CreatedOn.floor('min')].iloc[:maxmin]
    if len(w)<3: return None
    E=r.EntryPrice
    for _,b in w.iterrows():
        if pe: sl=b.High>=E+R; tp=b.Low<=E-T
        else: sl=b.Low<=E-R; tp=b.High>=E+T
        if sl: return -1.0
        if tp: return T/R
    return (E-w.Close.iloc[-1])/R if pe else (w.Close.iloc[-1]-E)/R
rows=[]
for r in d.itertuples():
    pe=r.OptionType=='PE'; sg=-1 if pe else 1; t=r.CreatedOn.floor('min'); day=t.date()
    R=sim(r)
    if R is None: continue
    g=C[r.Symbol]; past=g[(g.index<t)&(g.index>=t-pd.Timedelta(minutes=15))]
    if len(past)<10: continue
    x=dict(Id=r.Id,Symbol=r.Symbol,pe=pe,t=t,day=day,R=R,stop=r.ExitReason in('OPTION_SL_HIT','INDEX_SL_HIT'))
    # cross-index agreement: other two indices' 15m and 30m move in signal direction
    ag=[];ag30=[]
    for s2,g2 in C.items():
        if s2==r.Symbol: continue
        p=g2[(g2.index<t)&(g2.index>=t-pd.Timedelta(minutes=30))]
        if len(p)<20: ag.append(np.nan);ag30.append(np.nan);continue
        p15=p[p.index>=t-pd.Timedelta(minutes=15)]
        ag.append(sg*(p15.Close.iloc[-1]-p15.Close.iloc[0])>0); ag30.append(sg*(p.Close.iloc[-1]-p.Close.iloc[0])>0)
    x['xagree15']=np.nansum(ag) if not all(pd.isna(ag)) else np.nan
    x['xagree30']=np.nansum(ag30) if not all(pd.isna(ag30)) else np.nan
    dd=D[r.Symbol].loc[day]; E=r.EntryPrice
    atr=abs(r.EntryPrice-r.StopLoss)/1.5
    # prior-day levels (direction relative, in ATR units; positive = beyond the level in trade direction)
    x['vs_pdlevel']=(sg*(E-(dd.pdl if pe else dd.pdh)))/atr if not pd.isna(dd.pdl) else np.nan
    x['gap_dir']=sg*(dd.op-dd.pcl)/dd.pcl*100 if not pd.isna(dd.pcl) else np.nan
    x['from_open']=sg*(E-dd.op)/atr
    x['mins']=(t.hour*60+t.minute)-555
    rows.append(x)
X=pd.DataFrame(rows); print(len(X)); X.to_csv(sys.argv[1]+'/feat.csv',index=False)
mid=X.t.median()
def tab(col,bins=None,q=None):
    b=pd.cut(X[col],bins) if bins is not None else X[col]
    g=X.groupby(b).apply(lambda g:pd.Series(dict(n=len(g),stop=round(g.stop.mean(),2),expR=round(g.R.mean(),3),sumR=round(g.R.sum(),1),H1=round(g[g.t<mid].R.mean(),2),H2=round(g[g.t>=mid].R.mean(),2),days=g.day.nunique())))
    print('\n',col);print(g.to_string())
print('ALL expR',X.R.mean().round(3),'stop',X.stop.mean().round(2))
tab('xagree15'); tab('xagree30'); tab('vs_pdlevel',[-99,-1,0,1,2,99]); tab('from_open',[-99,-2,-1,0,1,2,99]); tab('gap_dir',[-9,-.3,0,.3,9]); tab('mins',[-1,30,60,120,200,400])
