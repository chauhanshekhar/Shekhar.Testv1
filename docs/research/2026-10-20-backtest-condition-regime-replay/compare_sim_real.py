"""Trade-by-trade: simulated (Trading.Backtest, one-position 'ALL' scope per side, costed) vs real closed trades, from 2026-10-08.
Read-only. Inputs: trades.csv (backtest dump), real-signals.csv (Signals + TradeJournal export)."""
import csv, collections, datetime as dt, sys

D = 'docs/research/2026-10-20-backtest-condition-regime-replay/'
FROM = '2026-10-08'
WIN = 3   # minutes: sim decision minute vs live signal CreatedOn

f = lambda s: dt.datetime.strptime(s[:16], '%Y-%m-%d %H:%M')
num = lambda v: float(v) if v not in (None, '', 'NULL') else None

sim = []
for x in csv.DictReader(open(D + 'trades.csv')):
    if x['Symbol'] != 'ALL' or x['Configuration'] not in ('CALL|Baseline', 'PUT|Baseline'):
        continue
    if x['EntryIst'][:10] < FROM:
        continue
    sim.append(dict(side=x['Configuration'][:x['Configuration'].index('|')], t=f(x['EntryIst']), te=f(x['ExitIst']),
                    reason=x['ExitReason'], gross=float(x['PnlRs']), cost=float(x['CostRs'] or 0),
                    src=x['PremiumSource'], sym=None))
# the ALL scope does not name the symbol; recover it from the per-symbol run (same entry minute, same side)
persym = collections.defaultdict(list)
for x in csv.DictReader(open(D + 'trades.csv')):
    if x['Symbol'] in ('BANKNIFTY', 'SENSEX', 'NIFTY') and x['Configuration'] in ('CALL|Baseline', 'PUT|Baseline'):
        persym[(x['Configuration'][:x['Configuration'].index('|')], x['EntryIst'][:16])].append(x['Symbol'])
for s in sim:
    c = persym.get((s['side'], s['t'].strftime('%Y-%m-%d %H:%M')), [])
    s['sym'] = c[0] if len(c) == 1 else ('/'.join(c) if c else '?')

real = []
for r in csv.DictReader(open(D + 'real-signals.csv', encoding='utf-8-sig')):
    if r['CreatedOnIst'][:10] < FROM:
        continue
    real.append(dict(id=r['Id'], sym=r['Symbol'], side=r['Direction'], t=f(r['CreatedOnIst']),
                     te=f(r['LastExitOn']) if r['LastExitOn'] not in ('', 'NULL') else None,
                     reason=r['ExitReason'], pnl=num(r['Pnl']) or 0.0, entry=num(r['EntryPriceAvg']),
                     sig=num(r['OptionLtp']), exitp=num(r['ExitPriceAvg']), qty=num(r['TotalQty'])))

# greedy nearest match on symbol + side within WIN minutes
used = set(); pairs = []; sim_only = []
for s in sorted(sim, key=lambda z: z['t']):
    best = None
    for i, r in enumerate(real):
        if i in used or r['side'] != s['side'] or r['sym'] != s['sym']:
            continue
        d = abs((r['t'] - s['t']).total_seconds()) / 60
        if d <= WIN and (best is None or d < best[0]):
            best = (d, i)
    if best:
        used.add(best[1]); pairs.append((s, real[best[1]]))
    else:
        sim_only.append(s)
real_only = [r for i, r in enumerate(real) if i not in used]

def busy(t):
    """Was a real position open at time t (any symbol/side)?"""
    return any(r['te'] and r['t'] <= t < r['te'] for r in real)

print(f"Period {FROM}..; simulated trades {len(sim)}, real trades {len(real)}")
print(f"matched {len(pairs)} | simulated-only {len(sim_only)} | real-only {len(real_only)}\n")

tot = lambda L, k: sum(z[k] for z in L)
sim_net = sum(s['gross'] - s['cost'] for s in sim)
real_net = tot(real, 'pnl')
print(f"SIM net (costed) {sim_net:,.0f}  gross {tot(sim,'gross'):,.0f}  costs {tot(sim,'cost'):,.0f}")
print(f"REAL net (journal, no costs) {real_net:,.0f}")
print(f"GAP (sim - real) = {sim_net - real_net:,.0f}\n")

m_sim_g = sum(s['gross'] for s, r in pairs); m_sim_c = sum(s['cost'] for s, r in pairs); m_real = sum(r['pnl'] for s, r in pairs)
so_g = sum(s['gross'] - s['cost'] for s in sim_only); ro = sum(r['pnl'] for r in real_only)
print("Decomposition of the gap (simulated minus real):")
print(f"  1. simulated trades with no live match   {so_g:>10,.0f}   (n={len(sim_only)})")
print(f"  2. real trades with no simulated match   {-ro:>10,.0f}   (n={len(real_only)}; sign flipped = real P&L not in sim)")
print(f"  3. matched trades, costs the sim charges {-m_sim_c:>10,.0f}   (real journal charges none)")
print(f"  4. matched trades, gross P&L difference  {m_sim_g - m_real:>10,.0f}   (entry fill + exit behaviour + settings; n={len(pairs)})")
print(f"     check: {so_g - ro - m_sim_c + (m_sim_g - m_real):,.0f} vs gap {sim_net - real_net:,.0f}\n")

print("Why simulated-only trades did not happen live:")
why = collections.Counter(); whyp = collections.defaultdict(float)
for s in sim_only:
    k = 'a real position (any symbol/side) was open' if busy(s['t']) else 'no real position open: live gates/settings/data blocked it'
    why[k] += 1; whyp[k] += s['gross'] - s['cost']
for k, v in why.items():
    print(f"  {k}: {v} trades, sim net {whyp[k]:,.0f}")

print("\nBy side and symbol (matched / sim-only / real-only: n and P&L):")
keys = sorted({(z['side'], z['sym']) for z in sim} | {(z['side'], z['sym']) for z in real})
print(f"  {'':<16}{'matched n':>10}{'sim P&L':>10}{'real P&L':>10}{'diff':>9} | {'sim-only n':>10}{'net':>9} | {'real-only n':>11}{'net':>9}")
for k in keys:
    P = [(s, r) for s, r in pairs if (s['side'], s['sym']) == k]
    SO = [s for s in sim_only if (s['side'], s['sym']) == k]
    RO = [r for r in real_only if (r['side'], r['sym']) == k]
    sg = sum(s['gross'] for s, r in P); rp = sum(r['pnl'] for s, r in P)
    print(f"  {k[0]+' '+k[1]:<16}{len(P):>10}{sg:>10,.0f}{rp:>10,.0f}{sg-rp:>9,.0f} | {len(SO):>10}{sum(s['gross']-s['cost'] for s in SO):>9,.0f} | {len(RO):>11}{sum(r['pnl'] for r in RO):>9,.0f}")

print("\nMatched pairs: exit reason agreement and entry fill vs signal price")
agree = sum(1 for s, r in pairs if s['reason'] == r['reason'])
print(f"  same exit reason: {agree} of {len(pairs)}")
ct = collections.Counter((s['reason'], r['reason']) for s, r in pairs if s['reason'] != r['reason'])
for (a, b), n in ct.most_common(8):
    print(f"    sim {a} vs real {b}: {n}")
slip = [r['entry'] - r['sig'] for s, r in pairs if r['entry'] and r['sig']]
print(f"  real entry fill - signal price: nonzero in {sum(1 for z in slip if abs(z) > 0.001)} of {len(slip)} (paper fills at the quote)")
print(f"  premium source in matched sim trades: {dict(collections.Counter(s['src'] for s, r in pairs))}")

print("\nWorst matched differences (sim gross - real P&L):")
for s, r in sorted(pairs, key=lambda p: p[0]['gross'] - p[1]['pnl'], reverse=True)[:8]:
    print(f"  {s['t']:%m-%d %H:%M} {s['side']} {s['sym']:<9} sim {s['gross']:>8,.0f} ({s['reason']}) vs real {r['pnl']:>8,.0f} ({r['reason']}) id {r['id']}")
