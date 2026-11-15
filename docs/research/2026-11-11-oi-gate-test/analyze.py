# OI-gate replay on the entries the replayed gates let through (2026-11-11). Run from the repo root.
# Live rule (OiConfirmationGate): CALL confirmed iff (CE OI change > 0 or < 0) AND last-1m price change > 0 (PUT: PE, price inverted).
# IsLongBuildUp = oi>0 & price>0, IsShortCovering = oi<0 & price>0, so OI only matters when its change is exactly 0.
# Entry set = Trading.Backtest CALL/PUT Legacy trades (live rules, OI gate NOT replayed), costed. Outcome = net rupees after costs.
import pandas as pd, numpy as np
D = 'docs/research/2026-11-11-oi-gate-test/'
o = pd.read_csv(D + 'oi.csv', parse_dates=['MinuteTime'])
c = pd.read_csv(D + 'candles.csv', parse_dates=['CandleTime'])
c['t'] = c.CandleTime + pd.Timedelta(hours=5, minutes=30)           # candle times are UTC; OI history and trades are IST
c['Symbol'] = c.Symbol.str.replace('_1m', '')
t = pd.read_csv('docs/research/2026-11-11-t2-partial/trades.csv', parse_dates=['EntryIst'])
t = t[t.Configuration.isin(['CALL|Exit|Legacy', 'PUT|Exit|Legacy']) & (t.EntryIst >= '2026-10-07') & t.Symbol.isin(['NIFTY', 'BANKNIFTY', 'SENSEX'])].copy()
t['side'] = t.Configuration.str[:4].str.rstrip('|'); t['net'] = t.PnlRs - t.CostRs
step = {s: int(np.diff(np.sort(g.StrikePrice.unique())).min()) for s, g in o.groupby('Symbol')}
oi = {k: g for k, g in o.groupby(['Symbol', 'MinuteTime'])}
cs = {s: g.sort_values('t') for s, g in c.groupby('Symbol')}

def feats(r):
    sym, e = r.Symbol, r.EntryIst
    g = cs[sym]; g = g[g.t < e].tail(2)
    price = g.Close.iloc[-1] - g.Close.iloc[-2] if len(g) == 2 else np.nan
    cur = oi.get((sym, e - pd.Timedelta(minutes=1))); prv = oi.get((sym, e - pd.Timedelta(minutes=2)))
    out = {'price': price}
    if cur is None or prv is None:
        return pd.Series({**out, 'oi2': np.nan, 'oi5': np.nan})
    spot = cur.UnderlyingLtp.iloc[0]; atm = round(spot / step[sym]) * step[sym]; ot = 'CE' if r.side == 'CALL' else 'PE'
    for n in (2, 5):
        k = lambda d: d[(d.OptionType == ot) & ((d.StrikePrice - atm).abs() <= step[sym] * n)].set_index('StrikePrice').OpenInterest
        a, b = k(cur), k(prv)
        out[f'oi{n}'] = float((a - b.reindex(a.index).fillna(a)).sum())
    return pd.Series(out)

f = t.apply(feats, axis=1); t = pd.concat([t, f], axis=1)
t['dir'] = np.where(t.side == 'CALL', t.price, -t.price)
t = t.dropna(subset=['price', 'oi2'])
t['live_oi2'] = (t.dir > 0) & (t.oi2 != 0)
t['wide_oi5'] = (t.dir > 0) & (t.oi5 != 0)
t['price_only'] = t.dir > 0
t['strict_oi2'] = (t.dir > 0) & (((t.side == 'CALL') & (t.oi2 > 0)) | ((t.side == 'PUT') & (t.oi2 > 0)))   # OI building only
mid = t.EntryIst.sort_values().iloc[len(t) // 2]

def pf(x):
    w, l = x[x > 0].sum(), -x[x < 0].sum()
    return round(w / l, 2) if l > 0 else np.nan

rows = []
def report(g, label):
    for v in ['live_oi2', 'wide_oi5', 'price_only', 'strict_oi2']:
        for passed in (True, False):
            x = g[g[v] == passed].net
            rows.append({'scope': label, 'variant': v, 'group': 'PASS' if passed else 'REJECT', 'n': len(x),
                         'net': round(x.sum()), 'avg': round(x.mean()) if len(x) else np.nan, 'pf': pf(x) if len(x) else np.nan,
                         'early_net': round(g[(g[v] == passed) & (g.EntryIst < mid)].net.sum()),
                         'late_net': round(g[(g[v] == passed) & (g.EntryIst >= mid)].net.sum())})
for side in ('CALL', 'PUT'):
    report(t[t.side == side], f'{side} ALL')
    for sym in ('NIFTY', 'BANKNIFTY', 'SENSEX'):
        report(t[(t.side == side) & (t.Symbol == sym)], f'{side} {sym}')
r = pd.DataFrame(rows); r.to_csv(D + 'results.csv', index=False)
pd.set_option('display.width', 200)
print(f'entries replayed: {len(t)} (CALL {int((t.side=="CALL").sum())}, PUT {int((t.side=="PUT").sum())}); split at {mid}')
print('share of entries where OI change was exactly 0 (the only case where OI matters):',
      round((t.oi2 == 0).mean() * 100, 1), '%')
print(r.to_string(index=False))
