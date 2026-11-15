"""Per-trade context for the 2026-10-26 BANKNIFTY streak (signals 10284-10290, incl. neighbouring winners).
Snapshots are taken at or before each event (no look-ahead for entry); index path from MarketTicks."""
import sys; sys.path.insert(0, "docs/research/2026-10-28-live-stoploss-monitor-analyze-streak"); from db import q
import pandas as pd; pd.set_option("display.width", 250); pd.set_option("display.max_columns", 40)
s = pd.read_csv("docs/research/2026-10-28-live-stoploss-monitor-analyze-streak/closed-signals.csv", parse_dates=["CreatedOn", "ExitTime"])
s = s[s.Id.between(10284, 10290)]
snap = q("""SELECT SnapshotTime,Symbol,Ltp,DayOpen,YesterdayClose,Ema20,Ema50,Ema20_5m,Ema50_5m,Rsi,Rsi5m,AtrRaw,AtrPct,Vwap,TrendDirection,MarketState,StrategySelected,SignalGenerated,BlockedBy
 FROM IndicatorSnapshots WHERE Symbol='BANKNIFTY' AND CAST(SnapshotTime AS date)='2026-10-26' ORDER BY SnapshotTime""")
tk = q("""SELECT TickTime,LastPrice FROM MarketTicks WITH (NOLOCK) WHERE Symbol IN ('BANKNIFTY','NSE-BANKNIFTY') AND TickTime>='2026-10-26 09:15' AND TickTime<'2026-10-26 15:31' ORDER BY TickTime""")
print("snap rows", len(snap), snap.SnapshotTime.min(), snap.SnapshotTime.max(), "ticks", len(tk), tk.TickTime.min() if len(tk) else None)
o = q("SELECT SignalId,Side,OrderType,OrderPurpose,Status,Price,TriggerPrice,AvgPrice,Quantity,OrderedAt,UpdatedAt FROM Orders WHERE SignalId BETWEEN 10284 AND 10290 ORDER BY OrderedAt")
print(o.to_string())
rows = []
for _, r in s.iterrows():
    e = r.CreatedOn; sn = snap[snap.SnapshotTime <= e]
    a = sn.iloc[-1]; m5 = sn[sn.SnapshotTime <= e - pd.Timedelta(minutes=5)]
    p5 = m5.iloc[-1].Ltp if len(m5) else None
    sgn = 1 if r.OptionType == "CE" else -1
    tw = tk[(tk.TickTime >= e) & (tk.TickTime <= r.ExitTime)]
    fav = (sgn * (tw.LastPrice - r.IdxEntry)).max() if len(tw) else None
    adv = (sgn * (tw.LastPrice - r.IdxEntry)).min() if len(tw) else None
    day_hi = snap[snap.SnapshotTime <= e].Ltp.max(); day_lo = snap[snap.SnapshotTime <= e].Ltp.min()
    rows.append(dict(Id=r.Id, Dir=r.OptionType, Entry=e.strftime("%H:%M:%S"), Exit=r.ExitTime.strftime("%H:%M:%S"),
        MinsHeld=round((r.ExitTime - e).total_seconds() / 60, 1), Pnl=r.Pnl, Exit_=r.ExitReason, SnapAge_s=round((e - a.SnapshotTime).total_seconds()),
        IdxEntry=r.IdxEntry, IdxSL=r.IdxSL, StopPts=round(abs(r.IdxEntry - r.IdxSL), 1), AtrRaw=a.AtrRaw, AtrPct=a.AtrPct,
        Rsi1m=a.Rsi, Rsi5m=a.Rsi5m, Ema20=a.Ema20, Ema50=a.Ema50, E20_5m=a.Ema20_5m, E50_5m=a.Ema50_5m,
        DistEma20_ATR=round((a.Ltp - a.Ema20) / a.AtrRaw, 2), Move5m_ATR=round((a.Ltp - p5) / a.AtrRaw, 2) if p5 else None,
        GapPct=round((a.DayOpen - a.YesterdayClose) / a.YesterdayClose * 100, 2), DayPos=round((a.Ltp - day_lo) / (day_hi - day_lo), 2) if day_hi > day_lo else None,
        Trend=a.TrendDirection, State=a.MarketState, Strat=a.StrategySelected, Vwap=a.Vwap, IdxMFE=fav, IdxMAE=adv,
        OptMFEpct=round((r.MfePrice / r.OptionLtp - 1) * 100, 1), OptMAEpct=round((r.MaePrice / r.OptionLtp - 1) * 100, 1)))
out = pd.DataFrame(rows); out.to_csv("docs/research/2026-10-28-live-stoploss-monitor-analyze-streak/streak-1026.csv", index=False)
print(out.T.to_string())
