"""Read-only. BANKNIFTY IndicatorSnapshots on 2026-10-30 with gate block reasons (why no earlier entry).
Run from repo root: python docs/research/2026-10-30-live-stoploss-monitor-today/blocked.py"""
import json, pyodbc, pandas as pd, warnings
warnings.filterwarnings('ignore'); pd.set_option('display.width',300); pd.set_option('display.max_colwidth',110)
OUT='docs/research/2026-10-30-live-stoploss-monitor-today/'
cs=json.load(open('src/Trading.Worker/appsettings.Production.json',encoding='utf-8-sig'))['ConnectionStrings']['DefaultConnection']
k={a.lower().replace(' ','').replace('_',''):b for a,b in (p.split('=',1) for p in cs.split(';') if '=' in p)}
cn=pyodbc.connect(f"DRIVER={{ODBC Driver 17 for SQL Server}};SERVER={k['server']};DATABASE={k['database']};TrustServerCertificate=yes;UID={k['userid']};PWD={k['password']};",timeout=60)
s=pd.read_sql("""SELECT CONVERT(varchar(19),SnapshotTime,120) T,Ltp,Ema20,Ema50,Ema20_5m E20_5,Ema50_5m E50_5,Rsi,Rsi5m,MarketState,StrategySelected Strat,
 SignalGenerated Sig,BlockedBy,BlockedOptionType BOT,BlockedReason FROM IndicatorSnapshots WITH (NOLOCK)
WHERE Symbol='BANKNIFTY' AND SnapshotTime>='2026-10-30' AND SnapshotTime<'2026-10-31' ORDER BY SnapshotTime""",cn)
s.to_csv(OUT+'banknifty_snapshots.csv',index=False)
print(len(s),s['T'].min(),s['T'].max())
print(s.groupby('BlockedBy',dropna=False).size().sort_values(ascending=False).to_string())
for a,b in [('10:35','10:59'),('13:40','14:07'),('14:30','15:03')]:
    x=s[(s['T']>='2026-10-30 '+a)&(s['T']<='2026-10-30 '+b)]
    print('\n==',a,b); print(x.drop(columns=['Ema20','Ema50']).round(1).to_string(index=False))
