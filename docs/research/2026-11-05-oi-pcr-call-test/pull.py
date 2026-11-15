import sys;sys.path.insert(0,'docs/research/2026-10-28-live-stoploss-monitor-analyze-streak')
from db import q
D='docs/research/2026-11-05-oi-pcr-call-test/'
s=q("""SELECT Symbol,SnapshotTime,Ltp,Ema20,Ema50,Ema20_5m,Ema50_5m,Rsi5m,AtrRaw,AtrPct,Pcr,Vwap,PriceChangePct,DayOpen,YesterdayClose
FROM IndicatorSnapshots WITH (NOLOCK) WHERE SnapshotTime>='2026-10-07' ORDER BY Symbol,SnapshotTime""")
s.to_csv(D+'snap.csv',index=False)
c=q("""SELECT Symbol,CandleTime,[Open],[High],[Low],[Close] FROM Candles WITH (NOLOCK) WHERE Symbol IN ('NIFTY_1m','BANKNIFTY_1m','SENSEX_1m') AND CandleTime>='2026-10-06' ORDER BY Symbol,CandleTime""")
c.to_csv(D+'candles.csv',index=False)
# ATM +/-2 strike OI per minute: sum CE and PE (strikes nearest underlying)
o=q("""SELECT Symbol,MinuteTime,StrikePrice,OptionType,OpenInterest,UnderlyingLtp FROM OptionMinuteHistory WITH (NOLOCK) ORDER BY Symbol,MinuteTime""")
o.to_csv(D+'oi.csv',index=False)
print(len(s),len(c),len(o))
