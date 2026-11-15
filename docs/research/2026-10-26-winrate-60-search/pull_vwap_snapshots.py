import json,pyodbc,pandas as pd,warnings
warnings.filterwarnings('ignore')
cs=json.load(open('src/Trading.Worker/appsettings.Production.json',encoding='utf-8-sig'))['ConnectionStrings']['DefaultConnection']
k={a.lower().replace(' ','').replace('_',''):b for a,b in (p.split('=',1) for p in cs.split(';') if '=' in p)}
cn=pyodbc.connect(f"DRIVER={{ODBC Driver 17 for SQL Server}};SERVER={k['server']};DATABASE={k['database']};TrustServerCertificate=yes;UID={k['userid']};PWD={k['password']};",timeout=30)
q="""WITH x AS (SELECT Symbol,SnapshotTime,Ltp,Vwap,Ema20,Ema50,Rsi,Rsi5m,AtrPct,PriceChangePct,Pcr,TrendDirection,
 ROW_NUMBER() OVER (PARTITION BY Symbol,DATEADD(minute,DATEDIFF(minute,0,SnapshotTime),0) ORDER BY SnapshotTime DESC) rn
 FROM IndicatorSnapshots WITH (NOLOCK) WHERE SnapshotTime>='2026-10-09')
SELECT Symbol,CONVERT(varchar(16),DATEADD(minute,DATEDIFF(minute,0,SnapshotTime),0),120) MinuteIst,Ltp,Vwap,Ema20,Ema50,Rsi,Rsi5m,AtrPct,PriceChangePct,Pcr,TrendDirection FROM x WHERE rn=1"""
df=pd.read_sql(q,cn); df.to_csv('docs/research/2026-10-26-winrate-60-search/snapshots-vwap.csv',index=False)
df['d']=df.MinuteIst.str[:10]; df['has']=df.Vwap.notna()
print(df.groupby(['Symbol','d']).agg(rows=('has','size'),vwap=('has','sum')).unstack(0).to_string())
