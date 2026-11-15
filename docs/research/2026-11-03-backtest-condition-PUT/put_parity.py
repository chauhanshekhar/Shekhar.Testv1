"""PUT Stage-1 extras (classification, weekly/day robustness) + Stage-2 execution parity audit.

    python docs/research/2026-11-03-backtest-condition-PUT/put_parity.py

Stage 1 reads grid.csv only (simulated). Stage 2 reads Signals + TradeJournal for comparison only;
actual P&L never feeds back into Stage 1 rankings.

Parity tolerances (fixed before the run):
  match      : same Symbol, PUT outcome row at floor(Signal.CreatedOn, minute) or the minute before
  entry ok   : |Signal.EntryPrice - snapshot Ltp| <= 0.10% of price, else ENTRY_MISMATCH
  HIGH parity: >= 80% matched AND exit-class agreement >= 70% AND |sim - actual| per-lot P&L <= 25% of |actual|
  LOW parity : exit-class agreement < 50% OR sim/actual P&L of opposite sign
"""
import os, sys
import numpy as np, pandas as pd

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "../../.."))
os.chdir(ROOT); sys.path.insert(0, ".claude/skills/backtest-condition")
from combo_grid import connect, load, features, outcome, nonoverlap  # noqa: E402

OUT = "docs/research/2026-11-03-backtest-condition-PUT"
pd.set_option("display.width", 250); pd.set_option("display.max_colwidth", 80)


class A: window = 15; date_from = "2026-09-30"; delta = 0.5; cost_per_order = 20.0; slip_pts = 1.0


# ---------- Stage 1: classification under this skill's criteria ----------
g = pd.read_csv(f"{OUT}/grid.csv"); g = g[g.Dir == "PUT"]
g = g[~g.Cell.str.contains("Delta=Missing")]
def cls(r):
    if r.hold_N < 30 or r.hold_Days < 5 or r.train_N < 100:
        pos = r.hold_Net > 0 and r.val_Net > 0
        return "PROMISING" if pos and r.train_Net > 0 else ("INSUFFICIENT DATA" if r.train_N < 100 and r.hold_N < 30 else "FAILED")
    ok = r.val_Net > 0 and r.hold_Net > 0 and r.hold_PF > 1.2 and r.train_Net > 0
    return "VALIDATED HISTORICALLY" if ok else "FAILED"
g["Class"] = g.apply(cls, axis=1)
print("PUT cells (excl Delta=Missing):", len(g)); print(g.Class.value_counts().to_string())
print("\nPROMISING:"); print(g[g.Class == "PROMISING"][["Cell", "train_N", "train_Net", "val_N", "val_Net", "hold_N", "hold_Days", "hold_Net", "hold_PF"]].to_string(index=False))
big = g[(g.train_N >= 100) & (g.hold_N >= 30) & (g.hold_Days >= 5)].sort_values("hold_Net", ascending=False)
print(f"\nCells meeting size rules (train>=100, hold>=30, hold days>=5): {len(big)}; top 10 by hold net:")
print(big[["Cell", "train_N", "train_Net", "val_N", "val_Net", "hold_N", "hold_Days", "hold_Net", "hold_PF", "hold_Exp", "all_Net", "all_PF", "all_MaxDD", "Class"]].head(10).to_string(index=False))
g.to_csv(f"{OUT}/classified.csv", index=False)

# ---------- Stage 1: per-week / per-day robustness for selected cells ----------
d = outcome(features(load(A)), A); d = d[d.Direction == "PUT"].copy()
d["day"] = d.t.dt.date; d["week"] = d.t.dt.to_period("W-SUN").astype(str)
def trades(cond):
    s = d
    for k, v in cond.items(): s = s[s[k] == v]
    s = s.sort_values(["Symbol", "t"])
    return s[nonoverlap(s.Symbol.values, s.t.values, s.FreeAt.values)]
SEL = {"BASE": {}, "Rsi5m=28-38 (live band)": {"Rsi5m": "28-38"}}
for c in big.Cell.head(3): SEL[c] = dict(p.split("=", 1) for p in c.split(" & "))
rows = []
for name, cond in SEL.items():
    t = trades(cond)
    wk = t.groupby("week").NetRs.agg(["size", "sum"]); dy = t.groupby("day").NetRs.sum()
    tot = dy.sum(); best = dy.max()
    rows.append(dict(Cell=name, N=len(t), Days=t.day.nunique(), Net=round(tot), PosWeeks=f"{(wk['sum'] > 0).sum()}/{len(wk)}",
                     BestDay=round(best), NetExBestDay=round(tot - best), PosDays=f"{(dy > 0).sum()}/{len(dy)}"))
    print(f"\n{name} by week:"); print(wk.round(0).to_string())
rob = pd.DataFrame(rows); print("\nRobustness:"); print(rob.to_string(index=False)); rob.to_csv(f"{OUT}/robustness.csv", index=False)

# ---------- Stage 2: execution parity ----------
cn = connect()
sig = pd.read_sql("""SELECT s.Id, s.Symbol, s.CreatedOn, s.EntryPrice, s.Quantity, s.OptionLtp, s.ExitReason, s.ExitTime,
    j.Pnl, j.ExitPx, j.Legs FROM Signals s
    OUTER APPLY (SELECT SUM(ProfitLoss) Pnl, MAX(ExitPrice) ExitPx, COUNT(*) Legs FROM TradeJournal t WHERE t.SignalId=s.Id) j
    WHERE s.OptionType='PE' AND s.CreatedOn>='2026-09-30'""", cn)
sig["t"] = pd.to_datetime(sig.CreatedOn).dt.floor("min")
lot = {"NIFTY": 65, "BANKNIFTY": 30, "SENSEX": 20}
key = d.set_index(["Symbol", "t"])
recs = []
for r in sig.itertuples():
    m = None
    for off in (0, 1):
        k = (r.Symbol, r.t - pd.Timedelta(minutes=off))
        if k in key.index: m = key.loc[[k]].iloc[0]; break
    rec = dict(SignalId=r.Id, Symbol=r.Symbol, Entry=r.CreatedOn, Lots=r.Quantity, ActExit=r.ExitReason,
               ActPnl=r.Pnl, ActPnlPerLot=(r.Pnl / r.Quantity if pd.notna(r.Pnl) and r.Quantity else np.nan))
    if pd.isna(r.Pnl): rec["Status"] = "DATA_MISSING"
    elif m is None: rec["Status"] = "ACTUAL_ONLY"
    else:
        diff = abs(r.EntryPrice - m.Ltp) / r.EntryPrice * 100
        rec.update(SimEntryIdx=m.Ltp, EntryDiffPct=round(diff, 3), SimRes=m.Res, SimPnlPerLot=m.NetRs,
                   Status="MATCHED" if diff <= 0.10 else "ENTRY_MISMATCH")
    recs.append(rec)
p = pd.DataFrame(recs)
p["ActClass"] = np.select([p.ActExit.str.contains("SL", na=False) & (p.ActPnl < 0), p.ActPnl > 0], ["SL", "WIN"], "OTHER")
p["SimClass"] = p.SimRes.map({"SL": "SL", "TARGET": "WIN", "NONE": "OTHER"})
p.to_csv(f"{OUT}/parity-matched.csv", index=False)
print("\nParity status:"); print(p.Status.value_counts().to_string())
m = p[p.Status == "MATCHED"]
if len(m):
    agree = (m.ActClass == m.SimClass).mean() * 100
    print(f"matched {len(m)}/{len(p)} ({len(m)/len(p)*100:.0f}%), exit-class agreement {agree:.0f}%")
    print("actual per-lot sum", round(m.ActPnlPerLot.sum()), "| sim per-lot sum", round(m.SimPnlPerLot.sum()),
          "| median entry diff %", m.EntryDiffPct.median())
    print(pd.crosstab(m.ActClass, m.SimClass, margins=True).to_string())
    print(m.groupby("Symbol").agg(n=("SignalId", "size"), act=("ActPnlPerLot", "sum"), sim=("SimPnlPerLot", "sum")).round(0).to_string())
    print(m.groupby("ActExit").agg(n=("SignalId", "size"), act=("ActPnlPerLot", "sum"), sim=("SimPnlPerLot", "sum")).round(0).to_string())
em = p[p.Status == "ENTRY_MISMATCH"]
if len(em): print("ENTRY_MISMATCH diff % quantiles:", em.EntryDiffPct.quantile([.5, .9]).round(3).to_dict())
