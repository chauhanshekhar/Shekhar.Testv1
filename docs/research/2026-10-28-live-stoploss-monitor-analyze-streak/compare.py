"""Stop-loss losses vs winners, all closed signals in closed-signals.csv (last 14 days, paper).
Entry features from the latest IndicatorSnapshot at or before signal CreatedOn (same symbol)."""
import sys; sys.path.insert(0, "docs/research/2026-10-28-live-stoploss-monitor-analyze-streak"); from db import q
import pandas as pd, numpy as np
F = "docs/research/2026-10-28-live-stoploss-monitor-analyze-streak/"
s = pd.read_csv(F + "closed-signals.csv", parse_dates=["CreatedOn", "ExitTime"]).sort_values("CreatedOn")
sn = q(f"""SELECT Symbol,SnapshotTime,Ltp,Ema20,Ema50,Ema20_5m,Ema50_5m,Rsi,Rsi5m,AtrRaw FROM IndicatorSnapshots
 WHERE SnapshotTime>='{s.CreatedOn.min():%Y-%m-%d}' ORDER BY SnapshotTime""")
s = pd.merge_asof(s, sn.sort_values("SnapshotTime"), left_on="CreatedOn", right_on="SnapshotTime", by="Symbol", direction="backward")
s["SnapAge"] = (s.CreatedOn - s.SnapshotTime).dt.total_seconds()
sg = np.where(s.OptionType == "CE", 1, -1)
s["Ema1mWith"] = sg * (s.Ema20 - s.Ema50) > 0
s["Ema5mWith"] = sg * (s.Ema20_5m - s.Ema50_5m) > 0
s["DistEma20ATR"] = sg * (s.Ltp - s.Ema20) / s.AtrRaw
s["StopATR"] = (s.IdxEntry - s.IdxSL).abs() / s.AtrRaw
s["Day"] = s.CreatedOn.dt.date
prev = s.groupby(["Day", "Symbol", "OptionType"])
s["MinsSincePrev"] = (s.CreatedOn - prev.ExitTime.shift()).dt.total_seconds() / 60
s["PrevWasSL"] = prev.SLloss.shift()
s["ReentryLevelATR"] = sg * (s.IdxEntry - prev.IdxEntry.shift()) / s.AtrRaw
s["Grp"] = np.where(s.SLloss, "SL-loss", np.where(s.Pnl > 0, "Win", "Other"))
s.to_csv(F + "trades-features.csv", index=False)
print("max snapshot age s", s.SnapAge.max(), "| groups", s.Grp.value_counts().to_dict(), s.Day.nunique(), "days")
def show(name, col):
    t = s.groupby(["Grp", col]).size().unstack(0).fillna(0).astype(int); print(f"\n{name}\n", t.to_string())
for c in ["OptionType", "Symbol", "MarketState", "Ema1mWith", "Ema5mWith"]: show(c, c)
s["ReentrySameLvl"] = s.ReentryLevelATR.abs() < 1
s["Within20mOfPrev"] = s.MinsSincePrev < 20
for c in ["ReentrySameLvl", "Within20mOfPrev", "PrevWasSL"]: show(c, c)
num = ["Rsi5m", "Rsi", "AtrPctAtEntry", "DistEma20ATR", "StopATR", "MinsSincePrev"]
print("\nmedians\n", s.groupby("Grp")[num].median().round(2).to_string())
print("\nby dir medians\n", s.groupby(["OptionType", "Grp"])[["Rsi5m", "DistEma20ATR", "AtrPctAtEntry"]].median().round(2).to_string())
s["HeldMin"] = (s.ExitTime - s.CreatedOn).dt.total_seconds() / 60
print("\nSL-loss minutes to stop: median", round(s[s.SLloss].HeldMin.median(), 1), "| <=2 min:", (s[s.SLloss].HeldMin <= 2).sum(), "of", s.SLloss.sum())
print("SL-loss option MFE % (median):", round(((s.MfePrice / s.OptionLtp - 1) * 100)[s.SLloss].median(), 1), "| MFE<3%:", (((s.MfePrice / s.OptionLtp - 1) * 100)[s.SLloss] < 3).sum())
print("Daily: ", s.groupby("Day").agg(n=("Id", "size"), sl=("SLloss", "sum"), pnl=("Pnl", "sum")).to_string())
