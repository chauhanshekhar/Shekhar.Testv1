# What the GATE actually sees: market.DayOpen = latest MarketTick.Open (fallback: first 1m candle open); market.YesterdayClose = candle-derived.
# Compare that live gap with the backtest gap (first 1m open vs prior close) at ~10:30 IST. MarketTicks are kept ~7 days, so only recent sessions exist.
import sys; sys.path.insert(0, 'docs/research/2026-10-28-live-stoploss-monitor-analyze-streak')
from db import q
import pandas as pd
D = 'docs/research/2026-11-11-early-trend-gapdays/'
t = q("""SELECT Symbol, TickTime, [Open] AS TickOpen, [Close] AS TickClose FROM (
           SELECT Symbol, TickTime, [Open], [Close],
                  ROW_NUMBER() OVER (PARTITION BY Symbol, CAST(TickTime AS date) ORDER BY TickTime DESC) rn
             FROM MarketTicks WITH (NOLOCK)
            WHERE Symbol IN ('NIFTY','BANKNIFTY','SENSEX') AND CAST(TickTime AS time) BETWEEN '10:25' AND '10:31') x WHERE rn = 1""")
s = q("""SELECT Symbol, SnapshotTime, DayOpen, YesterdayClose FROM IndicatorSnapshots WITH (NOLOCK)
         WHERE SnapshotTime >= '2026-10-25' AND CAST(SnapshotTime AS time) BETWEEN '10:25' AND '10:35' ORDER BY Symbol, SnapshotTime""")
t['date'] = pd.to_datetime(t.TickTime).dt.date; s['date'] = pd.to_datetime(s.SnapshotTime).dt.date
s = s.groupby(['Symbol', 'date']).last().reset_index()
m = t.merge(s, on=['Symbol', 'date'])
m['gate_gap'] = (m.TickOpen - m.YesterdayClose) / m.YesterdayClose * 100        # EarlyTrendGate.GapPct(market.DayOpen, market.YesterdayClose)
m['bt_gap'] = (m.DayOpen - m.YesterdayClose) / m.YesterdayClose * 100           # audit DayOpen = first 1m candle open (backtest definition)
cls = lambda x: 'GapUp' if x >= 0.30 else ('GapDown' if x <= -0.30 else 'Normal')
m['gate_cls'] = m.gate_gap.map(cls); m['bt_cls'] = m.bt_gap.map(cls)
m['open_diff_pts'] = (m.TickOpen - m.DayOpen).round(2)
out = m[['Symbol', 'date', 'TickOpen', 'DayOpen', 'open_diff_pts', 'gate_gap', 'bt_gap', 'gate_cls', 'bt_cls']].round(3)
out.to_csv(D + 'gap_parity_ticks.csv', index=False)
pd.set_option('display.width', 200)
print('symbol-sessions with ticks:', len(out), '| classification disagree:', int((out.gate_cls != out.bt_cls).sum()),
      '| tick open != candle open:', int((out.open_diff_pts.abs() > 0.5).sum()))
print(out.to_string(index=False))
