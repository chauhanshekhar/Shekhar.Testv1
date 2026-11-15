"""Read-only. What changed in behaviour after the 10-29 publish: errors, signals, blocks, strategy settings.
Run from repo root: python docs/research/2026-10-30-deploy-check/check.py"""
import json, pyodbc, pandas as pd, warnings
warnings.filterwarnings('ignore'); pd.set_option('display.width',260); pd.set_option('display.max_colwidth',120); pd.set_option('display.max_rows',200)
cs=json.load(open('src/Trading.Worker/appsettings.Production.json',encoding='utf-8-sig'))['ConnectionStrings']['DefaultConnection']
k={a.lower().replace(' ','').replace('_',''):b for a,b in (p.split('=',1) for p in cs.split(';') if '=' in p)}
cn=pyodbc.connect(f"DRIVER={{ODBC Driver 17 for SQL Server}};SERVER={k['server']};DATABASE={k['database']};TrustServerCertificate=yes;UID={k['userid']};PWD={k['password']};",timeout=60)
q=lambda s:pd.read_sql(s,cn)
print('== Signals per day (last 6 sessions)'); print(q("""SELECT CAST(CreatedOn AS date) D,Symbol,OptionType,ISNULL(SelectedStrategy,'') Strat,COUNT(*) N,SUM(CASE WHEN ExitReason LIKE '%SL%' THEN 1 ELSE 0 END) SL
 FROM Signals WITH (NOLOCK) WHERE CreatedOn>='2026-10-22' GROUP BY CAST(CreatedOn AS date),Symbol,OptionType,SelectedStrategy ORDER BY 1,2""").to_string(index=False))
print('\n== AppErrors per day'); print(q("SELECT CAST(CreatedOn AS date) D,COUNT(*) N FROM AppErrors WITH (NOLOCK) WHERE CreatedOn>='2026-10-22' GROUP BY CAST(CreatedOn AS date) ORDER BY 1").to_string(index=False))
