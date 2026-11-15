"""Phase 1 shadow report: SENSEX two-read stop vs the actual stop. READ-ONLY (SELECT only), no src/ change.
Usage: python docs/research/2026-11-11-two-read-shadow/shadow_report.py [--since 2026-10-07] [--out DIR]
For every SENSEX signal whose actual exit was OPTION_SL_HIT:
  first breach  = actual Signals.ExitTime / journal exit price (stop in force = final Signals.OptionSL)
  second reading= first OptionMinuteHistory row with MinuteTime >= next minute boundary after the breach
  Ltp <= stop  -> hypothetical confirmed exit at that Ltp ; Ltp > stop -> hypothetical cancelled, position continues and
  the same two-read rule is applied each later minute until T1 (exit at T1), confirmed stop, or 15:15.
Limits: minute resolution; stop level held fixed after the breach (no later trailing); partials ignored (per-unit points).
Cost 2 pts per round trip, same for actual and hypothetical."""
import json,pyodbc,pandas as pd,numpy as np,warnings,datetime as dt,argparse,os
warnings.filterwarnings('ignore'); pd.set_option('display.width',250); pd.set_option('display.max_columns',40)
ap=argparse.ArgumentParser(); ap.add_argument('--since',default='2026-10-07'); ap.add_argument('--out',default=os.path.dirname(__file__)); a=ap.parse_args()
cs=json.load(open('src/Trading.Worker/appsettings.Production.json',encoding='utf-8-sig'))['ConnectionStrings']['DefaultConnection']
k={x.lower().replace(' ','').replace('_',''):y for x,y in (p.split('=',1) for p in cs.split(';') if '=' in p)}
cn=pyodbc.connect(f"DRIVER={{ODBC Driver 17 for SQL Server}};SERVER={k['server']};DATABASE={k['database']};TrustServerCertificate=yes;UID={k['userid']};PWD={k['password']};",timeout=120)
COST=2.0
sg=pd.read_sql("SELECT Id,SignalType,OptionSymbol,CreatedOn,ExitTime,OptionLtp,OptionSL,OptionT1 FROM Signals WITH (NOLOCK) WHERE Symbol='SENSEX' AND ExitReason='OPTION_SL_HIT' AND CreatedOn>=? AND ExitTime IS NOT NULL AND OptionSL>0 ORDER BY Id",cn,params=[a.since])
jr=pd.read_sql("SELECT SignalId,SUM(ExitPrice*Quantity)/NULLIF(SUM(Quantity),0) ex,SUM(Quantity) q FROM TradeJournal WITH (NOLOCK) WHERE ExitReason='OPTION_SL_HIT' AND SignalId IN (SELECT Id FROM Signals WHERE Symbol='SENSEX') GROUP BY SignalId",cn).set_index('SignalId')
omh=pd.read_sql("SELECT TradingSymbol,MinuteTime,Ltp FROM OptionMinuteHistory WITH (NOLOCK) WHERE Ltp>0 AND TradingSymbol IN (SELECT DISTINCT OptionSymbol FROM Signals WHERE Symbol='SENSEX' AND CreatedOn>=?) ORDER BY TradingSymbol,MinuteTime",cn,params=[a.since])
g={s:x.set_index('MinuteTime').Ltp for s,x in omh.groupby('TradingSymbol')}
rows=[]
for _,s in sg.iterrows():
    if s.Id not in jr.index or s.OptionSymbol not in g: continue
    L=g[s.OptionSymbol]; stop=float(s.OptionSL); entry=float(s.OptionLtp); act=float(jr.loc[s.Id,'ex']); q=float(jr.loc[s.Id,'q'])
    b=s.ExitTime; nxt=(b.replace(second=0,microsecond=0)+dt.timedelta(minutes=1))
    after=L[L.index>=nxt]; end=s.CreatedOn.replace(hour=15,minute=15,second=0,microsecond=0)
    after=after[after.index<=end]
    if len(after)==0: continue
    r1t,r1=after.index[0],float(after.iloc[0]); r=dict(Id=s.Id,Side=s.SignalType,Entry=entry,Stop=stop,BreachTime=b,ActualExit=act,Qty=q,
        Read2Time=r1t,Read2=r1)
    if r1<=stop: r.update(Verdict='CONFIRMED',HypExit=r1,HypTime=r1t)
    else:
        r['Verdict']='CANCELLED'; run=0; hx=None
        for t,v in after.iloc[1:].items():
            if v>=float(s.OptionT1): hx,ht=float(s.OptionT1),t; break
            run=run+1 if v<=stop else 0
            if run>=2: hx,ht=float(v),t; break
        if hx is None: hx,ht=float(after.iloc[-1]),after.index[-1]
        r.update(HypExit=hx,HypTime=ht)
    r['ActualPts']=act-entry-COST; r['HypPts']=r['HypExit']-entry-COST; r['DiffPts']=r['HypPts']-r['ActualPts']; r['DiffRs']=(r['HypExit']-act)*q
    r['Better']='improved' if r['DiffPts']>0.005 else ('worsened' if r['DiffPts']<-0.005 else 'same')
    rows.append(r)
d=pd.DataFrame(rows); os.makedirs(a.out,exist_ok=True); d.to_csv(os.path.join(a.out,'shadow_trades.csv'),index=False)
print('SENSEX actual stops with data:',len(d),'| range',d.BreachTime.min(),d.BreachTime.max())
def st(x,col):
    w=x[col][x[col]>0].sum(); l=-x[col][x[col]<0].sum(); eq=x[col].cumsum(); dd=(eq.cummax()-eq).max()
    return dict(n=len(x),net_pts=round(x[col].sum(),1),exp=round(x[col].mean(),2),PF=round(w/l,2) if l else np.nan,maxDD=round(dd,1))
def block(name,x):
    print('\n==',name); print(pd.DataFrame({'actual':st(x,'ActualPts'),'two_read':st(x,'HypPts')}).T)
    print('verdicts',x.Verdict.value_counts().to_dict(),'| improved',(x.Better=='improved').sum(),'worsened',(x.Better=='worsened').sum(),'| stops delayed (confirmed later/worse)',((x.Verdict=='CONFIRMED')&(x.Better=='worsened')).sum(),'| cancelled then lost more',((x.Verdict=='CANCELLED')&(x.Better=='worsened')).sum(),'| Rs diff',round(x.DiffRs.sum()))
block('ALL SENSEX stops',d)
for s in ('CALL','PUT'): block(s,d[d.Side==s])
m=len(d)//2; block('first half',d.iloc[:m]); block('second half',d.iloc[m:])
