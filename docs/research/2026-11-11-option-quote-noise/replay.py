"""Replay of option-quote-noise fixes on real signals, minute-resolution premium path (OptionMinuteHistory).
READ-ONLY. Simplified bracket per signal: stop = OptionSL, take-profit = OptionT1 (full exit), force exit 15:15.
Pass criteria (fixed BEFORE running): candidate must beat V0 on net points after cost in BOTH chronological halves,
profit factor >= V0, and per-trade expectancy >= V0 (so gain is not just fewer trades). Cost = 2 pts/round trip."""
import json,pyodbc,pandas as pd,numpy as np,warnings,datetime as dt,sys
warnings.filterwarnings('ignore'); pd.set_option('display.width',250); pd.set_option('display.max_columns',40)
cs=json.load(open('src/Trading.Worker/appsettings.Production.json',encoding='utf-8-sig'))['ConnectionStrings']['DefaultConnection']
k={a.lower().replace(' ','').replace('_',''):b for a,b in (p.split('=',1) for p in cs.split(';') if '=' in p)}
cn=pyodbc.connect(f"DRIVER={{ODBC Driver 17 for SQL Server}};SERVER={k['server']};DATABASE={k['database']};TrustServerCertificate=yes;UID={k['userid']};PWD={k['password']};",timeout=120)
COST=2.0
omh=pd.read_sql("SELECT TradingSymbol,MinuteTime,Ltp,Delta,UnderlyingLtp FROM OptionMinuteHistory WITH (NOLOCK) WHERE Ltp>0 ORDER BY TradingSymbol,MinuteTime",cn)
print('OMH range',omh.MinuteTime.min(),omh.MinuteTime.max(),len(omh))
sg=pd.read_sql("SELECT Id,Symbol,SignalType,OptionSymbol,CreatedOn,OptionLtp,OptionSL,OptionT1,ExitReason,StopLoss,EntryPrice,DeltaAtEntry FROM Signals WITH (NOLOCK) WHERE CreatedOn>=? AND OptionLtp>0 AND OptionSL>0 AND OptionT1>0 AND OptionSymbol IS NOT NULL ORDER BY Id",cn,params=[omh.MinuteTime.min()])
# OptionSL is overwritten to entry after breakeven/partial, so the ORIGINAL stop distance is estimated from the intact
# index stop: d = |EntryPrice-StopLoss| * |DeltaAtEntry|. Unmodified rows (OptionSL < entry) keep their true distance.
sg['d_true']=sg.OptionLtp-sg.OptionSL; sg['d_est']=(sg.EntryPrice-sg.StopLoss).abs()*sg.DeltaAtEntry.abs()
um=sg[sg.d_true>0.5]; print('d_est vs true on unmodified rows: n',len(um),'median ratio',(um.d_est/um.d_true).median().round(3),'p10/p90',(um.d_est/um.d_true).quantile([.1,.9]).round(2).tolist())
sg['d']=np.where(sg.d_true>0.5,sg.d_true,sg.d_est); sg=sg[sg.d>0.5].copy()
grp={s:g.set_index('MinuteTime') for s,g in omh.groupby('TradingSymbol')}
def path(sym,t0,t1):
    g=grp.get(sym); 
    return None if g is None else g.loc[(g.index>=t0)&(g.index<=t1)]
def run(sig,var):
    t0=sig.CreatedOn.replace(second=0,microsecond=0); day_end=t0.replace(hour=15,minute=15)
    p=path(sig.OptionSymbol,t0-dt.timedelta(minutes=3),day_end)
    if p is None or t0 not in p.index: return None
    L0=float(p.loc[t0,'Ltp']); d=float(sig.d); tp=sig.OptionT1-sig.OptionLtp
    U0=p.loc[t0,'UnderlyingLtp']; D0=abs(p.loc[t0,'Delta'] or 0.5)
    prior=p[(p.index<=t0)].Ltp.tail(3)
    after=p[p.index>t0]
    entry=L0; sl=L0-d; t1=L0+tp
    if var=='V1_limit_median':
        r=float(prior.median()); 
        if r>=L0: entry=L0   # fill now at LTP only if LTP not above median (no spike)
        filled=False
        for t,row in after.head(2).iterrows() if r<L0 else []:
            if row.Ltp<=r: entry=r; filled=True; at=t; break
        if r<L0 and not filled: return dict(skip=True)
        if r<L0: after=after[after.index>at]
        sl=entry-d; t1=entry+tp
    stop_run=0
    for t,row in after.iterrows():
        L=float(row.Ltp)
        if L>=t1: return dict(pts=t1-entry-COST,R=(t1-entry-COST)/d,why='T1',skip=False,mins=(t-t0).seconds/60)
        hit=L<=sl
        if var=='V2_two_reads':
            stop_run=stop_run+1 if hit else 0
            if stop_run>=2: return dict(pts=L-entry-COST,R=(L-entry-COST)/d,why='SL',skip=False,mins=(t-t0).seconds/60)
            continue
        if var=='V3_index_confirm' and hit:
            adverse=(row.UnderlyingLtp-U0) if sig.SignalType=='PUT' else (U0-row.UnderlyingLtp)
            hit= adverse*D0>=0.5*d
        if hit: return dict(pts=L-entry-COST,R=(L-entry-COST)/d,why='SL',skip=False,mins=(t-t0).seconds/60)
    if len(after)==0: return None
    L=float(after.iloc[-1].Ltp); return dict(pts=L-entry-COST,R=(L-entry-COST)/d,why='EOD',skip=False,mins=(after.index[-1]-t0).seconds/60)
VARS=['V0_baseline','V1_limit_median','V2_two_reads','V3_index_confirm']
rows=[]
for _,s in sg.iterrows():
    for v in VARS:
        r=run(s,v)
        if r is None: break
        rows.append(dict(Id=s.Id,Symbol=s.Symbol,Type=s.SignalType,var=v,actual=s.ExitReason,**r))
df=pd.DataFrame(rows)
ids=df.Id.unique(); print('signals replayed',len(ids),'of',len(sg))
df.to_csv('docs/research/2026-11-11-option-quote-noise/trades.csv',index=False)
b=df[df['var']=='V0_baseline']
b=b.assign(act_stop=b.actual.str.contains('SL'))
print('\nVALIDATION baseline replay vs actual (is-stop):'); print(pd.crosstab(b.act_stop,b.why))
def stats(g):
    t=g[~g.skip]; w=t.pts[t.pts>0].sum(); l=-t.pts[t.pts<0].sum()
    return pd.Series(dict(n=len(t),skipped=int(g.skip.sum()),stop_rate=(t.why=='SL').mean(),net_pts=t.pts.sum(),exp_pts=t.pts.mean(),expR=t.R.mean(),PF=w/l if l else np.nan))
for name,sub in (('ALL',df),('SENSEX',df[df.Symbol=='SENSEX']),('SENSEX PUT',df[(df.Symbol=='SENSEX')&(df.Type=='PUT')]),('BANKNIFTY+NIFTY (control)',df[df.Symbol!='SENSEX'])):
    print('\n==',name); print(sub.groupby('var').apply(stats).round(3))
    ids=sorted(sub.Id.unique()); mid=ids[len(ids)//2]
    for h,hs in (('first half',sub[sub.Id<mid]),('second half',sub[sub.Id>=mid])):
        print(' ',h,'ids',hs.Id.min(),'-',hs.Id.max()); print(hs.groupby('var').apply(stats).round(3)[['n','skipped','stop_rate','net_pts','exp_pts','PF']])
