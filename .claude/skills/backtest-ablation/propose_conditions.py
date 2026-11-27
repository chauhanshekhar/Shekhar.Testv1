"""Search the live-exit replay outcomes for the best entry-condition combination and write a proposal.

    python .claude/skills/backtest-condition/propose_conditions.py <replay-folder> [--min-trades 20] [--no-costs]

<replay-folder> is the output of
    dotnet run --project src/Trading.Backtest -- --side BOTH --live-interchange --from 2026-10-07 \
        --cost-per-order 20 --slippage-points 1 --dump-trades --out <replay-folder>
and must contain trades.csv (one row per simulated trade: Configuration, Symbol scope, EntryIst, PnlRs, CostRs).
Simulated outcomes only - never Signals, Orders or TradeJournal.

For every Configuration x scope (ALL = one position across symbols; NIFTY/BANKNIFTY/SENSEX = symbol alone):
  * chronological 60/20/20 day split into train / validation / holdout, net after costs per period
  * PF, expectancy, max drawdown, distinct days, and net with the single best day removed (one-day dependence)
Pass rule (a candidate, not a rule):
  N >= --min-trades, net > 0 in ALL three periods, PF > 1.2, net without best day > 0,
  and beats the live configuration of the same side and scope in total AND in holdout.
Also reports each condition's marginal effect: net with the condition OFF minus net with it ON, averaged over
every pairing of the other conditions, and how often the sign agrees (consistency).

Writes <replay-folder>/proposal.md and candidates.csv.
"""
import argparse, os, sys

import numpy as np
import pandas as pd

sys.path.insert(0, os.path.dirname(__file__))
from combo_grid import md  # noqa: E402

SCOPES = ["ALL", "NIFTY", "BANKNIFTY", "SENSEX"]


def metrics(t, cuts):
    p = t.Net
    w, l = p[p > 0].sum(), -p[p < 0].sum()
    cum = p.cumsum()
    byday = t.groupby("day").Net.sum()
    r = dict(N=len(t), Days=t.day.nunique(), Net=round(p.sum()), Gross=round(t.PnlRs.sum()),
             PF=round(w / l, 2) if l else np.inf, Exp=round(p.mean(), 1) if len(t) else 0.0,
             WinPct=round((p > 0).mean() * 100, 1) if len(t) else 0.0,
             MaxDD=round((cum.cummax() - cum).max()) if len(t) else 0,
             NetExBestDay=round(p.sum() - byday.max()) if len(byday) else 0)
    for name, lo, hi in [("Train", None, cuts[0]), ("Val", cuts[0], cuts[1]), ("Hold", cuts[1], None)]:
        m = np.ones(len(t), bool)
        if lo is not None: m &= t.day.values >= lo
        if hi is not None: m &= t.day.values < hi
        r[f"{name}N"], r[f"{name}Net"] = int(m.sum()), round(p[m].sum())
    return r


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("folder"); ap.add_argument("--min-trades", type=int, default=20)
    ap.add_argument("--no-costs", action="store_true", help="rank on gross P&L (costs ignored)")
    a = ap.parse_args()

    t = pd.read_csv(os.path.join(a.folder, "trades.csv"))
    t["Net"] = t.PnlRs - (0 if a.no_costs else t.CostRs.fillna(0))
    t["day"] = pd.to_datetime(t.EntryIst).dt.date
    t["Side"] = t.Configuration.str.split("|").str[0]
    t["Off"] = t.Configuration.str.split("|").str[-1]
    t = t.sort_values("EntryIst")
    days = sorted(t.day.unique()); n = len(days)
    cuts = (days[int(n * .6)], days[int(n * .8)])

    rows = []
    for (cfg, scope), g in t.groupby(["Configuration", "Symbol"]):
        rows.append(dict(Configuration=cfg, Side=cfg.split("|")[0], Scope=scope, Off=cfg.split("|")[-1], **metrics(g, cuts)))
    c = pd.DataFrame(rows)
    live = c[c.Off == "Live"].set_index(["Side", "Scope"])
    c["LiveNet"] = [live.Net.get((s, sc), np.nan) for s, sc in zip(c.Side, c.Scope)]
    c["LiveHold"] = [live.HoldNet.get((s, sc), np.nan) for s, sc in zip(c.Side, c.Scope)]
    c["Pass"] = ((c.N >= a.min_trades) & (c.TrainNet > 0) & (c.ValNet > 0) & (c.HoldNet > 0) & (c.PF > 1.2)
                 & (c.NetExBestDay > 0) & (c.Net > c.LiveNet) & (c.HoldNet > c.LiveHold) & (c.Off != "Live"))
    c.to_csv(os.path.join(a.folder, "candidates.csv"), index=False)

    # Marginal effect of switching each condition off, paired over every combination of the others.
    eff = []
    for side, s in c.groupby("Side"):
        toggles = sorted({x for o in s.Off for x in o.removeprefix("Off-").split("+") if o != "Live"})
        for scope, sc in s.groupby("Scope"):
            net = {frozenset() if o == "Live" else frozenset(o.removeprefix("Off-").split("+")): v for o, v in zip(sc.Off, sc.Net)}
            for tg in toggles:
                d = [net[k | {tg}] - net[k] for k in net if tg not in k and (k | {tg}) in net]
                if d:
                    eff.append(dict(Side=side, Scope=scope, Condition=tg, Pairs=len(d), AvgGainIfOff=round(np.mean(d)),
                                    OffBetterPct=round(np.mean([x > 0 for x in d]) * 100)))
    e = pd.DataFrame(eff)

    cols = ["Configuration", "Scope", "N", "Days", "WinPct", "Gross", "Net", "PF", "Exp", "MaxDD", "NetExBestDay",
            "TrainNet", "ValNet", "HoldNet", "LiveNet", "LiveHold", "Pass"]
    basis = "gross (costs ignored)" if a.no_costs else "after costs (CostRs from the replay)"
    with open(os.path.join(a.folder, "proposal.md"), "w", encoding="utf-8") as f:
        f.write(f"# Condition search on live-exit replay ({days[0]}..{days[-1]}, {n} sessions)\n\n"
                f"Net is {basis}. Split train <{cuts[0]} | validation <{cuts[1]} | holdout rest. "
                "Simulated outcomes only; no live signals or trades used.\n\n"
                f"Pass = N >= {a.min_trades}, all three periods > 0, PF > 1.2, profitable without its best day, "
                "beats live in total and holdout (same side and scope).\n\n")
        f.write("## Live baseline\n\n" + md(c[c.Off == "Live"].sort_values(["Side", "Scope"])[cols]) + "\n\n")
        p = c[c.Pass].sort_values(["Side", "HoldNet"], ascending=[True, False])
        f.write(f"## Passing candidates ({len(p)})\n\n" + (md(p[cols]) if len(p) else
                "None - no combination passed. No historically validated improvement under these assumptions.") + "\n\n")
        for side in ["CALL", "PUT"]:
            top = c[(c.Side == side) & (c.Off != "Live")].sort_values("Net", ascending=False).head(10)
            f.write(f"## {side}: top 10 by net (context, pass or not)\n\n" + md(top[cols]) + "\n\n")
        f.write("## Condition effect (net when OFF minus net when ON, averaged over the other combinations)\n\n"
                + md(e.sort_values(["Side", "Scope", "AvgGainIfOff"], ascending=[True, True, False])) + "\n")
    print(f"{n} sessions, {len(c)} cells, passing {int(c.Pass.sum())}")
    print(c[c.Pass].sort_values("HoldNet", ascending=False)[cols].head(20).to_string(index=False))
    print(e[e.Scope == "ALL"].to_string(index=False))


if __name__ == "__main__":
    main()
