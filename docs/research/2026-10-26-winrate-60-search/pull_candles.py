import json,pyodbc,pandas as pd,warnings
warnings.filterwarnings('ignore')
cs=json.load(open('src/Trading.Worker/appsettings.Production.json',encoding='utf-8-sig'))['ConnectionStrings']['DefaultConnection']
k={a.lower().replace(' ','').replace('_',''):b for a,b in (p.split('=',1) for p in cs.split(';') if '=' in p)}
c=f"DRIVER={{ODBC Driver 17 for SQL Server}};SERVER={k['server']};DATABASE={k['database']};TrustServerCertificate=yes;UID={k['userid']};PWD={k['password']};"
cn=pyodbc.connect(c,timeout=30)
df=pd.read_sql("SELECT Symbol,CandleTime,[Open],[High],[Low],[Close] FROM Candles WITH (NOLOCK) WHERE Symbol IN ('NIFTY_1m','BANKNIFTY_1m','SENSEX_1m') ORDER BY Symbol,CandleTime",cn)
df.to_csv('docs/research/2026-10-26-winrate-60-search/candles-1m.csv',index=False)
print(len(df)); print(pd.read_sql("SELECT Symbol,LotSize FROM TradingInstruments WHERE IsActive=1",cn))
