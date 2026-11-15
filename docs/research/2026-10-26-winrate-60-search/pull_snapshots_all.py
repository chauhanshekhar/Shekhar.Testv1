import json,pyodbc,pandas as pd,warnings
warnings.filterwarnings('ignore')
cs=json.load(open('src/Trading.Worker/appsettings.Production.json',encoding='utf-8-sig'))['ConnectionStrings']['DefaultConnection']
k={a.lower().replace(' ','').replace('_',''):b for a,b in (p.split('=',1) for p in cs.split(';') if '=' in p)}
cn=pyodbc.connect(f"DRIVER={{ODBC Driver 17 for SQL Server}};SERVER={k['server']};DATABASE={k['database']};TrustServerCertificate=yes;UID={k['userid']};PWD={k['password']};",timeout=60)
q="""WITH x AS (SELECT Symbol,SnapshotTime,Ltp,DayOpen,YesterdayClose,Ema20,Ema50,Ema20_5m,Ema50_5m,Rsi,Rsi5m,AtrPct,Pcr,Vwap,PriceChangePct,
 TrendDirection,MarketRegime,MarketState,StrategySelected,Confidence,
 ROW_NUMBER() OVER (PARTITION BY Symbol,DATEADD(minute,DATEDIFF(minute,0,SnapshotTime),0) ORDER BY SnapshotTime DESC) rn
 FROM IndicatorSnapshots WITH (NOLOCK) WHERE SnapshotTime>='2026-07-10')
SELECT Symbol,CONVERT(varchar(16),DATEADD(minute,DATEDIFF(minute,0,SnapshotTime),0),120) MinuteIst,Ltp,DayOpen,YesterdayClose,Ema20,Ema50,Ema20_5m,Ema50_5m,Rsi,Rsi5m,AtrPct,Pcr,Vwap,PriceChangePct,TrendDirection,MarketRegime,MarketState,StrategySelected,Confidence FROM x WHERE rn=1"""
df=pd.read_sql(q,cn); df.to_csv('docs/research/2026-10-26-winrate-60-search/snapshots-all.csv',index=False)
print(len(df)); print(df.groupby('Symbol').MinuteIst.agg(['min','max','count']))
for c in ['TrendDirection','MarketRegime','MarketState','StrategySelected']: print(c,df[c].value_counts().head(8).to_dict())
