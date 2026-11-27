"""Combination grid from IndicatorSnapshots + IndicatorSnapshotOutcomes (historical simulation only).

    python .claude/skills/backtest-condition/combo_grid.py <out-folder> [--dir CALL|PUT|BOTH] [--window 15]
           [--max-k 3] [--features Rsi5m,Delta,Pcr,Atr,Ema1m,Ema5m,Gap,Trend] [--from 2026-09-30] [--min-train 30]

What it does
  1. Reads every outcome row joined to its snapshot, keeping ONLY rows that pass the alignment audit
     (same Symbol AND |Snapshot.Ltp - Outcome.EntryPrice| < 0.01). Rows before --from are excluded
     (the id space was reset before 2026-09-30; older joins are garbage). Audit counts are printed.
  2. Dedupes to one row per (Symbol, minute, Direction).
  3. Buckets each feature (values known at the snapshot minute only):
       Rsi5m  <28 | 28-38 | 38-45 | 45-55 | 55-65 | >=65 | Missing
       Delta  |BlockedDelta| <0.3 | 0.3-0.4 | 0.4-0.5 | 0.5-0.6 | >=0.6 | Missing (only stored when a signal was blocked)
       Pcr    <0.8 | 0.8-1.0 | 1.0-1.2 | >=1.2 | Missing
       Atr    AtrPct <0.03 | 0.03-0.04 | 0.04-0.05 | >=0.05
       Ema1m  Ema20>Ema50 | Ema20<=Ema50          Ema5m  same on the 5-minute EMAs
       Gap    (DayOpen-YesterdayClose)/YesterdayClose: GapUp >=+0.3% | GapDown <=-0.3% | Normal
       Trend  snapshot TrendDirection
  4. Every combination of 1..--max-k features x every value tuple is one grid cell. Inside a cell, per
     symbol, a new entry starts only after the previous one resolved (SecondsToFirstHit, or the full
     window when neither barrier was hit), so overlapping minutes are not counted many times.
  5. Outcome = FirstHitResult<window>Min with the stored fixed index barriers (TargetPoints/StopLossPoints):
     TARGET = +TargetPoints, SL = -StopLossPoints, NONE = 0 (reported separately, not a loss).
     Rs per lot = index points x --delta x lot size - 2 x --cost-per-order - 2 x --slip-pts x lot size.
  6. Chronological day split 60/20/20 -> train / val / hold. Pass = train N >= --min-train, val Net > 0,
     hold N >= 30, hold Net > 0, hold PF > 1.1. Ranked by hold Net, then hold expectancy.

Writes <out>/grid.csv (every cell) and <out>/grid-top.md (baseline + top passing/non-passing cells per direction).
Base rate only: no live gates, no option premium path, no live-trade data.
"""
import argparse, itertools, json, os, sys, warnings

import numpy as np
import pandas as pd

warnings.filterwarnings("ignore")
LOT = {"NIFTY": 65, "BANKNIFTY": 30, "SENSEX": 20}


def connect():
    import pyodbc
    cs = json.load(open("src/Trading.Worker/appsettings.Production.json", encoding="utf-8-sig"))["ConnectionStrings"]["DefaultConnection"]
    k = {a.lower().replace(" ", "").replace("_", ""): b for a, b in (p.split("=", 1) for p in cs.split(";") if "=" in p)}
    return pyodbc.connect(
        f"DRIVER={{ODBC Driver 17 for SQL Server}};SERVER={k['server']};DATABASE={k['database']};"
        f"TrustServerCertificate=yes;UID={k['userid']};PWD={k['password']};", timeout=120)


def load(a):
    cn = connect()
    w = a.window
    audit = pd.read_sql(f"""SELECT COUNT(*) n,
        SUM(CASE WHEN s.Id IS NULL THEN 1 ELSE 0 END) orphan,
        SUM(CASE WHEN s.Symbol=o.Symbol AND ABS(s.Ltp-o.EntryPrice)<0.01 THEN 1 ELSE 0 END) aligned
        FROM IndicatorSnapshotOutcomes o LEFT JOIN IndicatorSnapshots s ON s.Id=o.IndicatorSnapshotId
        WHERE s.SnapshotTime>='{a.date_from}' OR (s.Id IS NULL AND o.CreatedOn>='{a.date_from}')""", cn)
    print("alignment audit:", audit.iloc[0].to_dict())
    d = pd.read_sql(f"""SELECT s.Symbol, s.SnapshotTime, s.Ltp, s.DayOpen, s.YesterdayClose, s.Ema20, s.Ema50, s.Ema20_5m, s.Ema50_5m,
        s.Rsi5m, s.AtrPct, s.Pcr, s.BlockedDelta, s.TrendDirection, o.Direction, o.TargetPoints, o.StopLossPoints,
        o.FirstHitResult{w}Min AS Res, o.SecondsToFirstHit, o.MaxFavorableMove{w} AS Mfe, o.MaxAdverseMove{w} AS Mae
        FROM IndicatorSnapshotOutcomes o JOIN IndicatorSnapshots s ON s.Id=o.IndicatorSnapshotId
        WHERE s.SnapshotTime>='{a.date_from}' AND s.Symbol=o.Symbol AND ABS(s.Ltp-o.EntryPrice)<0.01""", cn)
    d["t"] = pd.to_datetime(d.SnapshotTime).dt.floor("min")
    n0 = len(d)
    d = d.sort_values("SnapshotTime").drop_duplicates(["Symbol", "t", "Direction"])
    print(f"rows aligned {n0}, after one-per-symbol-minute-direction {len(d)}, unresolved (NULL result) {d.Res.isna().sum()}")
    return d[d.Res.notna()].copy()


def features(d):
    r = d.Rsi5m
    d["Rsi5m"] = np.select([r.isna(), r < 28, r < 38, r < 45, r < 55, r < 65], ["Missing", "<28", "28-38", "38-45", "45-55", "55-65"], ">=65")
    x = d.BlockedDelta.abs().where(d.BlockedDelta.fillna(0) != 0)
    d["Delta"] = np.select([x.isna(), x < .3, x < .4, x < .5, x < .6], ["Missing", "<0.3", "0.3-0.4", "0.4-0.5", "0.5-0.6"], ">=0.6")
    p = d.Pcr.where(d.Pcr.fillna(0) != 0)
    d["Pcr"] = np.select([p.isna(), p < .8, p < 1, p < 1.2], ["Missing", "<0.8", "0.8-1.0", "1.0-1.2"], ">=1.2")
    at = d.AtrPct
    d["Atr"] = np.select([at.isna(), at < .03, at < .04, at < .05], ["Missing", "<0.03", "0.03-0.04", "0.04-0.05"], ">=0.05")
    d["Ema1m"] = np.where(d.Ema20 > d.Ema50, "Ema20>Ema50", "Ema20<=Ema50")
    d["Ema5m"] = np.where(d.Ema20_5m > d.Ema50_5m, "Ema20_5m>Ema50_5m", "Ema20_5m<=Ema50_5m")
    g = (d.DayOpen - d.YesterdayClose) / d.YesterdayClose * 100
    d["Gap"] = np.select([g.isna() | (d.YesterdayClose <= 0), g >= .3, g <= -.3], ["Missing", "GapUp", "GapDown"], "Normal")
    d["Trend"] = d.TrendDirection.fillna("Missing")
    return d


def outcome(d, a):
    pts = np.select([d.Res == "TARGET", d.Res == "SL"], [d.TargetPoints, -d.StopLossPoints], 0.0)
    lot = d.Symbol.map(LOT).fillna(1)
    d["IdxPts"] = pts
    d["NetRs"] = pts * a.delta * lot - 2 * a.cost_per_order - 2 * a.slip_pts * lot
    held = d.SecondsToFirstHit.where(d.Res != "NONE").fillna(a.window * 60).clip(lower=60)
    d["FreeAt"] = d.t + pd.to_timedelta(held, unit="s")
    return d


def nonoverlap(sym, t, free):
    keep = np.zeros(len(t), bool); nxt = {}
    for i in range(len(t)):
        if sym[i] in nxt and t[i] < nxt[sym[i]]: continue
        keep[i] = True; nxt[sym[i]] = free[i]
    return keep


def stats(g):
    if len(g) == 0: return dict(N=0)
    p = g.NetRs; w, l = p[p > 0].sum(), -p[p < 0].sum(); cum = p.cumsum()
    res = g.Res.value_counts()
    resolved = res.get("TARGET", 0) + res.get("SL", 0)
    return dict(N=len(g), Days=g.day.nunique(), Target=int(res.get("TARGET", 0)), SL=int(res.get("SL", 0)), NoHit=int(res.get("NONE", 0)),
                TgtPct=round(res.get("TARGET", 0) / resolved * 100, 1) if resolved else np.nan,
                AvgMfe=round(g.Mfe.mean(), 1), AvgMae=round(g.Mae.mean(), 1),
                Net=round(p.sum()), PF=round(w / l, 2) if l else np.inf, Exp=round(p.mean(), 1), MaxDD=round((cum.cummax() - cum).max()))


def cell(sub, label, direction):
    sub = sub.sort_values(["Symbol", "t"])
    g = sub[nonoverlap(sub.Symbol.values, sub.t.values, sub.FreeAt.values)]
    out = {"Dir": direction, "Cell": label}
    for sp in ["train", "val", "hold", "all"]:
        s = stats(g if sp == "all" else g[g.Split == sp]); out.update({f"{sp}_{k}": v for k, v in s.items()})
    return out


def md(df):
    """Markdown table without the optional tabulate dependency."""
    if df.empty: return "_none_"
    head = "| " + " | ".join(df.columns) + " |\n|" + "---|" * len(df.columns) + "\n"
    return head + "\n".join("| " + " | ".join("" if pd.isna(v) else str(v) for v in r) + " |" for r in df.itertuples(index=False))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("out"); ap.add_argument("--dir", default="BOTH"); ap.add_argument("--window", type=int, default=15, choices=[5, 10, 15])
    ap.add_argument("--max-k", type=int, default=3); ap.add_argument("--features", default="Rsi5m,Delta,Pcr,Atr,Ema1m,Ema5m,Gap,Trend")
    ap.add_argument("--from", dest="date_from", default="2026-09-30"); ap.add_argument("--min-train", type=int, default=30)
    ap.add_argument("--delta", type=float, default=0.5); ap.add_argument("--cost-per-order", type=float, default=20.0)
    ap.add_argument("--slip-pts", type=float, default=1.0)
    a = ap.parse_args(); os.makedirs(a.out, exist_ok=True)
    feats = a.features.split(",")
    d = outcome(features(load(a)), a)
    d["day"] = d.t.dt.date
    days = sorted(d.day.unique()); n = len(days)
    c1, c2 = days[int(n * .6)], days[int(n * .8)]
    d["Split"] = np.where(d.day < c1, "train", np.where(d.day < c2, "val", "hold"))
    print(f"{n} days {days[0]}..{days[-1]} | train <{c1} | val <{c2} | hold to {days[-1]}")
    print("feature missing share:", {f: round((d[f] == "Missing").mean(), 3) for f in feats})

    rows = []
    for direction in (["CALL", "PUT"] if a.dir == "BOTH" else [a.dir]):
        dd = d[d.Direction == direction]
        rows.append(cell(dd, "BASE (no filter)", direction))
        for k in range(1, a.max_k + 1):
            for combo in itertools.combinations(feats, k):
                for vals, sub in dd.groupby(list(combo)):
                    vals = vals if isinstance(vals, tuple) else (vals,)
                    if (sub.Split == "train").sum() < a.min_train: continue
                    rows.append(cell(sub, " & ".join(f"{f}={v}" for f, v in zip(combo, vals)), direction))
    g = pd.DataFrame(rows)
    g["Pass"] = (g.train_N >= a.min_train) & (g.val_Net > 0) & (g.hold_N >= 30) & (g.hold_Net > 0) & (g.hold_PF > 1.1)
    g = g.sort_values(["Dir", "Pass", "hold_Net", "hold_Exp"], ascending=[True, False, False, False])
    g.to_csv(os.path.join(a.out, "grid.csv"), index=False)

    cols = ["Cell", "train_N", "train_Net", "train_PF", "val_N", "val_Net", "val_PF", "hold_N", "hold_Days", "hold_Net",
            "hold_PF", "hold_Exp", "all_TgtPct", "all_NoHit", "all_Net", "all_PF", "all_MaxDD"]
    with open(os.path.join(a.out, "grid-top.md"), "w", encoding="utf-8") as f:
        f.write(f"# Combination grid ({days[0]}..{days[-1]}, {n} days, {a.window}-min window)\n\n"
                f"Base rate from aligned IndicatorSnapshotOutcomes; Rs per lot after costs. Simulated only.\n\n")
        for direction, gd in g.groupby("Dir"):
            f.write(f"## {direction}: {len(gd)} cells, {int(gd.Pass.sum())} pass\n\n")
            f.write(gd[gd.Cell.str.startswith("BASE")][cols].pipe(md) + "\n\n")
            top = gd[gd.Pass].head(20)
            f.write("### Passing cells (ranked by holdout net)\n\n" + (top[cols].pipe(md) if len(top) else
                    "**No historically validated edge found under the tested assumptions.**") + "\n\n")
            f.write("### Best non-passing by total net (for context, NOT recommendations)\n\n"
                    + gd[~gd.Pass].sort_values("all_Net", ascending=False).head(10)[cols].pipe(md) + "\n\n")
    for direction, gd in g.groupby("Dir"):
        print(f"\n{direction}: {len(gd)} cells, {int(gd.Pass.sum())} pass")
        print(gd.head(10)[["Cell", "train_N", "val_Net", "hold_N", "hold_Net", "hold_PF", "all_Net", "all_PF", "Pass"]].to_string(index=False))


if __name__ == "__main__":
    main()
