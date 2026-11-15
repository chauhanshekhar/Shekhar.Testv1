# Read-only pull for the OI-gate replay (2026-11-11). Run from the repo root.
import sys; sys.path.insert(0, 'docs/research/2026-10-28-live-stoploss-monitor-analyze-streak')
from db import q
D = 'docs/research/2026-11-11-oi-gate-test/'
o = q("""SELECT Symbol,MinuteTime,StrikePrice,OptionType,OpenInterest,UnderlyingLtp
         FROM OptionMinuteHistory WITH (NOLOCK) WHERE MinuteTime>='2026-10-06' ORDER BY Symbol,MinuteTime""")
o.to_csv(D + 'oi.csv', index=False)
c = q("""SELECT Symbol,CandleTime,[Close] FROM Candles WITH (NOLOCK)
         WHERE Symbol IN ('NIFTY_1m','BANKNIFTY_1m','SENSEX_1m') AND CandleTime>='2026-10-06' ORDER BY Symbol,CandleTime""")
c.to_csv(D + 'candles.csv', index=False)
print(len(o), len(c))
