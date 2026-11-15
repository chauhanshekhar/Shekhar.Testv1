"""Scan MarketTicks for one-tick index spikes (tick differs from BOTH neighbours by > 0.15% in the same direction)
and check whether the spike value equals the previous session close (a feed fallback)."""
import sys; sys.path.insert(0, "docs/research/2026-10-28-live-stoploss-monitor-analyze-streak"); from db import q
import pandas as pd
t = q("SELECT Symbol,TickTime,LastPrice FROM MarketTicks WITH (NOLOCK) WHERE Symbol IN ('NIFTY','BANKNIFTY','SENSEX') ORDER BY Symbol,TickTime")
yc = q("SELECT Symbol,CAST(SnapshotTime AS date) d,MAX(YesterdayClose) yc FROM IndicatorSnapshots GROUP BY Symbol,CAST(SnapshotTime AS date)")
t["d"] = pd.to_datetime(t.TickTime).dt.date; yc["d"] = pd.to_datetime(yc.d).dt.date
t = t.merge(yc, on=["Symbol", "d"], how="left")
g = t.groupby(["Symbol", "d"]).LastPrice
t["prev"], t["next"] = g.shift(), g.shift(-1)
a, b = (t.LastPrice - t.prev) / t.prev, (t.LastPrice - t.next) / t.next
sp = t[((a > .0015) & (b > .0015)) | ((a < -.0015) & (b < -.0015))].copy()
sp["EqYestClose"] = (sp.LastPrice - sp.yc).abs() < 0.01
print("tick days", t.d.nunique(), t.d.min(), t.d.max(), "ticks", len(t))
print(sp.groupby(["Symbol", "EqYestClose"]).size())
sp.to_csv("docs/research/2026-10-28-live-stoploss-monitor-analyze-streak/tick-spikes.csv", index=False)
print(sp[["Symbol", "TickTime", "prev", "LastPrice", "next", "yc"]].tail(15).to_string())
