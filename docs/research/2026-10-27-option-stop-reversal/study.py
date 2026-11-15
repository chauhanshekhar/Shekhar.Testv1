"""
Read-only study: after an OPTION_SL_HIT, how often did the index reverse past the entry level within 30 min,
against the base rate of the same move on every comparable 1-minute bar? And what would a persistence rule
("do not exit on the spike; exit only if the index is still beyond the stop level N minutes later") have changed?

Inputs (exported with sqlcmd from the database, SELECT only):
  signals.txt  Signals + TradeJournal sum + partial count, '|' separated, 2nd line is the sqlcmd dash row
  units.txt    entry-order filled units per SignalId
  candles.txt  Candles *_1m (CandleTime is UTC -> +5:30 for IST), High/Low/Close
Run:  python study.py <exports_dir> <out_dir>

Option premium paths do not exist for history (OptionChainData starts the same day), so premium is a delta proxy:
  premium(idx) = OptionLtp + DeltaAtEntry * (idx - EntryPrice)      (no gamma / theta / IV)
It is stated everywhere it is used. Costs: Rs20 per order + 1 premium point per side (the user's confirmed model).
"""
import sys, csv, os, datetime as dt
from collections import defaultdict
import numpy as np, pandas as pd

src, out = sys.argv[1], sys.argv[2]
IST = dt.timedelta(hours=5, minutes=30)
WINDOW = 30           # minutes after exit
LONG = 90             # minutes for the index-SL-vs-T1 continuation
COST_ORDER, SLIP = 20.0, 1.0


def read(path):
    rows = [l.rstrip("\n") for l in open(path, encoding="utf-8-sig") if l.strip()]
    hdr = rows[0].split("|")
    return pd.DataFrame([r.split("|") for r in rows[2:] if "rows affected" not in r], columns=hdr)


sig = read(f"{src}/signals.txt")
units = read(f"{src}/units.txt")
cd = read(f"{src}/candles.txt")

for c in ["EntryPrice", "StopLoss", "Target1", "OptionLtp", "OptionSL", "OptionT1", "DeltaAtEntry", "Quantity", "Pnl", "Partials", "Id"]:
    sig[c] = pd.to_numeric(sig[c], errors="coerce")
sig["CreatedOn"] = pd.to_datetime(sig["CreatedOn"])
sig["ExitTime"] = pd.to_datetime(sig["ExitTime"])
units["SignalId"] = pd.to_numeric(units["SignalId"]); units["EntryUnits"] = pd.to_numeric(units["EntryUnits"])
sig = sig.merge(units, left_on="Id", right_on="SignalId", how="left")
sig["Units"] = sig["EntryUnits"]

cd["T"] = pd.to_datetime(cd["T"]) + IST
for c in ["H", "L", "C"]:
    cd[c] = pd.to_numeric(cd[c])
cd["Symbol"] = cd["Symbol"].str.replace("_1m", "", regex=False)
cd = cd.sort_values(["Symbol", "T"]).reset_index(drop=True)
cd["Day"] = cd["T"].dt.date

# per-symbol arrays of 1-minute bars (index start time in IST)
bars = {}
for s, g in cd.groupby("Symbol"):
    g = g.reset_index(drop=True)
    bars[s] = dict(t=g["T"].values.astype("datetime64[m]"), h=g["H"].values, l=g["L"].values, c=g["C"].values,
                   day=g["Day"].values)


def idx_at(s, t, side="left"):
    return int(np.searchsorted(bars[s]["t"], np.datetime64(t, "m"), side=side))


def reversed_within(s, i0, ref, call, n, lim_stop=None):
    """first bar in [i0, i0+n) whose range reaches ref on the favourable side; also whether index stop (lim_stop)
    is reached first. Same-bar ties count as stop-first (conservative)."""
    b = bars[s]
    i1 = min(i0 + n, len(b["t"]))
    for i in range(i0, i1):
        # stop-first test
        stop_hit = lim_stop is not None and ((b["l"][i] <= lim_stop) if call else (b["h"][i] >= lim_stop))
        rev = (b["h"][i] >= ref) if call else (b["l"][i] <= ref)
        if stop_hit:
            return "STOP_FIRST"
        if rev:
            return "REVERSED"
    return "NEITHER"


# ---------- 1. the stop-outs -------------------------------------------------------------------
so = sig[(sig["ExitReason"] == "OPTION_SL_HIT") & (sig["StrategyType"] == "SINGLE") & sig["ExitTime"].notna()].copy()
so["Day"] = so["ExitTime"].dt.date
recs = []
for r in so.itertuples():
    s = r.Symbol
    if s not in bars:
        continue
    call = r.SignalType == "CALL"
    ex = idx_at(s, r.ExitTime.floor("min"), "right")     # first FULL bar after the exit minute
    if ex >= len(bars[s]["t"]):
        continue
    ref = r.EntryPrice
    hold = max(1, int((r.ExitTime - r.CreatedOn).total_seconds() // 60))
    ci = max(0, idx_at(s, r.ExitTime.floor("min"), "left"))
    exit_idx = bars[s]["c"][min(ci, len(bars[s]["c"]) - 1)]
    adverse = (ref - exit_idx) if call else (exit_idx - ref)
    outcome = reversed_within(s, ex, ref, call, WINDOW, r.StopLoss if r.StopLoss > 0 else None)
    outcome_nostop = reversed_within(s, ex, ref, call, WINDOW, None)
    recs.append(dict(Id=r.Id, Symbol=s, Side=r.SignalType, Day=r.Day, HoldMin=hold, AdversePts=round(adverse, 2),
                     Pnl=r.Pnl, Units=r.Units, Out30=outcome, Rev30=outcome_nostop == "REVERSED"))
so_df = pd.DataFrame(recs)

# ---------- 2. base rate: any bar, same symbol & side, same adverse move over the same hold -----------------
# For each stop-out i: all bars t (09:30-15:00 IST, any session) where the index moved >= AdversePts against the
# side over the last HoldMin minutes; outcome = returns to the bar-(t-hold) close within 30 min. Same measure.
base_rows = []
for r in so_df.itertuples():
    s = r.Symbol; call = r.Side == "CALL"; b = bars[s]; h = r.HoldMin; d = max(r.AdversePts, 0.0)
    n = len(b["c"])
    tm = pd.to_datetime(b["t"])
    mins = (tm.hour * 60 + tm.minute).values
    ok = (mins >= 9 * 60 + 30) & (mins <= 15 * 60)
    idxs = np.arange(h, n - WINDOW)
    idxs = idxs[ok[idxs]]
    # same session only
    sess = b["day"]
    idxs = idxs[sess[idxs] == sess[idxs - h]]
    ref = b["c"][idxs - h]
    move = (ref - b["c"][idxs]) if call else (b["c"][idxs] - ref)
    sel = idxs[move >= d]
    if len(sel) == 0:
        continue
    hit = 0
    for i in sel:
        refv = b["c"][i - h]
        j1 = min(i + 1 + WINDOW, n)
        seg_h = b["h"][i + 1:j1]; seg_l = b["l"][i + 1:j1]
        hit += (seg_h.max() >= refv) if call else (seg_l.min() <= refv)
    base_rows.append(dict(Id=r.Id, Day=r.Day, Symbol=s, Side=r.Side, N=len(sel), Rate=hit / len(sel)))
base_df = pd.DataFrame(base_rows)

# ---------- 3. other exit types, same measure (does a stop-out look different from other exits?) -------------------
oth = []
for r in sig[sig["ExitTime"].notna() & (sig["StrategyType"] == "SINGLE") & (sig["ExitReason"] != "OPTION_SL_HIT")].itertuples():
    s = r.Symbol
    if s not in bars:
        continue
    call = r.SignalType == "CALL"
    ex = idx_at(s, r.ExitTime.floor("min"), "right")
    if ex >= len(bars[s]["t"]):
        continue
    oth.append(dict(Reason=r.ExitReason, Rev30=reversed_within(s, ex, r.EntryPrice, call, WINDOW, None) == "REVERSED"))
oth_df = pd.DataFrame(oth)

# ---------- 4. persistence rule, delta proxy ---------------------------------------------------------------------
def prem(r, idxv):
    return r.OptionLtp + r.DeltaAtEntry * (idxv - r.EntryPrice)


rule_rows = []
for N in (1, 2, 3, 5):
    pb = pr = 0.0
    kept = adv = 0
    for r in so.itertuples():
        s = r.Symbol
        if s not in bars or pd.isna(r.Units) or r.DeltaAtEntry == 0 or r.StopLoss <= 0:
            continue
        call = r.SignalType == "CALL"
        b = bars[s]
        ex = idx_at(s, r.ExitTime.floor("min"), "right")      # first full bar after the exit
        k = ex + N - 1                                         # bar whose CLOSE is N minutes after the exit
        if k >= len(b["t"]) or r.OptionLtp <= 0:
            continue
        stop_eq = r.EntryPrice + (r.OptionSL - r.OptionLtp) / r.DeltaAtEntry   # index level where proxy premium = stop
        u = r.Units
        base_pnl = (r.OptionSL - r.OptionLtp) * u - 2 * (COST_ORDER + SLIP * u) + 0.0
        close_k = b["c"][k]
        confirmed = (close_k >= stop_eq) if not call else (close_k <= stop_eq)
        # an exit order is placed in both arms; the entry order cost is common and cancels, so only exits counted
        if confirmed:
            rule_pnl = (prem(r, close_k) - r.OptionLtp) * u - 2 * (COST_ORDER + SLIP * u)
            adv += 1
        else:
            kept += 1
            # keep the trade: first of index SL / index T1 within LONG minutes (T1 = full exit at the T1 premium proxy)
            res = None
            for i in range(k + 1, min(k + 1 + LONG, len(b["t"]))):
                if b["day"][i] != b["day"][k]:
                    break
                sl_hit = (b["l"][i] <= r.StopLoss) if call else (b["h"][i] >= r.StopLoss)
                t1_hit = r.Target1 > 0 and ((b["h"][i] >= r.Target1) if call else (b["l"][i] <= r.Target1))
                if sl_hit:
                    res = r.StopLoss; break
                if t1_hit:
                    res = r.Target1; break
            if res is None:
                last = min(k + LONG, len(b["t"]) - 1)
                while b["day"][last] != b["day"][k]:
                    last -= 1
                res = b["c"][last]
            rule_pnl = (prem(r, res) - r.OptionLtp) * u - 2 * (COST_ORDER + SLIP * u)
        pb += base_pnl; pr += rule_pnl
    rule_rows.append(dict(N_min=N, Trades=kept + adv, ConfirmedAdverse=adv, Released=kept,
                          BaselineNet=round(pb), RuleNet=round(pr), Delta=round(pr - pb)))
rule_df = pd.DataFrame(rule_rows)

# per-day split of the N=2 rule
day_rows = defaultdict(lambda: [0.0, 0.0, 0])
N = 2
for r in so.itertuples():
    s = r.Symbol
    if s not in bars or pd.isna(r.Units) or r.DeltaAtEntry == 0 or r.StopLoss <= 0 or r.OptionLtp <= 0:
        continue
    call = r.SignalType == "CALL"; b = bars[s]
    ex = idx_at(s, r.ExitTime.floor("min"), "right"); k = ex + N - 1
    if k >= len(b["t"]):
        continue
    stop_eq = r.EntryPrice + (r.OptionSL - r.OptionLtp) / r.DeltaAtEntry
    u = r.Units; cost = 2 * (COST_ORDER + SLIP * u)
    base = (r.OptionSL - r.OptionLtp) * u - cost
    ck = b["c"][k]
    if (ck >= stop_eq) if not call else (ck <= stop_eq):
        rule = (prem(r, ck) - r.OptionLtp) * u - cost
    else:
        res = None
        for i in range(k + 1, min(k + 1 + LONG, len(b["t"]))):
            if b["day"][i] != b["day"][k]:
                break
            sl_hit = (b["l"][i] <= r.StopLoss) if call else (b["h"][i] >= r.StopLoss)
            t1_hit = r.Target1 > 0 and ((b["h"][i] >= r.Target1) if call else (b["l"][i] <= r.Target1))
            if sl_hit: res = r.StopLoss; break
            if t1_hit: res = r.Target1; break
        if res is None:
            last = min(k + LONG, len(b["t"]) - 1)
            while b["day"][last] != b["day"][k]:
                last -= 1
            res = b["c"][last]
        rule = (prem(r, res) - r.OptionLtp) * u - cost
    d = b["day"][k]
    day_rows[d][0] += base; day_rows[d][1] += rule; day_rows[d][2] += 1
day_df = pd.DataFrame([dict(Day=d, Trades=v[2], Baseline=round(v[0]), Rule=round(v[1]), Delta=round(v[1] - v[0])) for d, v in sorted(day_rows.items())])

# ---------- output -----------------------------------------------------------------------------------------------
os.makedirs(out, exist_ok=True)
so_df.to_csv(f"{out}/stopouts.csv", index=False)
base_df.to_csv(f"{out}/base_rate.csv", index=False)
rule_df.to_csv(f"{out}/persistence_rule.csv", index=False)
day_df.to_csv(f"{out}/persistence_rule_by_day.csv", index=False)

print("STOP-OUTS (OPTION_SL_HIT, SINGLE):", len(so_df), "days:", so_df["Day"].nunique())
print(so_df["Out30"].value_counts().to_string())
print("reversal past entry within 30 min (ignoring index SL):", round(so_df["Rev30"].mean(), 3))
print("\nBASE RATE (same adverse move & hold, any bar):  mean per-stop-out rate =", round(base_df["Rate"].mean(), 3),
      " n_stopouts=", len(base_df), " bars matched (median)=", int(base_df["N"].median()))
print("\nOTHER EXITS, same reversal measure:")
print(oth_df.groupby("Reason")["Rev30"].agg(["count", "mean"]).round(3).to_string())
print("\nBY SYMBOL x SIDE (stop-outs):")
print(so_df.groupby(["Symbol", "Side"]).agg(N=("Id", "count"), Rev30=("Rev30", "mean")).round(3).to_string())
bd = base_df.groupby(["Symbol", "Side"])["Rate"].mean().round(3)
print("base rate by symbol x side:\n", bd.to_string())
print("\nBY DAY (stop-outs reversed):")
dd = so_df.groupby("Day").agg(N=("Id", "count"), Reversed=("Rev30", "sum")).assign(Rate=lambda x: (x.Reversed / x.N).round(2))
print(dd.to_string())
print("\nPERSISTENCE RULE (proxy, net of Rs20/order + 1pt/side), all stop-outs:")
print(rule_df.to_string(index=False))
print("\nRULE N=2 by day:")
print(day_df.to_string(index=False))

# ---------- 5. proxy fidelity + what the released trades did (diagnostic, N=2) ------------------------------------
fid = []
for r in so.itertuples():
    s = r.Symbol
    if s not in bars or r.DeltaAtEntry == 0 or r.OptionLtp <= 0 or r.StopLoss <= 0 or pd.isna(r.Units):
        continue
    call = r.SignalType == "CALL"; b = bars[s]
    ci = idx_at(s, r.ExitTime.floor("min"), "left")
    if ci >= len(b["c"]):
        continue
    stop_eq = r.EntryPrice + (r.OptionSL - r.OptionLtp) / r.DeltaAtEntry
    c_exit = b["c"][ci]
    beyond = (c_exit >= stop_eq) if not call else (c_exit <= stop_eq)
    # index distance to the stop-equivalent level vs the index SL, as % of the stop-equivalent distance
    fid.append(dict(Id=r.Id, Day=r.ExitTime.date(), BeyondStopEqAtExitBar=beyond,
                    StopEqPts=round(abs(stop_eq - r.EntryPrice), 1), IndexSlPts=round(abs(r.StopLoss - r.EntryPrice), 1),
                    ExitBarAdverse=round((c_exit - r.EntryPrice) * (1 if not call else -1), 1)))
fid = pd.DataFrame(fid)
print("\nPROXY FIDELITY: stop-outs where the index (exit-minute close) was already beyond the delta-implied stop level:",
      int(fid["BeyondStopEqAtExitBar"].sum()), "of", len(fid))
print("median stop-eq distance", fid["StopEqPts"].median(), "pts; median index-SL distance", fid["IndexSlPts"].median(),
      "pts; median adverse index move at exit bar", fid["ExitBarAdverse"].median(), "pts")
fid.to_csv("proxy_fidelity.csv", index=False)
