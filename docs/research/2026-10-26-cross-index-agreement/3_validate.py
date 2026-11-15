import pandas as pd,numpy as np,sys,json,pyodbc,warnings
warnings.filterwarnings('ignore'); pd.set_option('display.width',250)
X=pd.read_csv(sys.argv[1]+'/feat.csv',parse_dates=['t']); X=X.dropna(subset=['xagree15'])
cs=json.load(open('src/Trading.Worker/appsettings.Production.json',encoding='utf-8-sig'))['ConnectionStrings']['DefaultConnection']
k={a.lower().replace(' ','').replace('_',''):b for a,b in (p.split('=',1) for p in cs.split(';') if '=' in p)}
cn=pyodbc.connect(f"DRIVER={{ODBC Driver 17 for SQL Server}};SERVER={k['server']};DATABASE={k['database']};TrustServerCertificate=yes;UID={k['userid']};PWD={k['password']};",timeout=60)
j=pd.read_sql("SELECT SignalId Id,SUM(ProfitLoss) pl FROM TradeJournal WITH (NOLOCK) WHERE SignalId IS NOT NULL GROUP BY SignalId",cn)
X=X.merge(j,on='Id',how='left'); X['blk']=X.xagree15<2
mid=X.t.median()
def s(g): return pd.Series(dict(n=len(g),days=g.day.nunique(),stop=round(g.stop.mean(),2),expR=round(g.R.mean(),3),sumR=round(g.R.sum(),1),real_pl=g.pl.sum(),real_n=g.pl.notna().sum()))
print(X.groupby('blk').apply(s))
for col in ['pe','Symbol']:
    print(X.groupby([col,'blk']).apply(s))
print(X.assign(half=np.where(X.t<mid,'H1','H2')).groupby(['half','blk']).apply(s))
# per-day: blocked sumR sign
pdz=X[X.blk].groupby('day').R.sum(); print('blocked days neg',(pdz<0).sum(),'of',len(pdz),' kept days:',(X[~X.blk].groupby('day').R.sum()>0).sum(),'pos of',X[~X.blk].day.nunique())
# permutation control: mean R difference kept-blocked
obs=X[~X.blk].R.mean()-X[X.blk].R.mean(); rng=np.random.default_rng(1); n=X.blk.sum(); r=X.R.values
perm=[r[rng.permutation(len(r))[n:]].mean()-r[rng.permutation(len(r))[:n]].mean() for _ in range(5000)]
# proper permutation
perm=[]
for _ in range(5000):
    p=rng.permutation(len(r)); perm.append(r[p[n:]].mean()-r[p[:n]].mean())
print('obs diff',round(obs,3),' p(one-sided)',(np.array(perm)>=obs).mean())
# day-clustered permutation: shuffle blocked labels within day-blocks is complex; use day-level bootstrap
days=X.day.unique(); bs=[]
for _ in range(2000):
    dsel=rng.choice(days,len(days)); sub=pd.concat([X[X.day==d_] for d_ in dsel]); bs.append(sub[~sub.blk].R.mean()-sub[sub.blk].R.mean())
print('day-bootstrap diff 5/50/95%',np.percentile(bs,[5,50,95]).round(3))
