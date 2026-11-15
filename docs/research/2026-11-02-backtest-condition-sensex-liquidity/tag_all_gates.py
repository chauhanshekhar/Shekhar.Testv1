# All symbols: tags live-config replay trades with the live gate that blocked the entry minute
# (stored IndicatorSnapshots.BlockedBy/BlockedReason). Read-only. Net = PnlRs - CostRs.
import json,pyodbc,pandas as pd,warnings,sys
warnings.filterwarnings('ignore'); pd.set_option('display.width',250)
D=sys.argv[1]
cs=json.load(open('src/Trading.Worker/appsettings.Production.json',encoding='utf-8-sig'))['ConnectionStrings']['DefaultConnection']
k={a.lower().replace(' ','').replace('_',''):b for a,b in (p.split('=',1) for p in cs.split(';') if '=' in p)}
cn=pyodbc.connect(f"DRIVER={{ODBC Driver 17 for SQL Server}};SERVER={k['server']};DATABASE={k['database']};TrustServerCertificate=yes;UID={k['userid']};PWD={k['password']};",timeout=60)
snap=pd.read_sql("""SELECT Symbol, CONVERT(char(16),SnapshotTime,120) m,
  MAX(CASE WHEN BlockedReason LIKE 'No liquid ATM%' THEN 'NoLiquidATM' WHEN BlockedReason LIKE '%PCR%' THEN 'PCR rule'
           WHEN SignalGenerated=1 THEN 'TRADED' ELSE ISNULL(BlockedBy,'(none)') END) gate
  FROM IndicatorSnapshots WITH (NOLOCK) WHERE SnapshotTime>='2026-10-07' GROUP BY Symbol, CONVERT(char(16),SnapshotTime,120)""",cn)
t=pd.read_csv(f'{D}/trades.csv')
t=t[(t.Symbol!='ALL')&t.Configuration.str.endswith('|Live')].copy()
t=t.merge(snap,left_on=['Symbol','EntryIst'],right_on=['Symbol','m'],how='left')
t['Side']=t.Configuration.str.split('|').str[0]; t['Net']=t.PnlRs-t.CostRs.fillna(0); t['Day']=t.EntryIst.str[:10]
t['Morning']=t.EntryIst.str[11:16]<'13:00'
def stats(g):
    w=g.Net[g.Net>0].sum(); l=-g.Net[g.Net<0].sum(); bd=g.groupby('Day').Net.sum().max()
    return pd.Series({'N':len(g),'Days':g.Day.nunique(),'Net':round(g.Net.sum()),'PF':round(w/l,2) if l else None,'NetExBestDay':round(g.Net.sum()-bd)})
print('By symbol x live gate at entry minute (all times):'); print(t.groupby(['Symbol','gate']).apply(stats).to_string()); print()
print('Morning (<13:00) vs afternoon, by symbol:'); print(t.groupby(['Symbol','Morning']).apply(stats).to_string())
t.to_csv(f'{D}/all-symbols-tagged.csv',index=False)
