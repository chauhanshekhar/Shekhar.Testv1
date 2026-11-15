"""Long-window chop entry gate backtest. READ-ONLY. No look-ahead: only 1m bars closed before the signal minute, SAME IST SESSION DAY only
(entries without W same-day bars are 'no data' = pass, fail-open like the live gate). Outcome = real TradeJournal P&L per signal.
chop_W = sum(High-Low over last W bars)/(max High - min Low). Costed = gross - Rs40 - qty*1pt (Rs20/order + 1pt round trip, memory estimate).
Cutoff protocol (to avoid fitting on test data): cutoff = quantile q of that symbol's chop in H1 (first half of signals by Id), applied to
H1 (in-sample) and H2 (out-of-sample). Grid = windows x quantiles x symbols is searched -> treat positives with suspicion. Run from repo root."""
import json, pyodbc, pandas as pd, numpy as np, warnings, datetime as dt
warnings.filterwarnings('ignore'); pd.set_option('display.width',250); pd.set_option('display.max_columns',40)
cs = json.load(open('src/Trading.Worker/appsettings.Production.json', encoding='utf-8-sig'))['ConnectionStrings']['DefaultConnection']
k = {a.lower().replace(' ','').replace('_',''): b for a,b in (p.split('=',1) for p in cs.split(';') if '=' in p)}
cn = pyodbc.connect(f"DRIVER={{ODBC Driver 17 for SQL Server}};SERVER={k['server']};DATABASE={k['database']};TrustServerCertificate=yes;UID={k['userid']};PWD={k['password']};", timeout=120)
WS=(15,30,45,60,90); QS=(0.7,0.8,0.9)
out=[]
for sym in ('NIFTY','BANKNIFTY','SENSEX'):
    c=pd.read_sql(f"SELECT CandleTime,High,Low FROM Candles WITH (NOLOCK) WHERE Symbol='{sym}_1m' ORDER BY CandleTime",cn)
    c['t']=c.CandleTime+dt.timedelta(hours=5,minutes=30); c=c.set_index('t')[['High','Low']].astype(float)
    s=pd.read_sql(f"""SELECT s.Id,s.SignalType,s.StrategyType,s.CreatedOn,SUM(j.ProfitLoss) pnl,SUM(j.Quantity) qty FROM Signals s WITH (NOLOCK)
      JOIN TradeJournal j WITH (NOLOCK) ON j.SignalId=s.Id WHERE s.Symbol='{sym}' GROUP BY s.Id,s.SignalType,s.StrategyType,s.CreatedOn ORDER BY s.Id""",cn)
    for _,r in s.iterrows():
        if r.StrategyType=='HeroZero': continue
        t0=r.CreatedOn.replace(second=0,microsecond=0)
        row=dict(sym=sym,Id=r.Id,type=r.SignalType,day=t0.date(),pnl=float(r.pnl or 0),cost=40+float(r.qty or 0)*1.0)
        day0=dt.datetime.combine(t0.date(),dt.time(0,0))
        for W in WS:
            p=c[(c.index<t0)&(c.index>=max(t0-dt.timedelta(minutes=W),day0))]
            sp=p.High.max()-p.Low.min() if len(p) else 0
            row[f'c{W}']=(p.High-p.Low).sum()/sp if len(p)>=W-1 and sp>0 else np.nan   # allow one missing minute
        out.append(row)
d=pd.DataFrame(out); d['net']=d.pnl-d.cost
d['half']=d.groupby('sym').Id.transform(lambda x: np.where(x<x.median(),'H1','H2'))
d.to_csv('docs/research/2026-11-13-long-window-chop/signals.csv',index=False)
print('trades',len(d)); print(d.groupby('sym').agg(n=('pnl','size'),gross=('pnl','sum'),costed=('net','sum')).round(0))
print('usable per window:',{W:int(d[f'c{W}'].notna().sum()) for W in WS})
res=[]
for sym in ['NIFTY','BANKNIFTY','SENSEX','ALL']:
    x=d if sym=='ALL' else d[d.sym==sym]
    for W in WS:
        f=f'c{W}'
        for q in QS:
            if sym=='ALL':   # per-symbol H1 cutoffs, pooled result
                parts=[]
                for sy,g in x.groupby('sym'):
                    cut=g[(g.half=='H1')][f].quantile(q); g=g.assign(blk=g[f]>cut); parts.append(g)
                y=pd.concat(parts)
            else:
                cut=x[x.half=='H1'][f].quantile(q); y=x.assign(blk=x[f]>cut)
            r=dict(sym=sym,W=W,q=q)
            for h in ('H1','H2'):
                z=y[y.half==h]; b=z[z.blk]
                r[f'{h}_nblk']=len(b); r[f'{h}_blk_gross']=round(b.pnl.sum()); r[f'{h}_blk_costed']=round(b.net.sum())
            r['days_blocked_pos_H2']=int((y[(y.half=="H2")&y.blk].groupby('day').pnl.sum()>0).sum()); r['days_blocked_H2']=int(y[(y.half=="H2")&y.blk].day.nunique())
            res.append(r)
R=pd.DataFrame(res)
R['gate_gain_H1']=-R.H1_blk_costed; R['gate_gain_H2']=-R.H2_blk_costed   # gain = losses avoided (blocked net negative -> positive gain)
R['both_halves']=(R.gate_gain_H1>0)&(R.gate_gain_H2>0)
print('\n=== gate gain (costed): + means blocking those trades would have helped. both_halves = positive in H1 AND H2 ===')
print(R[['sym','W','q','H1_nblk','gate_gain_H1','H2_nblk','gate_gain_H2','days_blocked_H2','days_blocked_pos_H2','both_halves']].to_string(index=False))
print('\ncells with both_halves & n_blocked(H2)>=8:'); print(R[R.both_halves&(R.H2_nblk>=8)][['sym','W','q','H1_nblk','gate_gain_H1','H2_nblk','gate_gain_H2']].to_string(index=False))
# today BANKNIFTY check
t=d[(d.sym=='BANKNIFTY')&(d.day==dt.date(2026,11,13))]
print('\nTODAY BANKNIFTY chop by window (13 trades):'); print(t[['Id','type','pnl']+[f'c{W}' for W in WS]].round(2).to_string(index=False))
