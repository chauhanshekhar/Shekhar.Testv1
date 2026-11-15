"""Sideways-market entry filters, REAL journal trades (HeroZero excluded), costed (Rs40 + qty x 1pt). READ-ONLY. No look-ahead: closed 1m bars before the
signal minute, same IST session only; entries lacking history pass (fail-open).
F1 range_vs_stop: span of last W closed bars (W=30,60) / index stop distance |EntryPrice-StopLoss|. BLOCK when ratio is LOW (index barely moved vs the stop).
   Cutoff = H1 quantile (10/20/30%) of that symbol's ratio; applied to H1 (in-sample) and H2 (out-of-sample).
F2 no_new_extreme: minutes since the session LOW (PUT) / HIGH (CALL) as of entry. BLOCK when >= N minutes (N=15,30,45,60). Fixed N, nothing fitted.
gain = losses avoided = -(costed net of blocked trades). Positive in BOTH halves required. Opening type from first bar open vs prior close (+-0.25%)."""
import json, pyodbc, pandas as pd, numpy as np, warnings, datetime as dt
warnings.filterwarnings('ignore'); pd.set_option('display.width',250); pd.set_option('display.max_columns',40)
cs = json.load(open('src/Trading.Worker/appsettings.Production.json', encoding='utf-8-sig'))['ConnectionStrings']['DefaultConnection']
k = {a.lower().replace(' ','').replace('_',''): b for a,b in (p.split('=',1) for p in cs.split(';') if '=' in p)}
cn = pyodbc.connect(f"DRIVER={{ODBC Driver 17 for SQL Server}};SERVER={k['server']};DATABASE={k['database']};TrustServerCertificate=yes;UID={k['userid']};PWD={k['password']};", timeout=120)
Ns=(15,30,45,60); Ws=(30,60); QS=(0.1,0.2,0.3)
out=[]
for sym in ('NIFTY','BANKNIFTY','SENSEX'):
    c=pd.read_sql(f"SELECT CandleTime,High,Low,[Open],[Close] FROM Candles WITH (NOLOCK) WHERE Symbol='{sym}_1m' ORDER BY CandleTime",cn)
    c['t']=c.CandleTime+dt.timedelta(hours=5,minutes=30); c=c.set_index('t')[['Open','High','Low','Close']].astype(float); c['day']=c.index.date
    first=c.groupby('day').Open.first(); lastc=c.groupby('day').Close.last(); days=list(first.index)
    gap={d:(first[d]/lastc[days[i-1]]-1) if i>0 else np.nan for i,d in enumerate(days)}
    s=pd.read_sql(f"""SELECT s.Id,s.SignalType,s.StrategyType,s.CreatedOn,s.EntryPrice,s.StopLoss,SUM(j.ProfitLoss) pnl,SUM(j.Quantity) qty FROM Signals s WITH (NOLOCK)
      JOIN TradeJournal j WITH (NOLOCK) ON j.SignalId=s.Id WHERE s.Symbol='{sym}' GROUP BY s.Id,s.SignalType,s.StrategyType,s.CreatedOn,s.EntryPrice,s.StopLoss ORDER BY s.Id""",cn)
    for _,r in s.iterrows():
        if r.StrategyType=='HeroZero': continue
        t0=r.CreatedOn.replace(second=0,microsecond=0); dd=t0.date()
        g=gap.get(dd,np.nan); op='GapDown' if g<-0.0025 else 'GapUp' if g>0.0025 else 'Normal' if g==g else 'NA'
        sess=c[(c.day==dd)&(c.index<t0)]
        row=dict(sym=sym,Id=r.Id,type=r.SignalType,day=dd,open=op,pnl=float(r.pnl or 0),cost=40+float(r.qty or 0))
        sd=abs(float(r.EntryPrice)-float(r.StopLoss))
        for W in Ws:
            p=sess[sess.index>=t0-dt.timedelta(minutes=W)]
            row[f'r{W}']=(p.High.max()-p.Low.min())/sd if len(p)>=W-1 and sd>0 else np.nan
        if len(sess)>=5:
            idx=sess.Low.idxmin() if r.SignalType=='PUT' else sess.High.idxmax()
            row['since']=(t0-idx).total_seconds()/60
        else: row['since']=np.nan
        out.append(row)
d=pd.DataFrame(out); d['net']=d.pnl-d.cost
d['half']=d.groupby('sym').Id.transform(lambda x: np.where(x<x.median(),'H1','H2'))
d.to_csv('docs/research/2026-11-13-sideways-range-and-extreme/signals.csv',index=False)
print('trades',len(d),' usable r30/r60/since:',int(d.r30.notna().sum()),int(d.r60.notna().sum()),int(d.since.notna().sum()))
print(d.groupby('open').agg(n=('pnl','size'),costed=('net','sum')).round(0).T.to_string())
def cell(y,label):
    r=dict(rule=label)
    for h in ('H1','H2'):
        z=y[(y.half==h)]; b=z[z.blk]; r[f'{h}_n']=len(b); r[f'{h}_gain']=round(-b.net.sum())
    b2=y[(y.half=='H2')&y.blk]; r['H2_days']=b2.day.nunique(); r['H2_days_helped']=int((b2.groupby('day').net.sum()<0).sum())
    r['both']=r['H1_gain']>0 and r['H2_gain']>0; return r
res=[]
for sym in ['NIFTY','BANKNIFTY','SENSEX','ALL']:
    x=d if sym=='ALL' else d[d.sym==sym]
    for N in Ns:
        y=x.assign(blk=x.since>=N); res.append(dict(sym=sym,**cell(y,f'F2 since-extreme>={N}m')))
    for W in Ws:
        f=f'r{W}'
        for q in QS:
            parts=[]
            for sy,g in x.groupby('sym'):
                cut=g[g.half=='H1'][f].quantile(q); parts.append(g.assign(blk=g[f]<cut))
            res.append(dict(sym=sym,**cell(pd.concat(parts),f'F1 range{W}/stop<H1q{int(q*100)}')))
R=pd.DataFrame(res); print('\n(gain = losses avoided, costed; + helps)'); print(R.to_string(index=False))
print('\nBoth-halves cells with >=8 blocked in H2:'); print(R[R.both&(R.H2_n>=8)].to_string(index=False))
print('\nF2 pooled by opening type (blocked n / gain):')
for N in Ns:
    y=d[d.since>=N]; print(f'N={N}',y.groupby('open').agg(n=('net','size'),gain=('net',lambda v:-v.sum())).round(0).T.to_dict())
print('\nF2 by trade type (pooled, N=30):'); y=d[d.since>=30]; print(y.groupby(['sym','type']).agg(n=('net','size'),gain=('net',lambda v:-v.sum())).round(0).to_string())
t=d[(d.sym=='BANKNIFTY')&(d.day==dt.date(2026,11,13))]; print('\nTODAY BANKNIFTY'); print(t[['Id','type','pnl','since','r30','r60']].round(2).to_string(index=False))
print('quantiles r60 pooled:',d.r60.quantile([.1,.2,.3,.5]).round(2).to_dict(),' since pooled:',d.since.quantile([.5,.7,.9]).round(0).to_dict())
