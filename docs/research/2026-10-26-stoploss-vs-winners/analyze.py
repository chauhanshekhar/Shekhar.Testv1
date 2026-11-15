"""Stop-loss vs profitable real trades, with the indicator state at entry and the EMA behaviour just after it.

Read-only. Real closed signals (Signals + TradeJournal, summed per SignalId, HERO_ZERO excluded) matched to the
last IndicatorSnapshots minute at or before Signals.CreatedOn (within 3 min) from the backtest-condition snapshots.csv.
Run from the repo root:  python docs/research/2026-10-26-stoploss-vs-winners/analyze.py
"""
import json, pyodbc, pandas as pd, numpy as np, warnings
warnings.filterwarnings('ignore'); pd.set_option('display.width', 250); pd.set_option('display.max_columns', 30)
OUT = 'docs/research/2026-10-26-stoploss-vs-winners/'
SNAP = 'docs/research/2026-10-26-backtest-condition-both/snapshots.csv'
cs = json.load(open('src/Trading.Worker/appsettings.Production.json', encoding='utf-8-sig'))['ConnectionStrings']['DefaultConnection']
k = {a.lower().replace(' ', '').replace('_', ''): b for a, b in (p.split('=', 1) for p in cs.split(';') if '=' in p)}
cn = pyodbc.connect(f"DRIVER={{ODBC Driver 17 for SQL Server}};SERVER={k['server']};DATABASE={k['database']};TrustServerCertificate=yes;UID={k['userid']};PWD={k['password']};", timeout=60)
t = pd.read_sql("""SELECT s.Id,s.Symbol,s.OptionType,s.CreatedOn,s.ExitTime,s.ExitReason,s.MarketState,s.OptionLtp,s.OptionSL,s.EntryPrice idxEntry,s.StopLoss idxStop,
 SUM(j.ProfitLoss) pl, MAX(j.ExitPrice) optExit FROM Signals s WITH (NOLOCK) JOIN TradeJournal j WITH (NOLOCK) ON j.SignalId=s.Id
 WHERE s.OptionType IN ('CE','PE') AND s.Status='CLOSED' AND s.StrategyType<>'HERO_ZERO'
 GROUP BY s.Id,s.Symbol,s.OptionType,s.CreatedOn,s.ExitTime,s.ExitReason,s.MarketState,s.OptionLtp,s.OptionSL,s.EntryPrice,s.StopLoss""", cn)
sn = pd.read_csv(SNAP, parse_dates=['MinuteIst']).sort_values('MinuteIst')
sn['atrpts'] = sn.AtrPct / 100 * sn.Ltp
sn['rel1'] = np.sign(sn.Ema20 - sn.Ema50); sn['rel5'] = np.sign(sn.Ema20_5m - sn.Ema50_5m)
by = {s: g.reset_index(drop=True) for s, g in sn.groupby('Symbol')}
rows = []
for r in t.itertuples():
    g = by.get(r.Symbol)
    if g is None: continue
    i = g.MinuteIst.searchsorted(r.CreatedOn, side='right') - 1
    if i < 0 or (r.CreatedOn - g.MinuteIst.iloc[i]) > pd.Timedelta(minutes=3): continue
    e = g.iloc[i]; call = r.OptionType == 'CE'; sg = 1 if call else -1
    fut = g[(g.MinuteIst > g.MinuteIst.iloc[i]) & (g.MinuteIst <= g.MinuteIst.iloc[i] + pd.Timedelta(minutes=10))]
    same1 = (fut.rel1 == sg).all() if len(fut) else np.nan                       # 1m EMA direction held for 10 min
    flip1 = bool((fut.rel1 != sg).any()) if len(fut) else np.nan                 # 1m EMA direction flipped within 10 min
    flip5 = bool((fut.rel5.dropna() != sg).any()) if fut.rel5.notna().any() else np.nan
    past = g[(g.MinuteIst <= g.MinuteIst.iloc[i]) & (g.MinuteIst > g.MinuteIst.iloc[i] - pd.Timedelta(minutes=30)) & (g.MinuteIst.dt.date == e.MinuteIst.date())]
    cross30 = int((past.rel1 != past.rel1.shift()).sum() - 1) if len(past) > 1 else 0
    atr = e.atrpts
    rows.append(dict(Id=r.Id, Symbol=r.Symbol, Side='CALL' if call else 'PUT', EntryTime=r.CreatedOn, ExitTime=r.ExitTime, ExitReason=r.ExitReason,
        MarketState=r.MarketState, Stop=r.ExitReason in ('OPTION_SL_HIT', 'INDEX_SL_HIT'), PnL=r.pl,
        IdxEntry=r.idxEntry, IdxStop=r.idxStop, OptEntry=r.OptionLtp, OptStop=r.OptionSL, OptExit=r.optExit,
        Rsi5m=e.Rsi5m, Rsi1m=e.Rsi, Pcr=e.Pcr, AtrPct=e.AtrPct,
        Gap1mATR=sg * (e.Ema20 - e.Ema50) / atr, Gap5mATR=sg * (e.Ema20_5m - e.Ema50_5m) / atr if pd.notna(e.Ema20_5m) else np.nan,
        Align5m=(e.rel5 == sg) if pd.notna(e.rel5) else np.nan,
        StretchATR=sg * (e.Ltp - e.Ema20) / atr, DayChangePct=e.PriceChangePct, Cross30=cross30,
        Flip1mWithin10m=flip1, Flip5mWithin10m=flip5,
        MinutesHeld=(r.ExitTime - r.CreatedOn).total_seconds() / 60 if pd.notna(r.ExitTime) else np.nan, Hour=r.CreatedOn.hour))
d = pd.DataFrame(rows); d.to_csv(OUT + 'trades-with-entry-state.csv', index=False)
print(len(d), 'of', len(t), 'real trades matched to a snapshot;', int(d.Stop.sum()), 'stop-loss exits')

feat = ['Rsi5m', 'Gap1mATR', 'Gap5mATR', 'StretchATR', 'Pcr', 'AtrPct', 'DayChangePct', 'Cross30', 'MinutesHeld']
def cmp(g, label):
    print('\n=====', label, f'(n={len(g)}, stops={int(g.Stop.sum())}, others={int((~g.Stop).sum())})')
    s, o = g[g.Stop], g[~g.Stop]; res = []
    for f in feat:
        a, b = s[f].dropna(), o[f].dropna()
        if len(a) < 5 or len(b) < 5: continue
        # Mann-Whitney U effect (probability a random stop trade has a higher value than a random other trade)
        ranks = pd.concat([a, b]).rank(); u = ranks.iloc[:len(a)].sum() - len(a) * (len(a) + 1) / 2
        auc = u / (len(a) * len(b)); res.append((f, len(a), round(a.median(), 3), round(b.median(), 3), round(auc, 2)))
    print(pd.DataFrame(res, columns=['feature', 'n_stop', 'median_stop', 'median_other', 'AUC(0.5=no difference)']).to_string(index=False))
    for f in ['Align5m', 'Flip1mWithin10m', 'Flip5mWithin10m']:
        x = g.dropna(subset=[f]); 
        if len(x): print(f, x.groupby(f).Stop.agg(['size', 'mean']).round(2).to_dict('index'))
    print('stop rate by MarketState'); print(g.groupby('MarketState').Stop.agg(['size', 'mean']).round(2).T.to_string())
for sd in ['PUT', 'CALL']:
    cmp(d[d.Side == sd], sd)
