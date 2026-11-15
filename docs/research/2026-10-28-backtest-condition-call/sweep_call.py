"""CALL condition sweep on the 2026-10-26 same-row snapshot/outcome export (candle-derived outcomes,
no IndicatorSnapshotOutcomes join). Gap from Candles daily open / previous close (daily-open-close.csv).
Non-overlapping: within a cell, per symbol, a new entry only after the previous one exited.
Chronological day split: train 60% / validation 20% / holdout 20%.
    python docs/research/2026-10-28-backtest-condition-call/sweep_call.py
"""
import itertools, pandas as pd, numpy as np
F = "docs/research/2026-10-28-backtest-condition-call/"
d = pd.read_csv("docs/research/2026-10-26-backtest-condition-both/snapshots-outcomes.csv")
d = d[d.Dir == "CALL"].copy()
d["t"] = pd.to_datetime(d.MinuteIst); d["day"] = d.t.dt.date

dc = pd.read_csv(F + "daily-open-close.csv"); dc["Symbol"] = dc.Symbol.str.replace("_1m", "")
dc["d"] = pd.to_datetime(dc.d).dt.date; dc = dc.sort_values(["Symbol", "d"])
dc["PrevClose"] = dc.groupby("Symbol").DayClose.shift(); dc["PrevD"] = dc.groupby("Symbol").d.shift()
dc["GapPct"] = (dc.DayOpen - dc.PrevClose) / dc.PrevClose * 100
dc.loc[(pd.to_datetime(dc.d) - pd.to_datetime(dc.PrevD)).dt.days > 4, "GapPct"] = np.nan   # missing prior session
d = d.merge(dc[["Symbol", "d", "GapPct"]], left_on=["Symbol", "day"], right_on=["Symbol", "d"], how="left")
d["Gap"] = np.select([d.GapPct >= .3, d.GapPct <= -.3, d.GapPct.notna()], ["GapUp", "GapDown", "Normal"], "Missing")
r = d.Rsi5m
d["RsiB"] = np.select([r.isna(), r < 28, r < 38, r < 45, r < 55, r < 65], ["Missing", "A<28", "B28-38", "C38-45", "D45-55", "E55-65"], "F>=65")
a = d.AtrPct
d["AtrB"] = np.select([a.isna(), a < .03, a < .04, a < .05], ["Missing", "Low", "MedLow", "MedHigh"], "High")
bull = (d.Ltp > d.Ema20) & (d.Ema20 > d.Ema50) & (d.Ema20_5m > d.Ema50_5m)
bear = (d.Ltp < d.Ema20) & (d.Ema20 < d.Ema50) & (d.Ema20_5m < d.Ema50_5m)
miss = d[["Ltp", "Ema20", "Ema50", "Ema20_5m", "Ema50_5m"]].isna().any(axis=1)
d["Ema"] = np.select([miss, bear, bull], ["Missing", "Opposed(bear)", "Aligned(bull)"], "Mixed")
d["Hour"] = d.t.dt.hour.clip(9, 14).astype(str)

days = sorted(d.day.unique()); n = len(days)
cut1, cut2 = days[int(n * .6)], days[int(n * .8)]
d["Split"] = np.where(d.day < cut1, "train", np.where(d.day < cut2, "val", "hold"))
d = d.sort_values(["Symbol", "t"]).reset_index(drop=True)
d.to_csv(F + "call-rows.csv", index=False)

def nonoverlap(g):
    keep, free = [], {}
    for i, s, t, m in zip(g.index, g.Symbol, g.t, g.MinsToExit.fillna(45)):
        if s in free and t < free[s]: continue
        keep.append(i); free[s] = t + pd.Timedelta(minutes=max(m, 1))
    return g.loc[keep]

def stats(g):
    g = g[(g.Result != "NA") & g.PnlRsEst.notna()]
    if len(g) == 0: return dict(N=0)
    p = g.PnlRsEst; w = p[p > 0].sum(); l = -p[p < 0].sum()
    cum = p.cumsum(); dd = (cum.cummax() - cum).max()
    byday = g.groupby("day").PnlRsEst.sum()
    return dict(N=len(g), Days=g.day.nunique(), TgtFirst=int((g.Result == "WIN").sum()), StopFirst=int((g.Result == "LOSS").sum()),
                Timeout=int(g.Result.str.startswith("TIMEOUT").sum()), TgtPct=round((g.Result == "WIN").mean() * 100, 1),
                Net=round(p.sum()), PF=round(w / l, 2) if l else np.inf, Exp=round(p.mean(), 1), MaxDD=round(dd),
                ExpR=round(g.OutR.mean(), 3), TopDayShare=round(byday.max() / p.sum(), 2) if p.sum() > 0 else np.nan,
                AvgRsi5m=round(g.Rsi5m.mean(), 1), AvgAtr=round(g.AtrPct.mean(), 4))

def evalcell(mask, label):
    g = nonoverlap(d[mask]); out = {"Cell": label}
    for sp in ["train", "val", "hold"]:
        s = stats(g[g.Split == sp]); out.update({f"{sp}_{k}": v for k, v in s.items()})
    s = stats(g); out.update({f"all_{k}": v for k, v in s.items()})
    return out

live = np.where(d.Gap == "GapUp", (d.Rsi5m >= 45) & (d.Rsi5m <= 85), (d.Rsi5m >= 45) & (d.Rsi5m <= 70))
rows = [evalcell(pd.Series(True, index=d.index), "BASE(no filter)"),
        evalcell(pd.Series(live, index=d.index), "LIVE 45-70 (GapUp 45-85)"),
        evalcell((d.Rsi5m >= 45) & (d.Rsi5m <= 70), "LIVE RSI5m 45-70")]
dims = {"Gap": d.Gap, "RsiB": d.RsiB, "AtrB": d.AtrB, "Ema": d.Ema, "Symbol": d.Symbol, "Hour": d.Hour}
for k, col in dims.items():
    for v in sorted(col.unique()): rows.append(evalcell(col == v, f"{k}={v}"))
for (k1, c1), (k2, c2) in itertools.combinations([("Gap", d.Gap), ("RsiB", d.RsiB), ("AtrB", d.AtrB), ("Ema", d.Ema)], 2):
    for v1, v2 in itertools.product(sorted(c1.unique()), sorted(c2.unique())):
        m = (c1 == v1) & (c2 == v2)
        if m.sum(): rows.append(evalcell(m, f"{k1}={v1} & {k2}={v2}"))
for vs in itertools.product(*[sorted(dims[k].unique()) for k in ["Gap", "RsiB", "AtrB", "Ema"]]):
    m = (d.Gap == vs[0]) & (d.RsiB == vs[1]) & (d.AtrB == vs[2]) & (d.Ema == vs[3])
    if m.sum() >= 30: rows.append(evalcell(m, "4way " + " & ".join(vs)))
# finer RSI thresholds: ceilings/floors chosen on TRAIN only
for lo in [0, 35, 40, 45, 50, 55, 60, 65]:
    for hi in [45, 50, 55, 60, 65, 70, 75, 80, 85, 101]:
        if hi > lo + 4: rows.append(evalcell((d.Rsi5m >= lo) & (d.Rsi5m < hi), f"RSI5m [{lo},{hi})"))
res = pd.DataFrame(rows); res.to_csv(F + "cells.csv", index=False)
print("days", n, "train <", cut1, "val <", cut2, "hold to", days[-1])
