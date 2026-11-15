# HeroZero expiry study (read-only). Usage: python study.py <outdir>
import sys; sys.path.insert(0,'docs/research/2026-10-28-live-stoploss-monitor-analyze-streak')
from db import q
import pandas as pd; pd.set_option('display.width',250); pd.set_option('display.max_rows',200)
out=sys.argv[1]
# 1) real HeroZero trades, P&L from journal, cost Rs20/order x2 + 1pt slippage x qty
j=q("""SELECT s.Id,s.Symbol,s.OptionType,s.CreatedOn,s.OptionLtp,s.Confidence,s.ExitReason,t.ProfitLoss,t.Quantity
FROM Signals s LEFT JOIN TradeJournal t ON t.SignalId=s.Id WHERE s.SelectedStrategy='HeroZero' ORDER BY s.Id""")
j['net']=j.ProfitLoss-40-j.Quantity.fillna(0)
print(j.to_string()); print('REAL: n',len(j),'wins',(j.net>0).sum(),'net',round(j.net.sum()))
j.to_csv(f'{out}/real-trades.csv',index=False)
# 2) threshold test after 14:00: first minute a CE/PE scores >= T, that option's real premium path to 15:15
h=q("""SELECT CAST(CreatedOn AS date) d,Symbol,TradingSymbol,OptionType,CreatedOn,TotalScore FROM HeroZeroScores WITH(NOLOCK)
WHERE CreatedOn>='2026-10-07' AND TotalScore>=55""")
rows=[]
for T in [60,63,65,70,75]:
  f=h[h.TotalScore>=T].sort_values('CreatedOn').groupby(['d','Symbol']).head(1)
  for r in f.itertuples():
    p=q(f"""SELECT MinuteTime,Ltp FROM OptionMinuteHistory WITH(NOLOCK) WHERE TradingSymbol='{r.TradingSymbol}'
      AND MinuteTime>='{r.CreatedOn:%Y-%m-%d %H:%M}' AND MinuteTime<'{r.d} 15:16' ORDER BY MinuteTime""")
    if len(p)<2: rows.append(dict(T=T,d=r.d,sym=r.Symbol,opt=r.TradingSymbol,t=r.CreatedOn,note='no premium history')); continue
    e=p.Ltp.iloc[0]; px=p.Ltp
    hitsl=(px<=e*0.7).idxmax() if (px<=e*0.7).any() else None; hit2=(px>=e*2).idxmax() if (px>=e*2).any() else None
    first='2x' if hit2 is not None and (hitsl is None or hit2<hitsl) else ('-30%' if hitsl is not None else 'neither')
    rows.append(dict(T=T,d=r.d,sym=r.Symbol,opt=r.TradingSymbol,t=r.CreatedOn,entry=e,max=px.max(),min=px.min(),close=px.iloc[-1],
      first=first,maxR=round(px.max()/e,2),closeR=round(px.iloc[-1]/e,2)))
x=pd.DataFrame(rows); x.to_csv(f'{out}/threshold-first-cross.csv',index=False); print(x.to_string())
