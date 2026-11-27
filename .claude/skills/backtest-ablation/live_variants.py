"""Live-condition variation grid (historical simulation only).

    python .claude/skills/backtest-condition/live_variants.py <out-folder> [--dir CALL|PUT] [--window 15]

Starts from the CURRENT live entry rule and changes ONLY those conditions. Three parts:
  1. LIVE baseline + "all live conditions off" (trend rows only).
  2. Interchange grid: every ON/OFF combination of the live conditions (2^k rows), live thresholds kept.
  3. One-condition sweeps (RSI band, gap-up RSI band, PCR band, ATR min, EMA gap, EarlyTrendGate, LTP-EMA20)
     plus the RSI x PCR joint grid.
Every row is scored with the same outcome, costs, dedupe, non-overlap and chronological split as combo_grid.py
(it reuses its functions). Snapshots + IndicatorSnapshotOutcomes only - never Signals, Orders or TradeJournal.

Live rules (StrategySettings read-only, verified 2026-10-29; SignalEngineService.EvaluateCall/EvaluatePut + gates):
  CALL  EMA20 > EMA50 (1m), LTP > EMA20, |EMA20-EMA50| > EmaGapMinPoints (10; BANKNIFTY 15),
        Rsi5m [45,70]; gap-up open (>= 0.30%) [45,85], PCR [0.98,1.50], AtrPct >= MinAtrPercent 0.03,
        EarlyTrendGate (side 1 = CALL only): blocked when 5m EMA20 > 5m EMA50.
  PUT   EMA20 < EMA50 (1m), LTP < EMA20, EMA gap as above, Rsi5m [28,38]; gap-up open [40,60],
        PCR <= 1.40, SENSEX PCR >= 0.70, AtrPct >= 0.03. (Band2, candle, delta, day, OI filters are off.)
Not modelled (no snapshot input): option-chain/OI gates, strategy selector, Gemini/EarlyCross/BANKNIFTY breakdown
rules, cooldowns, one-position-at-a-time across symbols, live exits. Re-check the settings above before a run.

Writes <out>/variants.csv and <out>/variants.md.
"""
import argparse, itertools, os, sys

import numpy as np
import pandas as pd

sys.path.insert(0, os.path.dirname(__file__))
from combo_grid import load, outcome, cell, md  # noqa: E402

EMA_GAP_LIVE = {"default": 10.0, "BANKNIFTY": 15.0}
LIVE = {
    "CALL": dict(rsi=(45, 70), gapup_rsi=(45, 85), gap_pct=0.30, pcr=(0.98, 1.50), sensex_pcr_min=None, atr=0.03,
                 early_trend="on", ltp_ema=True, ema_gap=EMA_GAP_LIVE),
    "PUT": dict(rsi=(28, 38), gapup_rsi=(40, 60), gap_pct=0.30, pcr=(None, 1.40), sensex_pcr_min=0.70, atr=0.03,
                early_trend="off", ltp_ema=True, ema_gap=EMA_GAP_LIVE),
}
# The switchable live conditions used by the interchange (on/off) grid.
TOGGLES = {
    "CALL": ["ltp_ema", "ema_gap", "rsi", "gapup_rsi", "pcr", "atr", "early_trend"],
    "PUT": ["ltp_ema", "ema_gap", "rsi", "gapup_rsi", "pcr", "sensex_pcr_min", "atr"],
}
OFF = dict(ltp_ema=False, ema_gap=None, rsi=None, gapup_rsi=None, pcr=None, sensex_pcr_min=None, atr=None, early_trend="off")
SHORT = dict(ltp_ema="LTP-EMA20", ema_gap="EMAgap", rsi="RSI", gapup_rsi="GapUpRSI", pcr="PCR",
             sensex_pcr_min="SENSEX-PCRmin", atr="ATR", early_trend="EarlyTrend")


def rule(d, call, rsi, gapup_rsi, gap_pct, pcr, sensex_pcr_min, atr, early_trend, ltp_ema, ema_gap):
    """Boolean mask of minutes the rule would accept. None/False/'off' in a slot = that condition off."""
    m = (d.Ema20 > d.Ema50) if call else (d.Ema20 < d.Ema50)
    if ltp_ema: m &= (d.Ltp > d.Ema20) if call else (d.Ltp < d.Ema20)
    if ema_gap is not None:
        lim = ema_gap if np.isscalar(ema_gap) else d.Symbol.map(ema_gap).fillna(ema_gap["default"])
        m &= (d.Ema20 - d.Ema50).abs() > lim
    r = d.Rsi5m
    gap = (d.DayOpen - d.YesterdayClose) / d.YesterdayClose * 100
    if rsi is not None or gapup_rsi is not None:
        lo, hi = rsi if rsi is not None else (-1, 101)
        up = (gap >= gap_pct).values
        normal_ok = (r >= lo) & (r <= hi)
        if gapup_rsi is not None:
            up_ok = (r >= gapup_rsi[0]) & (r <= gapup_rsi[1])
        else:
            up_ok = normal_ok
        m &= r.notna() & np.where(up, up_ok, normal_ok)
    if pcr is not None:
        lo, hi = pcr
        if lo is not None: m &= d.Pcr >= lo
        if hi is not None: m &= d.Pcr <= hi
    if sensex_pcr_min is not None: m &= (d.Symbol != "SENSEX") | (d.Pcr >= sensex_pcr_min)
    if atr is not None: m &= d.AtrPct >= atr
    five_up = (d.Ema20_5m > 0) & (d.Ema50_5m > 0) & (d.Ema20_5m > d.Ema50_5m)
    if early_trend == "on": m &= ~five_up
    elif early_trend == "reverse": m &= five_up
    return m


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("out"); ap.add_argument("--dir", default="CALL", choices=["CALL", "PUT"])
    ap.add_argument("--window", type=int, default=15, choices=[5, 10, 15])
    ap.add_argument("--from", dest="date_from", default="2026-09-30")
    ap.add_argument("--delta", type=float, default=0.5); ap.add_argument("--cost-per-order", type=float, default=20.0)
    ap.add_argument("--slip-pts", type=float, default=1.0)
    a = ap.parse_args(); os.makedirs(a.out, exist_ok=True)
    call = a.dir == "CALL"

    d = outcome(load(a), a)
    d = d[d.Direction == a.dir].copy()
    d["day"] = d.t.dt.date
    days = sorted(d.day.unique()); n = len(days)
    c1, c2 = days[int(n * .6)], days[int(n * .8)]
    d["Split"] = np.where(d.day < c1, "train", np.where(d.day < c2, "val", "hold"))
    print(f"{a.dir}: {len(d)} rows, {n} days {days[0]}..{days[-1]} | train <{c1} | val <{c2}")

    base = dict(LIVE[a.dir])
    variants = [("LIVE (current settings)", "-", base),
                ("All live conditions off (trend rows only)", "-", dict(base, **OFF))]

    def v(group, label, **chg):
        p = dict(base); p.update(chg); variants.append((label, group, p))

    # Interchange grid: every on/off combination of the live conditions (all-on = LIVE, all-off listed above).
    tg = TOGGLES[a.dir]
    for k in range(1, len(tg)):
        for offs in itertools.combinations(tg, k):
            v("Interchange (live conditions on/off)", "OFF: " + ", ".join(SHORT[x] for x in offs) +
              " | ON: " + ", ".join(SHORT[x] for x in tg if x not in offs), **{x: OFF[x] for x in offs})

    lr = base["rsi"]
    los, his = ([35, 40, 45, 50, 55, 60], [60, 65, 70, 75, 80, 85, 100]) if call else ([0, 20, 25, 28, 30, 32, 35], [35, 38, 40, 42, 45, 50])
    for lo in los:
        for hi in his:
            if hi > lo and (lo, hi) != lr: v("RSI band (normal open)", f"RSI5m {lo}-{hi}", rsi=(lo, hi))
    v("RSI band (normal open)", "RSI filter off (gap-up band kept)", rsi=None)
    gups = [(45, 70), (45, 75), (45, 100), (50, 85), (55, 85)] if call else [(28, 38), (30, 50), (35, 60), (40, 50), (40, 70)]
    for g in gups:
        v("RSI band (gap-up open)", f"Gap-up RSI5m {g[0]}-{g[1]}", gapup_rsi=g)
    v("RSI band (gap-up open)", "Gap-up override off (normal band on gap-up)", gapup_rsi=None)
    for lo in [None, 0.7, 0.8, 0.9, 0.95, 1.05, 1.1]:
        for hi in [1.0, 1.2, 1.3, 1.4, 1.5, None]:
            if (lo, hi) != base["pcr"] and (lo is None or hi is None or hi > lo):
                v("PCR band", f"PCR {lo or 'none'}-{hi or 'none'}", pcr=(lo, hi))
    v("PCR band", "PCR filter off", pcr=None)
    if not call:
        v("PCR band", "SENSEX PCR >= 0.70 off", sensex_pcr_min=None)
    for x in [None, 0.02, 0.04, 0.05]:
        v("ATR min", f"ATR% >= {x}" if x else "ATR filter off", atr=x)
    v("EarlyTrendGate", "EarlyTrendGate " + ("off" if call else "on (block when 5m EMA20>EMA50)"),
      early_trend="off" if call else "on")
    v("EarlyTrendGate", "Require 5m EMA20>EMA50", early_trend="reverse")
    if not call:
        v("EarlyTrendGate", "Require 5m EMA20<EMA50 (5m aligned)", early_trend="on")
    v("LTP vs EMA20", "LTP vs EMA20 off", ltp_ema=False)
    v("EMA gap (live: 10, BANKNIFTY 15)", "EMA gap off", ema_gap=None)
    for x in [5, 10, 15, 20, 30]:
        v("EMA gap (live: 10, BANKNIFTY 15)", f"|EMA20-EMA50| > {x} pts (all symbols)", ema_gap=float(x))
    rsis = [(45, 60), (45, 65), (50, 70), (55, 70), (45, 70)] if call else [(25, 38), (28, 35), (28, 40), (30, 45), (28, 38)]
    pcrs = [(None, 1.0), (0.8, 1.2), (0.98, 1.5), (1.05, None)] if call else [(None, 1.0), (None, 1.2), (None, 1.4), (0.8, None)]
    for rl, rh in rsis:
        for pl, ph in pcrs:
            if ((rl, rh), (pl, ph)) != (lr, base["pcr"]):
                v("RSI x PCR (joint)", f"RSI5m {rl}-{rh} & PCR {pl or 'none'}-{ph or 'none'}", rsi=(rl, rh), pcr=(pl, ph))

    rows = []
    for label, group, p in variants:
        r = cell(d[rule(d, call, **p)], label, a.dir); r["Group"] = group; rows.append(r)
    g = pd.DataFrame(rows)
    live = g.iloc[0]
    g["dHold_vs_live"] = g.hold_Net - live.hold_Net
    g["Pass"] = (g.train_N >= 30) & (g.val_Net > 0) & (g.hold_N >= 30) & (g.hold_Net > 0) & (g.hold_PF > 1.1)
    g["BeatsLive_all3"] = (g.train_Net > live.train_Net) & (g.val_Net > live.val_Net) & (g.hold_Net > live.hold_Net)
    g.to_csv(os.path.join(a.out, "variants.csv"), index=False)

    cols = ["Cell", "train_N", "train_Net", "train_PF", "val_N", "val_Net", "val_PF", "hold_N", "hold_Days", "hold_Net",
            "hold_PF", "all_N", "all_Days", "all_TgtPct", "all_Net", "all_PF", "all_Exp", "all_MaxDD", "BeatsLive_all3", "Pass"]
    with open(os.path.join(a.out, "variants.md"), "w", encoding="utf-8") as f:
        f.write(f"# {a.dir} live-condition variants ({days[0]}..{days[-1]}, {n} days, {a.window}-min window)\n\n"
                "Simulated only (snapshots + outcomes; no live signals or trades). Live rule, every on/off combination of "
                "its conditions, then one condition changed at a time (plus RSI x PCR joint grid).\n\n")
        f.write(md(g.iloc[:2][cols]) + "\n\n")
        for grp, gg in g.iloc[2:].groupby("Group", sort=False):
            f.write(f"## {grp}\n\n" + md(gg.sort_values("all_Net", ascending=False)[cols]) + "\n\n")
    print(g[["Cell", "train_N", "train_Net", "val_Net", "hold_N", "hold_Net", "all_N", "all_Net", "all_PF", "BeatsLive_all3", "Pass"]]
          .sort_values("all_Net", ascending=False).head(25).to_string(index=False))
    print("\nLIVE:", live[["train_N", "train_Net", "val_N", "val_Net", "hold_N", "hold_Net", "all_N", "all_Net", "all_PF"]].to_dict())
    print("variants beating live in all 3 periods:", int(g.BeatsLive_all3.sum()), "| passing:", int(g.Pass.sum()), "of", len(g))


if __name__ == "__main__":
    main()
