-- Why does BANKNIFTY trade 2 lots while NIFTY/SENSEX trade 4? Read-only.
-- Code sizes every symbol the same: AdaptiveLotMin (capped by MaxLotSize) -> Signals.Quantity (lots).
-- Broker quantity = lots x TradingInstruments.LotSize. Splits only come from data/config.
DECLARE @Days int = 5;

-- 1. Contracts per lot per symbol
SELECT Symbol, LotSize, IsActive FROM TradingInstruments ORDER BY Symbol;

-- 2. Lots chosen per signal vs. what was ordered
SELECT s.Symbol, s.Id, s.CreatedOn, s.Quantity AS Lots,
       o.Quantity AS OrderQty, o.FilledQty, o.Status
FROM Signals s
LEFT JOIN Orders o ON o.SignalId = s.Id
WHERE s.CreatedOn >= DATEADD(day, -@Days, SYSDATETIME())
ORDER BY s.Symbol, s.CreatedOn DESC;

-- 3. Any per-symbol sizing overrides
SELECT SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM StrategySettings
WHERE SettingKey LIKE '%Lot%' OR SettingKey LIKE '%Quantity%'
ORDER BY SettingKey, Symbol;
