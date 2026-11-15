-- Read-only. Signals of the last 3 sessions with journal P&L and exit detail.
DECLARE @From datetime2 = '2026-10-26';
SELECT s.Id, s.Symbol, s.OptionType, CONVERT(varchar(19), s.CreatedOn, 120) AS CreatedOn,
       CONVERT(varchar(19), s.ExitTime, 120) AS ExitTime, s.ExitReason, s.Status, s.Quantity,
       s.EntryPrice, s.StopLoss, s.Target1, s.OptionLtp, s.OptionSL, s.OptionT1, s.MfePrice, s.MaePrice,
       s.ProtectedSL, s.IndexT1Hit, s.StopLossTriggerPrice, s.StopLossStatus,
       s.Rsi5mAtEntry, s.RsiAtEntry, s.PcrAtEntry, s.DeltaAtEntry, s.AtrPctAtEntry, s.AtrRawAtEntry,
       s.MarketState, s.SelectedStrategy, s.IsExpiryDay, s.Confidence,
       j.Pnl, j.Rows, j.ExitAvg
FROM Signals s WITH (NOLOCK)
OUTER APPLY (SELECT SUM(tj.ProfitLoss) AS Pnl, COUNT(*) AS Rows,
                    SUM(tj.ExitPrice*tj.Quantity)/NULLIF(SUM(tj.Quantity),0) AS ExitAvg
             FROM TradeJournal tj WITH (NOLOCK) WHERE tj.SignalId = s.Id) j
WHERE s.CreatedOn >= @From AND ISNULL(s.StrategyType,'') <> 'HERO_ZERO'
ORDER BY s.CreatedOn;
