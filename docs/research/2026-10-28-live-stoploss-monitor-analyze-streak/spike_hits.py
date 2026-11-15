"""Closed signals whose holding window contains an index spike tick (tick-spikes.csv), with the exit reason."""
import sys; sys.path.insert(0, "docs/research/2026-10-28-live-stoploss-monitor-analyze-streak"); from db import q
import pandas as pd
F = "docs/research/2026-10-28-live-stoploss-monitor-analyze-streak/"
sp = pd.read_csv(F + "tick-spikes.csv", parse_dates=["TickTime"])
s = q("""SELECT s.Id,s.Symbol,s.OptionType,s.CreatedOn,s.ExitTime,s.ExitReason,s.IndexT1Hit,s.Target1,s.StopLoss,s.OptionLtp,s.ProtectedSL,
 (SELECT SUM(ProfitLoss) FROM TradeJournal j WHERE j.SignalId=s.Id) Pnl FROM Signals s WHERE s.Status='CLOSED' AND s.CreatedOn>='2026-10-22'""")
rows = []
for _, r in s.iterrows():
    h = sp[(sp.Symbol == r.Symbol) & (sp.TickTime >= r.CreatedOn) & (sp.TickTime <= r.ExitTime + pd.Timedelta(seconds=2))]
    for _, x in h.iterrows():
        up = x.LastPrice > x.prev
        hitT1 = (r.OptionType == "CE" and x.LastPrice >= r.Target1) or (r.OptionType == "PE" and x.LastPrice <= r.Target1)
        hitSL = (r.OptionType == "CE" and x.LastPrice <= r.StopLoss) or (r.OptionType == "PE" and x.LastPrice >= r.StopLoss)
        rows.append(dict(Id=r.Id, Sym=r.Symbol, Dir=r.OptionType, Spike=x.TickTime.strftime("%m-%d %H:%M:%S"), Exit=r.ExitTime.strftime("%H:%M:%S"),
                         SpikePx=x.LastPrice, Prev=x.prev, CrossesT1=hitT1, CrossesIdxSL=hitSL, ExitReason=r.ExitReason, IndexT1Hit=r.IndexT1Hit, Pnl=r.Pnl))
print(pd.DataFrame(rows).to_string() if rows else "none", "\nsignals checked", len(s))
