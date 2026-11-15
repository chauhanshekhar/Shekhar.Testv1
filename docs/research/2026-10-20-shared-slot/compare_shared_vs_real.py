"""Shared-slot replay (CALL+PUT, one position, costed) vs real closed trades since 2026-10-08. Read-only.
Inputs: signals.csv (replay log, Scope=ALL), real-signals.csv (Signals + TradeJournal export)."""
import csv, collections, datetime as dt

D = 'docs/research/2026-10-20-shared-slot/'
REAL = 'docs/research/2026-10-20-backtest-condition-regime-replay/real-signals.csv'
FROM = '2026-10-08'
WIN = 3
f = lambda s: dt.datetime.strptime(s[:16], '%Y-%m-%d %H:%M')
num = lambda v: float(v) if v not in (None, '', 'NULL') else None

log = [x for x in csv.DictReader(open(D + 'signals.csv')) if x['Scope'] == 'ALL' and x['TimeIst'][:10] >= FROM]
sim = []
for x in log:
    if x['Outcome'] == 'ENTERED':
        sim.append(dict(side=x['Side'], sym=x['Symbol'], t=f(x['TimeIst']), te=f(x['ExitIst']), reason=x['ExitReason'],
                        gross=float(x['PnlRs']), cost=float(x['CostRs'] or 0)))
real = []
for r in csv.DictReader(open(REAL, encoding='utf-8-sig')):
    if r['CreatedOnIst'][:10] < FROM: continue
    real.append(dict(id=r['Id'], sym=r['Symbol'], side=r['Direction'], t=f(r['CreatedOnIst']),
                     te=f(r['LastExitOn']) if r['LastExitOn'] not in ('', 'NULL') else None,
                     reason=r['ExitReason'], pnl=num(r['Pnl']) or 0.0))

used = set(); pairs = []; sim_only = []
for s in sorted(sim, key=lambda z: z['t']):
    best = None
    for i, r in enumerate(real):
        if i in used or r['side'] != s['side'] or r['sym'] != s['sym']: continue
        d = abs((r['t'] - s['t']).total_seconds()) / 60
        if d <= WIN and (best is None or d < best[0]): best = (d, i)
    if best: used.add(best[1]); pairs.append((s, real[best[1]]))
    else: sim_only.append(s)
real_only = [r for i, r in enumerate(real) if i not in used]

sim_net = sum(s['gross'] - s['cost'] for s in sim); real_net = sum(r['pnl'] for r in real)
print(f"since {FROM}: simulated (shared slot, costed) {len(sim)} trades net {sim_net:,.0f} (gross {sum(s['gross'] for s in sim):,.0f}, costs {sum(s['cost'] for s in sim):,.0f})")
print(f"             real journal (no costs)        {len(real)} trades net {real_net:,.0f}")
print(f"matched {len(pairs)} | sim-only {len(sim_only)} | live-only {len(real_only)}")
print(f"gross basis: sim {sum(s['gross'] for s in sim):,.0f} vs real {real_net:,.0f}\n")
mg = sum(s['gross'] for s, r in pairs); mr = sum(r['pnl'] for s, r in pairs)
print(f"matched pairs: sim gross {mg:,.0f} vs real {mr:,.0f}; same exit reason {sum(1 for s,r in pairs if s['reason']==r['reason'])} of {len(pairs)}")
print(f"sim-only gross {sum(s['gross'] for s in sim_only):,.0f} (n={len(sim_only)}) | live-only {sum(r['pnl'] for r in real_only):,.0f} (n={len(real_only)})\n")

# what did the replay log say at each live-only trade's minute?
idx = collections.defaultdict(list)
for x in log: idx[(x['Symbol'], x['Side'])].append((f(x['TimeIst']), x))
print("Live-only trades: what the replay recorded at that minute (+-1)")
cnt = collections.Counter(); amt = collections.defaultdict(float)
rows = []
for r in sorted(real_only, key=lambda r: r['t']):
    near = [(t, x) for t, x in idx[(r['sym'], r['side'])] if abs((t - r['t']).total_seconds()) <= 60]
    if not near: k = 'no replay signal at that minute'; extra = ''
    else:
        x = min(near, key=lambda z: abs((z[0] - r['t']).total_seconds()))[1]
        k = x['Outcome']; extra = f" holder={x['OpenSymbol'] or '-'} until {x['HeldUntilIst'] or '-'}"
    cnt[k] += 1; amt[k] += r['pnl']; rows.append((r, k, extra))
for r, k, extra in rows:
    print(f"  {r['t']:%m-%d %H:%M} {r['side']:<4} {r['sym']:<9} live {r['pnl']:>7,.0f} ({r['reason']:<16}) -> replay: {k}{extra}")
print()
for k, n in cnt.most_common(): print(f"  {n:>3} trades live P&L {amt[k]:>8,.0f}  {k}")

print("\nSim-only trades: did live record a block at that minute? (see simonly_blocked_by earlier) -> listing sim-only by day")
bd = collections.defaultdict(list)
for s in sim_only: bd[s['t'].date()].append(s)
for d in sorted(bd): print(f"  {d}: " + ", ".join(f"{s['t']:%H:%M} {s['side'][0]} {s['sym'][:2]} {s['gross']-s['cost']:+,.0f}" for s in bd[d]))

print("\nFirst divergence per day")
ev = [(min(s['t'], r['t']), 'matched', s, r) for s, r in pairs] + [(s['t'], 'SIM only', s, None) for s in sim_only] + [(r['t'], 'LIVE only', None, r) for r in real_only]
ev.sort(key=lambda e: e[0])
byd = collections.defaultdict(list)
for e in ev: byd[e[0].date()].append(e)
for d in sorted(byd):
    L = byd[d]; first = next((e for e in L if e[1] != 'matched'), None)
    nm = sum(1 for e in L if e[1] == 'matched')
    if first is None: print(f"  {d}: identical ({nm} matched)"); continue
    x = first[2] or first[3]
    before = [e for e in L if e[1] == 'matched' and e[0] < first[0]]
    print(f"  {d}: first divergence {first[0]:%H:%M} {first[1]} {x['side']} {x['sym']} (matched before it: {len(before)}; matched that day: {nm}/{len(L)} events)")
