"""Read-only. Today's (2026-10-30) real signals with journal P&L, plus index 1m candles around each.
Run from repo root: python docs/research/2026-10-30-live-stoploss-monitor-today/pull.py"""
import json, pyodbc, pandas as pd
pd.set_option('display.width',300); pd.set_option('display.max_columns',40)
OUT='docs/research/2026-10-30-live-stoploss-monitor-today/'
cs=json.load(open('src/Trading.Worker/appsettings.Production.json',encoding='utf-8-sig'))['ConnectionStrings']['DefaultConnection']
k={a.lower().replace(' ','').replace('_',''):b for a,b in (p.split('=',1) for p in cs.split(';') if '=' in p)}
cn=pyodbc.connect(f"DRIVER={{ODBC Driver 17 for SQL Server}};SERVER={k['server']};DATABASE={k['database']};TrustServerCertificate=yes;UID={k['userid']};PWD={k['password']};",timeout=60)
s=pd.read_sql("""SELECT s.Id,s.Symbol,s.OptionType,s.StrategyType,CONVERT(varchar(19),s.CreatedOn,120) Cr,CONVERT(varchar(19),s.ExitTime,120) Ex,
 s.ExitReason,s.Status,s.Quantity,s.EntryPrice,s.StopLoss,s.Target1,s.OptionLtp,s.OptionSL,s.OptionT1,s.MfePrice,s.MaePrice,s.ProtectedSL,
 s.Rsi5mAtEntry Rsi5,s.RsiAtEntry Rsi1,s.AtrPctAtEntry AtrPct,s.AtrRawAtEntry Atr,s.MarketState,s.SelectedStrategy,j.Pnl,j.ExitAvg
FROM Signals s WITH (NOLOCK) OUTER APPLY (SELECT SUM(tj.ProfitLoss) Pnl,SUM(tj.ExitPrice*tj.Quantity)/NULLIF(SUM(tj.Quantity),0) ExitAvg
 FROM TradeJournal tj WITH (NOLOCK) WHERE tj.SignalId=s.Id) j
WHERE s.CreatedOn>='2026-10-30' ORDER BY s.CreatedOn""",cn)
s.to_csv(OUT+'signals.csv',index=False); print(s.to_string())
c=pd.read_sql("""SELECT c.Symbol,DATEADD(minute,330,c.CandleTime) T,c.[Open] O,c.High H,c.Low L,c.[Close] C FROM Candles c WITH (NOLOCK)
WHERE c.Symbol IN ('NIFTY_1m','BANKNIFTY_1m','SENSEX_1m') AND c.CandleTime>='2026-10-30 03:30' AND c.CandleTime<'2026-10-30 10:05' ORDER BY 1,2""",cn)
c.to_csv(OUT+'candles.csv',index=False); print(len(c),'candles; last',c['T'].max())
