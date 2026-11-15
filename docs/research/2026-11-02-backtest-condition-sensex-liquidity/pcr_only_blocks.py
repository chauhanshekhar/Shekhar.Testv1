# Morning minutes where PCR was the ONLY failing CALL/PUT condition (from SignalEngine BlockedReason),
# grouped into episodes, with the index move 15/30 min later (snapshot Ltp; ATR = AtrRaw). Read-only.
import json,pyodbc,pandas as pd,re,warnings,sys
warnings.filterwarnings('ignore'); pd.set_option('display.width',250)
cs=json.load(open('src/Trading.Worker/appsettings.Production.json',encoding='utf-8-sig'))['ConnectionStrings']['DefaultConnection']
k={a.lower().replace(' ','').replace('_',''):b for a,b in (p.split('=',1) for p in cs.split(';') if '=' in p)}
cn=pyodbc.connect(f"DRIVER={{ODBC Driver 17 for SQL Server}};SERVER={k['server']};DATABASE={k['database']};TrustServerCertificate=yes;UID={k['userid']};PWD={k['password']};",timeout=60)
s=pd.read_sql("""SELECT Symbol, CONVERT(char(16),SnapshotTime,120) m, MAX(Ltp) Ltp, MAX(Pcr) Pcr, MAX(AtrRaw) Atr,
  MAX(CASE WHEN BlockedBy='SignalEngine' THEN BlockedReason END) r
  FROM IndicatorSnapshots WITH (NOLOCK) WHERE ((SnapshotTime>='2026-10-30' AND SnapshotTime<'2026-10-31') OR SnapshotTime>='2026-11-02')
  GROUP BY Symbol, CONVERT(char(16),SnapshotTime,120)""",cn)
s['t']=pd.to_datetime(s.m); s=s.sort_values(['Symbol','t'])
def fails(r,side):
    return [] if not isinstance(r,str) else [p.strip() for p in r.split('|') if p.strip().startswith(side+':')]
rows=[]
for side in ['CALL','PUT']:
    f=s.r.map(lambda r:fails(r,side))
    s[side+'_pcronly']=f.map(lambda x:len(x)==1 and 'PCR' in x[0])
    s[side+'_pcrfail']=f.map(lambda x:any('PCR' in i for i in x))
fw=s.set_index(['Symbol','t']).Ltp
for side in ['CALL','PUT']:
    x=s[s[side+'_pcronly']&(s.t.dt.time<pd.Timestamp('13:00').time())].copy()
    for (sym,d),g in x.groupby(['Symbol',x.t.dt.date]):
        ep=(g.t.diff()>pd.Timedelta('5min')).cumsum()
        for _,e in g.groupby(ep):
            a=e.iloc[0]; sg=1 if side=='CALL' else -1
            def mv(mins):
                try: return round(sg*(fw[(sym,a.t+pd.Timedelta(minutes=mins))]-a.Ltp)/a.Atr,2)
                except KeyError: return None
            rows.append(dict(Day=str(d),Symbol=sym,Side=side,Start=a.t.strftime('%H:%M'),End=e.t.iloc[-1].strftime('%H:%M'),Minutes=len(e),
                             Pcr=round(a.Pcr,2),Rule=re.search(r'PCR[^-]*',e.r.iloc[0][e.r.iloc[0].find(side+':'):]).group(0).strip(),
                             Ltp=a.Ltp,Move15_ATR=mv(15),Move30_ATR=mv(30)))
out=pd.DataFrame(rows).sort_values(['Day','Symbol','Start'])
print(out.to_string(index=False)); print()
m=s[s.t.dt.time<pd.Timestamp('13:00').time()]
print(m.groupby([m.t.dt.date,'Symbol'])[['CALL_pcrfail','CALL_pcronly','PUT_pcrfail','PUT_pcronly']].sum().to_string())
out.to_csv(sys.argv[1]+'/pcr-only-blocks.csv',index=False)
