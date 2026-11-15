# Tags SENSEX live-config replay trades with the live liquidity verdict at the entry minute
# (IndicatorSnapshots.BlockedReason LIKE 'No liquid ATM%'). Read-only.
import json,pyodbc,pandas as pd,warnings,sys
warnings.filterwarnings('ignore'); pd.set_option('display.width',250)
D=sys.argv[1]
cs=json.load(open('src/Trading.Worker/appsettings.Production.json',encoding='utf-8-sig'))['ConnectionStrings']['DefaultConnection']
k={a.lower().replace(' ','').replace('_',''):b for a,b in (p.split('=',1) for p in cs.split(';') if '=' in p)}
cn=pyodbc.connect(f"DRIVER={{ODBC Driver 17 for SQL Server}};SERVER={k['server']};DATABASE={k['database']};TrustServerCertificate=yes;UID={k['userid']};PWD={k['password']};",timeout=60)
snap=pd.read_sql("""SELECT CONVERT(char(16),SnapshotTime,120) m,
  MAX(CASE WHEN BlockedReason LIKE 'No liquid ATM%' THEN 1 ELSE 0 END) noliq
  FROM IndicatorSnapshots WITH (NOLOCK) WHERE Symbol='SENSEX' AND SnapshotTime>='2026-10-07' GROUP BY CONVERT(char(16),SnapshotTime,120)""",cn)
t=pd.read_csv(f'{D}/trades.csv')
t=t[(t.Symbol=='SENSEX')&t.Configuration.str.endswith('|Live')].copy()
t=t.merge(snap,left_on='EntryIst',right_on='m',how='left')
t['Side']=t.Configuration.str.split('|').str[0]
t['Net']=t.PnlRs-t.CostRs.fillna(0)
t['Day']=t.EntryIst.str[:10]
t['Bucket']=t.noliq.map({1:'BLOCKED (no liquid ATM)',0:'allowed'}).fillna('no snapshot')
def stats(g):
    w=g.Net[g.Net>0].sum(); l=-g.Net[g.Net<0].sum(); eq=g.sort_values('EntryIst').Net.cumsum()
    return pd.Series({'N':len(g),'Days':g.Day.nunique(),'Win%':round(100*(g.Net>0).mean()),'Gross':round(g.PnlRs.sum()),'Net':round(g.Net.sum()),
      'PF':round(w/l,2) if l else None,'Exp':round(g.Net.mean()),'MaxDD':round((eq.cummax()-eq).max()),'BestDay':round(g.groupby('Day').Net.sum().max()),
      'NetExBestDay':round(g.Net.sum()-g.groupby('Day').Net.sum().max())})
print(t.groupby(['Side','Bucket']).apply(stats).to_string()); print()
b=t[t.Bucket.str.startswith('BLOCKED')]
print('Blocked trades, per day:'); print(b.groupby(['Side','Day']).agg(N=('Net','size'),Net=('Net','sum')).round(0).to_string()); print()
days=sorted(t.Day.unique()); n=len(days); cut1,cut2=days[int(n*.6)],days[int(n*.8)]
b=b.assign(Period=b.Day.map(lambda d:'train' if d<cut1 else 'val' if d<cut2 else 'hold'))
print(f'Split: train <{cut1} <= val < {cut2} <= hold'); print(b.groupby(['Side','Period']).apply(stats)[['N','Days','Net','PF']].to_string())
t.to_csv(f'{D}/sensex-live-tagged.csv',index=False)
