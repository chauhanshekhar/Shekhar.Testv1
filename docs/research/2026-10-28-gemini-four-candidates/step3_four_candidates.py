"""Step 3: four candidate rules, one entry per symbol per 15 min, path-ordered first touch on 1m candles,
costs applied, earlier-days select / later-days validate. Read-only analysis of exported CSVs.
Features: IndicatorSnapshots minute rows (snapshot-forward.csv). Rsi = 1-minute RSI and alignment definition
copied from the pasted query. Model = skill snapshot_outcomes: stop clamp(1.25*ATR,20,100), target 1.5*ATR,
45-min horizon, both-in-one-candle = stop, timeout marked to close."""
import csv, sys, collections, statistics
import numpy as np, pandas as pd
D = sys.argv[1]
LOT = {"NIFTY": 65, "BANKNIFTY": 30, "SENSEX": 20}
c = pd.read_csv(f"{D}/candles-1m.csv"); c["t"] = pd.to_datetime(c.CandleTime) + pd.Timedelta(hours=5, minutes=30)
cd = {}
for s, g in c.groupby("Symbol"):
    g = g.sort_values("t"); cd[s[:-3]] = (g.t.values.astype("datetime64[m]"), g.High.values, g.Low.values, g.Close.values)
df = pd.read_csv(f"{D}/snapshot-forward.csv", encoding="utf-8-sig")
df["gap"] = 100 * (df.DayOpen - df.YesterdayClose) / df.YesterdayClose
df["day"] = df.MinuteIst.str[:10]
df["opentype"] = np.where(df.gap >= .3, "GapUp", np.where(df.gap <= -.3, "GapDown", "Normal"))
df["emaal"] = np.where((df.Ltp > df.Ema20) & (df.Ema20 > df.Ema50) & (df.Ema20_5m > df.Ema50_5m), "Bull",
              np.where((df.Ltp < df.Ema20) & (df.Ema20 < df.Ema50) & (df.Ema20_5m < df.Ema50_5m), "Bear", "Mixed"))
CAND = {
 "C1 GapDown PUT rsi45-55 atr<.03 bear": ("GapDown", "PUT", 45, 55, 0, .03, "Bear"),
 "C2 GapUp CALL rsi55-65 atr<.03 bull": ("GapUp", "CALL", 55, 65, 0, .03, "Bull"),
 "C3 GapUp PUT rsi45-55 atr.04-.05 bear": ("GapUp", "PUT", 45, 55, .04, .05, "Bear"),
 "C4 GapUp PUT rsi45-55 atr.04-.05 mixed": ("GapUp", "PUT", 45, 55, .04, .05, "Mixed"),
}
days = sorted(df.day.unique()); cut = days[int(len(days) * .6)]
def sim(r, d):
    atr = r.AtrPct / 100 * r.Ltp; R = min(max(1.25 * atr, 20), 100); T = 1.5 * atr
    ts, hi, lo, cl = cd[r.Symbol]; j = int(np.searchsorted(ts, np.datetime64(r.MinuteIst[:16]), side="right"))
    if j >= len(ts): return None
    H, L, C = hi[j:j+45], lo[j:j+45], cl[j:j+45]; E = r.Ltp
    hs, ht = (L <= E - R, H >= E + T) if d == "CALL" else (H >= E + R, L <= E - T)
    si = int(np.argmax(hs)) if hs.any() else 10**9; ti = int(np.argmax(ht)) if ht.any() else 10**9
    if si == ti == 10**9:
        mv = (C[-1] - E) if d == "CALL" else (E - C[-1]); return ("TIMEOUT", mv, R, len(H) < 45)
    return ("STOP", -R, R, False) if si <= ti else ("TARGET", T, R, False)
def pf(x): 
    g = sum(v for v in x if v > 0); l = -sum(v for v in x if v < 0); return g / l if l else float("inf")
def report(name, rows, tag):
    if not rows: print(f"  {tag}: 0 entries"); return
    k = collections.Counter(r["res"] for r in rows); n = len(rows)
    pnl = [r["pnl"] for r in rows]; dd = len({r["day"] for r in rows})
    print(f"  {tag}: n={n} days={dd} target={k['TARGET']/n:.0%} stop={k['STOP']/n:.0%} timeout={k['TIMEOUT']/n:.0%} "
          f"net=Rs{sum(pnl):,.0f} avg/trade=Rs{statistics.mean(pnl):,.0f} PF={pf(pnl):.2f} gross(no cost)=Rs{sum(r['gross'] for r in rows):,.0f}")
for name, (op, d, a, b, la, ha, al) in CAND.items():
    s = df[(df.opentype == op) & (df.Rsi >= a) & (df.Rsi < b) & (df.AtrPct >= la) & (df.AtrPct < ha) & (df.emaal == al)]
    if d == "PUT": pass
    rows = []; last = {}
    for r in s.sort_values(["Symbol", "MinuteIst"]).itertuples(index=False):
        t = r.MinuteIst; m = int(t[11:13]) * 60 + int(t[14:16]); k = (r.Symbol, r.day)
        if k in last and m - last[k] < 15: continue
        o = sim(r, d)
        if o is None or o[3]: continue
        last[k] = m; res, mv, R, _ = o; q = LOT[r.Symbol]; cost = 2 * 20 + 2 * 1.0 * q
        gross = mv * .5 * q; rows.append(dict(sym=r.Symbol, day=r.day, res=res, pnl=gross - cost, gross=gross))
    print(f"\n{name}  | minute rows matching={len(s)} -> non-overlap entries={len(rows)} (split date {cut})")
    report(name, rows, "ALL      ")
    report(name, [r for r in rows if r["day"] < cut], "TRAIN    ")
    report(name, [r for r in rows if r["day"] >= cut], "VALIDATE ")
    for sy in ("NIFTY", "BANKNIFTY", "SENSEX"):
        report(name, [r for r in rows if r["sym"] == sy], f"{sy:9s}")
    pd_ = collections.defaultdict(float)
    for r in rows: pd_[r["day"]] += r["pnl"]
    print("  by day:", {k: round(v) for k, v in sorted(pd_.items())})
