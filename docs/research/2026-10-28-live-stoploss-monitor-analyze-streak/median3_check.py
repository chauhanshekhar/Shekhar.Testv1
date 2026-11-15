"""Check the proposed exit-side index price = median of the last 3 ticks (per symbol, same day):
how many one-tick spikes survive it, and how much it lags real moves."""
import sys; sys.path.insert(0, "docs/research/2026-10-28-live-stoploss-monitor-analyze-streak"); from db import q
import pandas as pd
t = q("SELECT Symbol,TickTime,LastPrice FROM MarketTicks WITH (NOLOCK) WHERE Symbol IN ('NIFTY','BANKNIFTY','SENSEX') ORDER BY Symbol,TickTime")
t["d"] = pd.to_datetime(t.TickTime).dt.date
t["med3"] = t.groupby(["Symbol", "d"]).LastPrice.transform(lambda s: s.rolling(3, min_periods=1).median())
sp = pd.read_csv("docs/research/2026-10-28-live-stoploss-monitor-analyze-streak/tick-spikes.csv", parse_dates=["TickTime"])
m = t.merge(sp[["Symbol", "TickTime"]], on=["Symbol", "TickTime"])
m["dev"] = ((m.med3 - m.LastPrice) / m.LastPrice).abs()
g = t.groupby(["Symbol", "d"])
t["prevMed"] = g.med3.shift()
t["medJump"] = ((t.med3 - t.prevMed) / t.prevMed).abs()
print("spikes", len(sp), "| med3 at spike within 0.05% of neighbours (filtered):",
      int((((m.med3 - m.LastPrice).abs() / m.LastPrice) > 0.001).sum()), "of", len(m))
print("med3 one-step jumps > 0.15% (a spike leaking through):", int((t.medJump > 0.0015).sum()))
t["err"] = (t.med3 - t.LastPrice).abs() / t.LastPrice * 100
print("median |med3 - raw| %:", round(t.err.median(), 4), "p99:", round(t.err.quantile(.99), 4), "ticks", len(t))
j = t[t.medJump > 0.0015].copy()
nxt = g.med3.shift(-5); j["med3_5later"] = nxt.loc[j.index]
j["reverts"] = ((j.med3_5later - j.prevMed).abs() / j.prevMed) < 0.0007
j["gap_s"] = (pd.to_datetime(j.TickTime) - pd.to_datetime(g.TickTime.shift().loc[j.index])).dt.total_seconds()
print("leaks that revert within 5 ticks (bad):", int(j.reverts.sum()), "| persistent moves (real):", int((~j.reverts).sum()),
      "| leaks after a >5 s feed gap:", int((j.gap_s > 5).sum()))
print(j[j.reverts][["Symbol", "TickTime", "prevMed", "med3", "LastPrice", "gap_s"]].to_string())
