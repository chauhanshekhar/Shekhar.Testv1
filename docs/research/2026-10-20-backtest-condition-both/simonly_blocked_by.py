"""For each simulated-only trade (no live match since 2026-10-08), read what the LIVE system recorded at that minute:
IndicatorSnapshots.BlockedBy / BlockedReason / MarketState / StrategySelected. Read-only SELECT via sqlcmd (password from SQLCMDPASSWORD)."""
import csv, os, re, subprocess, sys, collections, datetime as dt, importlib.util

D = 'docs/research/2026-10-20-backtest-condition-both/'
spec = importlib.util.spec_from_file_location('cmp', D + 'compare_sim_real.py')
src = open(D + 'compare_sim_real.py', encoding='utf-8').read().split('print(f"Period')[0]
ns = {}
exec(src, ns)
sim_only = ns['sim_only']

vals = []
for s in sim_only:
    syms = s['sym'].split('/') if s['sym'] not in ('?',) else ['BANKNIFTY', 'SENSEX', 'NIFTY']
    for sym in syms:
        vals.append((sym, s['t'], s['side'], s['gross'] - s['cost'], s['sym']))

rows = []
for i, (sym, t, side, net, orig) in enumerate(vals):
    q = (f"SET NOCOUNT ON; SELECT TOP 1 '{sym}','{t:%Y-%m-%d %H:%M}','{side}',{net:.0f},'{orig}', "
         f"ISNULL(BlockedBy,''), ISNULL(LEFT(BlockedReason,90),''), ISNULL(MarketState,''), ISNULL(StrategySelected,'') "
         f"FROM IndicatorSnapshots WITH (NOLOCK) WHERE Symbol='{sym}' "
         f"AND SnapshotTime BETWEEN DATEADD(minute,-1,'{t:%Y-%m-%d %H:%M}') AND DATEADD(minute,2,'{t:%Y-%m-%d %H:%M}') "
         f"ORDER BY ABS(DATEDIFF(second,SnapshotTime,'{t:%Y-%m-%d %H:%M}'));")
    out = subprocess.run(['sqlcmd', '-S', '13.206.229.15,1433', '-d', 'TradingDB_Paper', '-U', 'sa', '-C', '-l', '30',
                          '-h', '-1', '-W', '-s', '|', '-Q', q], capture_output=True, text=True,
                         env=dict(os.environ)).stdout.strip()
    rows.append((sym, t, side, net, orig, out))

by = collections.defaultdict(lambda: [0, 0.0])
print('simulated-only trades and what live recorded at that minute (BlockedBy):\n')
for sym, t, side, net, orig, out in rows:
    parts = out.split('|')
    blocked = parts[5] if len(parts) > 5 else 'NO SNAPSHOT'
    if orig == '?' or '/' in orig:
        pass
    print(f"{t:%m-%d %H:%M} {side:<4} {sym:<9} sim net {net:>8,.0f}  live: {blocked or '(no block recorded)'}  | {parts[6] if len(parts)>6 else ''} | state={parts[7] if len(parts)>7 else ''}")
    by[blocked or '(no block recorded)'][0] += 1
    by[blocked or '(no block recorded)'][1] += net
print('\nsummary by live BlockedBy (rows for ambiguous-symbol trades are counted once per candidate symbol):')
for k, (n, p) in sorted(by.items(), key=lambda z: -z[1][0]):
    print(f"  {k:<28} n={n:>3}  sim net {p:>9,.0f}")
