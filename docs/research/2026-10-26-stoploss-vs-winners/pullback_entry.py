"""Does a limit entry on a small pullback (a better price) cut first-minutes stop-outs without losing the winners?
Base rate over every Bullish/Bearish snapshot minute (snapshots-outcomes.csv), index candles only, no look-ahead.
Baseline = enter at Ltp now. Variant = rest a limit p*R better than Ltp for W minutes; if filled, stop/target keep
their distances from the NEW fill price; if never filled the opportunity is skipped (counted as 0 R, and also reported
as the average R of the skipped ones so adverse selection is visible).
"""
import numpy as np, pandas as pd, warnings
warnings.filterwarnings('ignore'); pd.set_option('display.width', 200)
d = pd.read_csv('docs/research/2026-10-26-backtest-condition-both/snapshots-outcomes.csv', parse_dates=['MinuteIst'])
d = d[d.Result.isin(['WIN', 'LOSS', 'TIMEOUT_WIN', 'TIMEOUT_LOSS'])].reset_index(drop=True)
c = pd.read_csv('docs/research/2026-10-26-winrate-60-search/candles-1m.csv', parse_dates=['CandleTime'])
c['t'] = c.CandleTime + pd.Timedelta(hours=5, minutes=30)
C = {}
for s, g in c.groupby('Symbol'):
    g = g.sort_values('t'); C[s[:-3]] = (g.t.values.astype('datetime64[m]'), g.High.to_numpy(float), g.Low.to_numpy(float), g.Close.to_numpy(float))
atr = (d.AtrPct / 100 * d.Ltp).to_numpy(); R = d.StopPts.to_numpy(); T = 1.5 * atr; E0 = d.Ltp.to_numpy()
call = (d.Dir == 'CALL').to_numpy(); mins = d.MinuteIst.values.astype('datetime64[m]')
def run(p, W, H=45):
    out = np.full(len(d), np.nan); filled = np.zeros(len(d), bool)
    for i in range(len(d)):
        ts, hi, lo, cl = C[d.Symbol.iat[i]]
        j = int(np.searchsorted(ts, mins[i], side='right')); n = min(len(ts) - j, H)
        if n < 2: continue
        Hh, Ll, Cc = hi[j:j + n], lo[j:j + n], cl[j:j + n]
        r, t_ = R[i], T[i]
        if p == 0: k = 0; E = E0[i]
        else:
            lvl = E0[i] - p * r if call[i] else E0[i] + p * r
            touch = (Ll <= lvl) if call[i] else (Hh >= lvl)
            touch = touch[:W]
            if not touch.any(): out[i] = 0.0; continue
            k = int(np.argmax(touch)); E = lvl
        filled[i] = True
        st = (Ll[k:] <= E - r) if call[i] else (Hh[k:] >= E + r)
        tg = (Hh[k + 1:] >= E + t_) if call[i] else (Ll[k + 1:] <= E - t_)       # target only from the NEXT bar after the fill
        si = int(np.argmax(st)) if st.any() else 10**9
        ti = (int(np.argmax(tg)) + 1) if tg.any() else 10**9
        if si == 10**9 and ti == 10**9: out[i] = ((Cc[-1] - E) if call[i] else (E - Cc[-1])) / r
        elif si <= ti: out[i] = -1.0
        else: out[i] = t_ / r
    return out, filled
base, _ = run(0, 0)
mid = d.MinuteIst.median(); h1 = (d.MinuteIst < mid).to_numpy()
print('side  variant                      opps   filled  fill%   stop%   avgR/opp  H1     H2   | avgR of skipped (baseline)')
for sd in ['PUT', 'CALL']:
    m = (d.Dir == sd).to_numpy()
    print(f'{sd}   baseline (enter now)       {m.sum():6d}  {m.sum():6d}  100.0   {np.mean(base[m] == -1)*100:5.1f}   {np.nanmean(base[m]):7.3f}  {np.nanmean(base[m&h1]):6.3f} {np.nanmean(base[m&~h1]):6.3f}')
    for p, W in [(0.15, 3), (0.25, 5), (0.35, 5), (0.5, 8)]:
        o, f = run(p, W)
        fm = m & f; sk = m & ~f & ~np.isnan(o)
        stop = np.mean(o[fm] == -1) * 100 if fm.any() else np.nan
        print(f'{sd}   limit {p:.2f}R within {W}m        {m.sum():6d}  {fm.sum():6d}  {fm.sum()/m.sum()*100:5.1f}   {stop:5.1f}   {np.nanmean(o[m]):7.3f}  {np.nanmean(o[m&h1]):6.3f} {np.nanmean(o[m&~h1]):6.3f}   | {np.nanmean(base[sk]):6.3f}')
