"""List actual closed signals (last @days) with summed TradeJournal P&L; find consecutive stop-loss losses.
A trade is a stop-loss loss only if ExitReason ends in SL_HIT AND summed journal P&L < 0."""
import sys; sys.path.insert(0, "docs/research/2026-10-28-live-stoploss-monitor-analyze-streak"); from db import q
import pandas as pd
d = q("""SELECT s.Id,s.Symbol,s.OptionType,s.CreatedOn,s.ExitTime,s.EntryPrice IdxEntry,s.StopLoss IdxSL,s.Target1 IdxT1,
 s.OptionLtp,s.OptionSL,s.MfePrice,s.MaePrice,s.ExitReason,s.Rsi5mAtEntry,s.AtrPctAtEntry,s.AtrRawAtEntry,s.MarketState,s.SelectedStrategy,
 (SELECT SUM(ProfitLoss) FROM TradeJournal j WHERE j.SignalId=s.Id) Pnl
 FROM Signals s WHERE s.CreatedOn>=DATEADD(day,-14,GETDATE()) AND s.Status='CLOSED' ORDER BY s.ExitTime""")
d["SLloss"] = d.ExitReason.fillna("").str.endswith("SL_HIT") & (d.Pnl < 0)
d["grp"] = (~d.SLloss).cumsum()
st = d[d.SLloss].groupby("grp").agg(n=("Id", "size"), first=("Id", "min"), last=("Id", "max"), start=("CreatedOn", "min"),
                                     loss=("Pnl", "sum"), syms=("Symbol", lambda s: ",".join(sorted(set(s)))))
print(st[st.n >= 2].to_string())
d.to_csv("docs/research/2026-10-28-live-stoploss-monitor-analyze-streak/closed-signals.csv", index=False)
print(len(d), d.CreatedOn.min(), d.CreatedOn.max(), "wins", (d.Pnl > 0).sum(), "SLloss", d.SLloss.sum(), "net", d.Pnl.sum())
