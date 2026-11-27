"""Reads every <run>/backtest-trades.csv, writes ablation.csv and prints the tables. All arithmetic is here."""
import csv, os, sys

ROOT = os.path.dirname(os.path.abspath(__file__))


def load(run):
    p = os.path.join(ROOT, run, "backtest-trades.csv")
    if not os.path.exists(p):
        return None
    with open(p, encoding="utf-8-sig") as f:
        rows = [r for r in csv.DictReader(f) if r["Date"][:2] == "20" and r["Symbol"]]
    for r in rows:
        r["net"] = float(r["NetRs"])
        r["gross"] = float(r["PnlRs"])
    rows.sort(key=lambda r: (r["Date"], r["ExitIst"]))
    return rows


def metrics(rows):
    n = len(rows)
    if n == 0:
        return dict(n=0, win=0, gp=0, gl=0, net=0, pf=0, exp=0, dd=0, aw=0, al=0)
    wins = [r["net"] for r in rows if r["net"] > 0]
    loss = [r["net"] for r in rows if r["net"] <= 0]
    gp, gl = sum(wins), -sum(loss)
    cum = peak = dd = 0.0
    for r in rows:
        cum += r["net"]
        peak = max(peak, cum)
        dd = max(dd, peak - cum)
    return dict(n=n, win=100 * len(wins) / n, gp=gp, gl=gl, net=gp - gl,
                pf=(gp / gl if gl else float("inf")), exp=(gp - gl) / n, dd=dd,
                aw=(gp / len(wins) if wins else 0), al=(-gl / len(loss) if loss else 0))


def key(r):
    return (r["Date"], r["EntryIst"], r["Symbol"], r["Side"])


runs = sorted(d for d in os.listdir(ROOT) if d.startswith("off-") and os.path.isdir(os.path.join(ROOT, d)))
base = load("baseline")
out = []
slices = [("ALL", lambda r: True), ("CALL", lambda r: r["Side"] == "CALL"), ("PUT", lambda r: r["Side"] == "PUT")]
for s in sorted({r["Symbol"] for r in base}):
    slices.append((s, lambda r, s=s: r["Symbol"] == s))

for name, rows in [("baseline", base)] + [(r, load(r)) for r in runs]:
    if rows is None:
        out.append(dict(run=name, slice="ALL", error="no trades file"))
        continue
    changed = len(set(map(key, rows)) ^ set(map(key, base))) if name != "baseline" else 0
    same_pnl = name != "baseline" and [round(r["net"], 2) for r in rows] != [round(r["net"], 2) for r in base]
    for sname, f in slices:
        m = metrics([r for r in rows if f(r)])
        m.update(run=name, slice=sname, changed=changed, pnl_differs=same_pnl)
        out.append(m)

fields = ["run", "slice", "changed", "pnl_differs", "n", "win", "gp", "gl", "net", "pf", "exp", "dd", "aw", "al", "error"]
with open(os.path.join(ROOT, "ablation.csv"), "w", newline="") as f:
    w = csv.DictWriter(f, fieldnames=fields)
    w.writeheader()
    for o in out:
        w.writerow({k: (round(v, 2) if isinstance(v, float) else v) for k, v in o.items() if k in fields})

b = {o["slice"]: o for o in out if o["run"] == "baseline"}
print("BASELINE")
for s, o in b.items():
    print(f"  {s:9} n={o['n']:3} win={o['win']:5.1f}% net={o['net']:10.0f} PF={o['pf']:5.2f} exp={o['exp']:8.0f} DD={o['dd']:8.0f}")
print()
print(f"{'switch':40} {'trdsΔ':>5} {'winΔ':>6} {'netΔ':>9} {'PFΔ':>6} {'DDΔ':>8}  verdict")
res = []
for o in out:
    if o["run"] in ("baseline",) or o["slice"] != "ALL":
        continue
    if "error" in o:
        res.append((0, f"{o['run']:40} ERROR")); continue
    d = b["ALL"]
    dn, dpf, ddd, dw, dt = o["net"] - d["net"], o["pf"] - d["pf"], o["dd"] - d["dd"], o["win"] - d["win"], o["n"] - d["n"]
    if o["changed"] == 0 and not o["pnl_differs"]:
        v = "no trades affected"
    elif o["changed"] < 5 and not o["pnl_differs"]:
        v = "TOO FEW TRADES"
    elif dn > 0 and dpf > 0 and ddd <= 0:
        v = "HURTS - candidate to remove"
    elif dn < 0 or dpf < 0 and ddd >= 0:
        v = "HELPS - keep" if dn < 0 and dpf <= 0 else "MIXED"
    else:
        v = "MIXED"
    if o["changed"] < 5 and v not in ("no trades affected",):
        v = "TOO FEW (" + str(o["changed"]) + " trades differ) " + v
    res.append((dn, f"{o['run'][4:]:40} {dt:5} {dw:6.1f} {dn:9.0f} {dpf:6.2f} {ddd:8.0f}  {v}"))
for _, line in sorted(res, key=lambda x: x[0]):
    print(line)
