import json,pyodbc,pandas as pd,warnings,sys
warnings.filterwarnings('ignore'); pd.set_option('display.width',250)
cs=json.load(open('src/Trading.Worker/appsettings.Production.json',encoding='utf-8-sig'))['ConnectionStrings']['DefaultConnection']
k={a.lower().replace(' ','').replace('_',''):b for a,b in (p.split('=',1) for p in cs.split(';') if '=' in p)}
cn=pyodbc.connect(f"DRIVER={{ODBC Driver 17 for SQL Server}};SERVER={k['server']};DATABASE={k['database']};TrustServerCertificate=yes;UID={k['userid']};PWD={k['password']};",timeout=60)
d=pd.read_sql("""SELECT Id,Symbol,SignalType,OptionType,EntryPrice,StopLoss,Target1,Target2,Confidence,CreatedOn,OptionLtp,OptionSL,OptionT1,MaePrice,MfePrice,Status,ExitReason,ExitTime,ChangeInOiAtEntry,DeltaAtEntry,PcrAtEntry,Rsi5mAtEntry,AtrPctAtEntry,MarketState,IsExpiryDay,IndexT1Hit,ProtectedSL FROM Signals WITH (NOLOCK) ORDER BY Id""",cn)
d.to_csv(sys.argv[1]+'/sig.csv',index=False)
print(len(d)); print(d.groupby(['Status']).size()); print(d.groupby(['OptionType','ExitReason']).size().unstack(0).fillna(0)); print(d.CreatedOn.min(),d.CreatedOn.max())
