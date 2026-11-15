"""SENSEX index-noise ENTRY gate test. READ-ONLY. Features from SENSEX_1m candles completed before entry (UTC->IST +5:30, no look-ahead).
 chop  = sum(1m high-low over last 15 closed bars) / (max high - min low)   (high = back-and-forth)
 rng_s = mean 1m range (last 10 closed bars) / index stop distance |EntryPrice-StopLoss|  (high = stop inside the noise)
Outcome = TradeJournal.ProfitLoss summed per signal (real, gross of costs). Terciles + halves; stop rate = OPTION_SL/PROTECTED exits with P&L<0."""
import json, pyodbc, pandas as pd, numpy as np, warnings, datetime as dt
warnings.filterwarnings('ignore'); pd.set_option('display.width',250); pd.set_option('display.max_columns',40)
cs = json.load(open('src/Trading.Worker/appsettings.Production.json', encoding='utf-8-sig'))['ConnectionStrings']['DefaultConnection']
k = {a.lower().replace(' ','').replace('_',''): b for a,b in (p.split('=',1) for p in cs.split(';') if '=' in p)}
cn = pyodbc.connect(f"DRIVER={{ODBC Driver 17 for SQL Server}};SERVER={k['server']};DATABASE={k['database']};TrustServerCertificate=yes;UID={k['userid']};PWD={k['password']};", timeout=120)
c = pd.read_sql("SELECT CandleTime,High,Low FROM Candles WITH (NOLOCK) WHERE Symbol='SENSEX_1m' ORDER BY CandleTime",cn)
c['t']=c.CandleTime+dt.timedelta(hours=5,minutes=30); c=c.set_index('t'); c[['High','Low']]=c[['High','Low']].astype(float)
s = pd.read_sql("""SELECT s.Id,s.SignalType,s.CreatedOn,s.EntryPrice,s.StopLoss,SUM(j.ProfitLoss) pnl FROM Signals s WITH (NOLOCK)
 JOIN TradeJournal j WITH (NOLOCK) ON j.SignalId=s.Id WHERE s.Symbol='SENSEX' GROUP BY s.Id,s.SignalType,s.CreatedOn,s.EntryPrice,s.StopLoss ORDER BY s.Id""",cn)
rows=[]
for _,r in s.iterrows():
    t0=r.CreatedOn.replace(second=0,microsecond=0)   # candle stamped t is open time -> closed bars end <= t0
    p=c[(c.index<t0)&(c.index>=t0-dt.timedelta(minutes=15))]
    if len(p)<12: continue
    rng=(p.High-p.Low); chop=rng.sum()/(p.High.max()-p.Low.min())
    sd=abs(float(r.EntryPrice)-float(r.StopLoss))
    rows.append(dict(Id=r.Id,type=r.SignalType,day=t0.date(),chop=chop,rng_s=rng.tail(10).mean()/sd if sd>0 else np.nan,pnl=float(r.pnl)))
d=pd.DataFrame(rows).dropna(); d['half']=np.where(d.Id<d.Id.median(),'H1','H2')
def st(x):
    w=x.pnl[x.pnl>0].sum(); l=-x.pnl[x.pnl<0].sum()
    return pd.Series(dict(n=len(x),net=x.pnl.sum(),exp=x.pnl.mean(),PF=w/l if l else np.nan,win=(x.pnl>0).mean()))
print('n=',len(d),' baseline'); print(st(d).round(2).to_string())
for f in ('chop','rng_s'):
    print(f'\n== {f} terciles (all / H1 / H2)')
    d['b']=pd.qcut(d[f],3,labels=['low','mid','high'])
    print(d.groupby('b').apply(st).round(2)); print(d.groupby(['half','b']).apply(st).round(2))
    for q in (0.7,0.8,0.9):
        cut=d[d.half=='H1'][f].quantile(q); h2=d[d.half=='H2']
        print(f' H1-fit q{int(q*100)} cut={cut:.2f}: H2 blocked n={(h2[f]>cut).sum()} net={h2[h2[f]>cut].pnl.sum():.0f} | kept n={(h2[f]<=cut).sum()} net={h2[h2[f]<=cut].pnl.sum():.0f}; H1 blocked net={d[(d.half=="H1")&(d[f]>cut)].pnl.sum():.0f}')
print('\nTODAY 11-12:'); print(d[d.day==dt.date(2026,11,12)][['Id','type','chop','rng_s','pnl']].round(2).to_string())
print('\nquantiles'); print(d[['chop','rng_s']].describe(percentiles=[.5,.8,.9]).round(2))
d.to_csv('docs/research/2026-11-12-sensex-noise-gate/signals.csv',index=False)
