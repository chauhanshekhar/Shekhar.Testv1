"""Option-noise ENTRY gate: historical test on OptionMinuteHistory. READ-ONLY.
Features at entry minute t0 (only candles known at t0: the 5 completed minutes before t0, plus the t0 read itself):
  spike = L0 / median(prior 5 completed LTPs) - 1
  noise = median |pct change| across prior5 + L0 (5 changes)
Outcome per signal = baseline bracket replay (stop=entry-d, T1=entry+tp, force exit 15:15, cost 2 pts) - same as
2026-11-11-option-quote-noise/replay.py V0. Not live P&L; isolates entry selection.
Method: (a) fixed spike thresholds on all data; (b) per-index noise cutoff = top-q quantile fitted on FIRST half only,
applied to SECOND half (out of sample). Blocked trades are compared with kept trades; a gate only 'works' if blocked
trades are worse than kept ones in the held-out half too."""
import json, pyodbc, pandas as pd, numpy as np, warnings, datetime as dt
warnings.filterwarnings('ignore'); pd.set_option('display.width', 250); pd.set_option('display.max_columns', 40)
cs = json.load(open('src/Trading.Worker/appsettings.Production.json', encoding='utf-8-sig'))['ConnectionStrings']['DefaultConnection']
k = {a.lower().replace(' ', '').replace('_', ''): b for a, b in (p.split('=', 1) for p in cs.split(';') if '=' in p)}
cn = pyodbc.connect(f"DRIVER={{ODBC Driver 17 for SQL Server}};SERVER={k['server']};DATABASE={k['database']};TrustServerCertificate=yes;UID={k['userid']};PWD={k['password']};", timeout=120)
COST = 2.0
omh = pd.read_sql("SELECT TradingSymbol,MinuteTime,Ltp FROM OptionMinuteHistory WITH (NOLOCK) WHERE Ltp>0 ORDER BY TradingSymbol,MinuteTime", cn)
sg = pd.read_sql("SELECT Id,Symbol,SignalType,OptionSymbol,CreatedOn,OptionLtp,OptionSL,OptionT1,EntryPrice,StopLoss,DeltaAtEntry FROM Signals WITH (NOLOCK) WHERE CreatedOn>=? AND OptionLtp>0 AND OptionSL>0 AND OptionT1>0 AND OptionSymbol IS NOT NULL ORDER BY Id", cn, params=[omh.MinuteTime.min()])
sg['d_true'] = sg.OptionLtp - sg.OptionSL; sg['d_est'] = (sg.EntryPrice - sg.StopLoss).abs() * sg.DeltaAtEntry.abs()
sg['d'] = np.where(sg.d_true > 0.5, sg.d_true, sg.d_est); sg = sg[sg.d > 0.5].copy()
grp = {s: g.set_index('MinuteTime').Ltp for s, g in omh.groupby('TradingSymbol')}
rows = []
for _, s in sg.iterrows():
    g = grp.get(s.OptionSymbol)
    t0 = s.CreatedOn.replace(second=0, microsecond=0)
    if g is None or t0 not in g.index: continue
    prior = g[(g.index < t0) & (g.index >= t0 - dt.timedelta(minutes=8))].tail(5)   # tolerate small gaps
    if len(prior) < 5: continue
    L0 = float(g[t0]); med = float(prior.median())
    series = np.append(prior.values.astype(float), L0)
    noise = float(np.median(np.abs(np.diff(series) / series[:-1])))
    d = float(s.d); tp = s.OptionT1 - s.OptionLtp; sl = L0 - d; t1 = L0 + tp
    pts, why = None, 'EOD'
    after = g[(g.index > t0) & (g.index <= t0.replace(hour=15, minute=15))]
    if len(after) == 0: continue
    for t, L in after.items():
        L = float(L)
        if L >= t1: pts, why = t1 - L0 - COST, 'T1'; break
        if L <= sl: pts, why = L - L0 - COST, 'SL'; break
    if pts is None: pts = float(after.iloc[-1]) - L0 - COST
    rows.append(dict(Id=s.Id, Symbol=s.Symbol, Type=s.SignalType, day=t0.date(), spike=L0 / med - 1, noise=noise, pts=pts, R=pts / d, why=why))
df = pd.DataFrame(rows); print('signals with 5 prior candles + path:', len(df), 'of', len(sg))
df.to_csv('docs/research/2026-11-12-option-noise-gate/signals.csv', index=False)
mid = df.Id.sort_values().iloc[len(df) // 2]; df['half'] = np.where(df.Id < mid, 'H1', 'H2')

def st(t):
    if len(t) == 0: return pd.Series(dict(n=0, net=0., exp=np.nan, PF=np.nan, stop=np.nan))
    w = t.pts[t.pts > 0].sum(); l = -t.pts[t.pts < 0].sum()
    return pd.Series(dict(n=len(t), net=t.pts.sum(), exp=t.pts.mean(), PF=w / l if l else np.nan, stop=(t.why == 'SL').mean()))

print('\nFeature distribution by index (spike %, noise %):')
print(df.groupby('Symbol')[['spike', 'noise']].describe(percentiles=[.5, .8, .9]).T.round(4).loc[lambda x: x.index.get_level_values(1).isin(['50%', '80%', '90%', 'max'])])
print('\nBASELINE by index:'); print(df.groupby('Symbol').apply(st).round(2))

print('\n(a) FIXED SPIKE THRESHOLD (entry premium above 5-candle median). blocked vs kept, by index and half')
for th in (0.01, 0.02, 0.03):
    print(f'\n spike > {th:.0%}')
    for sym in ['ALL', 'NIFTY', 'BANKNIFTY', 'SENSEX']:
        sub = df if sym == 'ALL' else df[df.Symbol == sym]
        for h in ('H1', 'H2'):
            x = sub[sub.half == h]; b = x[x.spike > th]; kp = x[x.spike <= th]
            print(f'  {sym:9s}{h} blocked n={len(b):3d} net={b.pts.sum():8.1f} exp={b.pts.mean() if len(b) else float("nan"):6.2f} | kept n={len(kp):3d} net={kp.pts.sum():8.1f} exp={kp.pts.mean():6.2f}')

print('\n(b) OUT-OF-SAMPLE per-index noise cutoff: fitted on H1 (top q by noise), applied to H2')
for q in (0.10, 0.20, 0.30):
    tot_b = [];
    for sym in ['NIFTY', 'BANKNIFTY', 'SENSEX']:
        h1 = df[(df.Symbol == sym) & (df.half == 'H1')]; h2 = df[(df.Symbol == sym) & (df.half == 'H2')]
        cut = h1.noise.quantile(1 - q); b = h2[h2.noise > cut]; kp = h2[h2.noise <= cut]; tot_b.append(b)
        b1 = h1[h1.noise > cut]
        print(f' q={q:.0%} {sym:9s} cut={cut:.4%}  H1 blocked n={len(b1)} exp={b1.pts.mean() if len(b1) else float("nan"):6.2f} | H2 blocked n={len(b):3d} net={b.pts.sum():8.1f} exp={b.pts.mean() if len(b) else float("nan"):6.2f} | H2 kept n={len(kp):3d} net={kp.pts.sum():8.1f} exp={kp.pts.mean():6.2f}')
    tb = pd.concat(tot_b); h2 = df[df.half == 'H2']
    print(f'   H2 all: blocked net={tb.pts.sum():.1f} (n={len(tb)}); baseline net={h2.pts.sum():.1f}; gate net={h2.pts.sum() - tb.pts.sum():.1f}')

print('\n(c) Does noise predict stops? stop rate by noise tercile within index')
for sym in ['NIFTY', 'BANKNIFTY', 'SENSEX']:
    x = df[df.Symbol == sym].copy(); x['t'] = pd.qcut(x.noise, 3, labels=['low', 'mid', 'high'])
    print(sym); print(x.groupby('t').apply(st).round(2))
print('\n(d) Yesterday-style days: per-day noise median and net pts, SENSEX')
x = df[df.Symbol == 'SENSEX'].groupby('day').agg(n=('pts', 'size'), noise=('noise', 'median'), spike=('spike', 'median'), net=('pts', 'sum')); print(x.round(4))
print('corr(day noise, day net) =', round(x.noise.corr(x.net), 3))
