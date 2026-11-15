"""Chop gate (live rule: 15 closed 1m bars, block if chop > 4.5) on NIFTY and BANKNIFTY real trades. READ-ONLY, no look-ahead
(bars closed before the signal minute; candle CandleTime is UTC open time, +5:30 -> IST). Outcome = TradeJournal.ProfitLoss per signal, gross.
Reports: baseline vs kept vs blocked, halves, per-day blocked, plus the SENSEX 4.5 share for scale. Run from repo root."""
import json, pyodbc, pandas as pd, numpy as np, warnings, datetime as dt
warnings.filterwarnings('ignore'); pd.set_option('display.width',250); pd.set_option('display.max_columns',40)
cs = json.load(open('src/Trading.Worker/appsettings.Production.json', encoding='utf-8-sig'))['ConnectionStrings']['DefaultConnection']
k = {a.lower().replace(' ','').replace('_',''): b for a,b in (p.split('=',1) for p in cs.split(';') if '=' in p)}
cn = pyodbc.connect(f"DRIVER={{ODBC Driver 17 for SQL Server}};SERVER={k['server']};DATABASE={k['database']};TrustServerCertificate=yes;UID={k['userid']};PWD={k['password']};", timeout=120)
CUT=4.5
def st(x):
    w=x.pnl[x.pnl>0].sum(); l=-x.pnl[x.pnl<0].sum()
    return pd.Series(dict(n=len(x),net=x.pnl.sum(),exp=x.pnl.mean() if len(x) else np.nan,PF=w/l if l else np.nan,win=(x.pnl>0).mean() if len(x) else np.nan))
allrows=[]
for sym in ('NIFTY','BANKNIFTY','SENSEX'):
    c = pd.read_sql(f"SELECT CandleTime,High,Low FROM Candles WITH (NOLOCK) WHERE Symbol='{sym}_1m' ORDER BY CandleTime",cn)
    c['t']=c.CandleTime+dt.timedelta(hours=5,minutes=30); c=c.set_index('t'); c[['High','Low']]=c[['High','Low']].astype(float)
    s = pd.read_sql(f"""SELECT s.Id,s.SignalType,s.StrategyType,s.CreatedOn,SUM(j.ProfitLoss) pnl FROM Signals s WITH (NOLOCK)
     JOIN TradeJournal j WITH (NOLOCK) ON j.SignalId=s.Id WHERE s.Symbol='{sym}' GROUP BY s.Id,s.SignalType,s.StrategyType,s.CreatedOn ORDER BY s.Id""",cn)
    rows=[]
    for _,r in s.iterrows():
        t0=r.CreatedOn.replace(second=0,microsecond=0)
        p=c[(c.index<t0)&(c.index>=t0-dt.timedelta(minutes=15))]
        if len(p)<15: continue            # live gate fails open under 15 bars
        span=p.High.max()-p.Low.min()
        if span<=0: continue
        rows.append(dict(sym=sym,Id=r.Id,type=r.SignalType,strat=r.StrategyType,day=t0.date(),chop=(p.High-p.Low).sum()/span,pnl=float(r.pnl or 0)))
    d=pd.DataFrame(rows)
    print(f'\n######## {sym}: candles={len(c)} signals_with_journal={len(s)} usable={len(d)}')
    if d.empty: continue
    d=d[d.strat!='HeroZero'] if (d.strat=='HeroZero').any() else d   # live gate exempts HeroZero
    d['half']=np.where(d.Id<d.Id.median(),'H1','H2'); d['blk']=d.chop>CUT
    print('chop quantiles:',d.chop.quantile([.1,.5,.8,.9,.95]).round(2).to_dict(),' share blocked=%.1f%%'%(100*d.blk.mean()))
    print('baseline'); print(st(d).round(2).to_frame().T.to_string())
    print('by decision (all)'); print(d.groupby('blk').apply(st).round(2))
    print('by decision x half'); print(d.groupby(['half','blk']).apply(st).round(2))
    print('by type x decision'); print(d.groupby(['type','blk']).apply(st).round(2))
    b=d[d.blk]; print('blocked trades per day (n, net):'); print(b.groupby('day').pnl.agg(['count','sum']).round(0).to_string())
    print('days with >=1 blocked:',b.day.nunique(),'of',d.day.nunique())
    allrows.append(d)
pd.concat(allrows).to_csv('docs/research/2026-11-13-chop-gate-nifty-banknifty/signals.csv',index=False)
