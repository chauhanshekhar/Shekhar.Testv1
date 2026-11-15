import json,pyodbc,warnings,pandas as pd
warnings.filterwarnings("ignore")
def conn():
    cs=json.load(open("src/Trading.Worker/appsettings.Production.json",encoding="utf-8-sig"))["ConnectionStrings"]["DefaultConnection"]
    k={a.lower().replace(" ","").replace("_",""):b for a,b in (p.split("=",1) for p in cs.split(";") if "=" in p)}
    return pyodbc.connect(f"DRIVER={{ODBC Driver 17 for SQL Server}};SERVER={k['server']};DATABASE={k['database']};TrustServerCertificate=yes;UID={k['userid']};PWD={k['password']};",timeout=60)
def q(s): return pd.read_sql(s,conn())
