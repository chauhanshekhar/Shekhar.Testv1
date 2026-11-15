# Live vs backtest gap-day classification (read-only). Run from the repo root.
# Live (EarlyTrendGate.GapPct): (DayOpen - YesterdayClose) / YesterdayClose, from the snapshot at gate time (feed values).
# Backtest (MarketSeriesBuilder.OpenGapPct): (first 1m bar open of the session - last 1m close before it) / that close.
import sys; sys.path.insert(0, 'docs/research/2026-10-28-live-stoploss-monitor-analyze-streak')
from db import q
import pandas as pd, numpy as np
D = 'docs/research/2026-11-11-early-trend-gapdays/'
s = q("""SELECT Symbol, SnapshotTime, DayOpen, YesterdayClose FROM IndicatorSnapshots WITH (NOLOCK)
         WHERE SnapshotTime >= '2026-10-07' AND CAST(SnapshotTime AS time) BETWEEN '09:15' AND '11:00'
           AND DayOpen > 0 AND YesterdayClose > 0 ORDER BY Symbol, SnapshotTime""")
c = q("""SELECT Symbol, CandleTime, [Open], [Close] FROM Candles WITH (NOLOCK)
         WHERE Symbol IN ('NIFTY_1m','BANKNIFTY_1m','SENSEX_1m') AND CandleTime >= '2026-10-05' ORDER BY Symbol, CandleTime""")
s['date'] = pd.to_datetime(s.SnapshotTime).dt.date
c['Symbol'] = c.Symbol.str.replace('_1m', ''); c['t'] = pd.to_datetime(c.CandleTime) + pd.Timedelta(hours=5, minutes=30); c['date'] = c.t.dt.date
rows = []
for (sym, d), g in s.groupby(['Symbol', 'date']):
    g = g.copy(); g['mins'] = pd.to_datetime(g.SnapshotTime).dt.hour * 60 + pd.to_datetime(g.SnapshotTime).dt.minute
    at = g.iloc[(g.mins - 630).abs().argmin()]                       # snapshot nearest 10:30 = when the soft rule first applies
    cc = c[c.Symbol == sym]; day = cc[cc.date == d]; prev = cc[cc.date < d]
    if day.empty or prev.empty: continue
    bt = (day.iloc[0].Open - prev.iloc[-1].Close) / prev.iloc[-1].Close * 100
    live = (at.DayOpen - at.YesterdayClose) / at.YesterdayClose * 100
    live_first = (g.iloc[0].DayOpen - g.iloc[0].YesterdayClose) / g.iloc[0].YesterdayClose * 100
    cls = lambda x: 'GapUp' if x >= 0.30 else ('GapDown' if x <= -0.30 else 'Normal')
    rows.append(dict(Symbol=sym, date=d, live_1030=round(live, 3), live_firstsnap=round(live_first, 3), backtest=round(bt, 3),
                     diff=round(live - bt, 3), live_cls=cls(live), bt_cls=cls(bt),
                     dayopen_min=g.DayOpen.min(), dayopen_max=g.DayOpen.max(), yclose_min=g.YesterdayClose.min(), yclose_max=g.YesterdayClose.max()))
r = pd.DataFrame(rows); r.to_csv(D + 'gap_parity.csv', index=False)
pd.set_option('display.width', 220)
print('sessions compared:', len(r), '| classification disagree:', int((r.live_cls != r.bt_cls).sum()),
      '| abs diff > 0.05pp:', int((r['diff'].abs() > 0.05).sum()), '| max abs diff:', r['diff'].abs().max())
print('DayOpen drifts during 09:15-11:00 on', int((r.dayopen_max != r.dayopen_min).sum()), 'sessions; YesterdayClose varies on', int((r.yclose_max != r.yclose_min).sum()))
print(r[(r.live_cls != r.bt_cls) | (r['diff'].abs() > 0.05) | (r.backtest.abs().between(0.25, 0.35))].to_string(index=False))
