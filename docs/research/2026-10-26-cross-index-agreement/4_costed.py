import pandas as pd,numpy as np,sys,json,pyodbc,warnings
warnings.filterwarnings('ignore'); pd.set_option('display.width',250)
X=pd.read_csv(sys.argv[1]+'/feat.csv',parse_dates=['t']).dropna(subset=['xagree15'])
cs=json.load(open('src/Trading.Worker/appsettings.Production.json',encoding='utf-8-sig'))['ConnectionStrings']['DefaultConnection']
k={a.lower().replace(' ','').replace('_',''):b for a,b in (p.split('=',1) for p in cs.split(';') if '=' in p)}
cn=pyodbc.connect(f"DRIVER={{ODBC Driver 17 for SQL Server}};SERVER={k['server']};DATABASE={k['database']};TrustServerCertificate=yes;UID={k['userid']};PWD={k['password']};",timeout=60)
q=pd.read_sql("SELECT Id,Quantity,DeltaAtEntry,EntryPrice,StopLoss FROM Signals WITH (NOLOCK)",cn)
X=X.merge(q,on='Id'); X['dl']=X.DeltaAtEntry.abs().replace(0,np.nan).fillna(0.45).clip(0.3,0.7)
X['Quantity']=X.Quantity*X.Symbol.map({'NIFTY':65,'BANKNIFTY':30,'SENSEX':20}); X['Rpts']=(X.EntryPrice-X.StopLoss).abs()*X.dl            # 1R in premium points
mid=X.t.median()
def run(cost_per_order,slip_pts,lab):
    X['gross']=X.R*X.Rpts*X.Quantity
    X['cost']=2*cost_per_order+2*slip_pts*X.Quantity
    X['net']=X.gross-X.cost
    out=[]
    for name,m in [('ALL',X.xagree15>=0),('KEEP (both agree)',X.xagree15>=2),('BLOCK',X.xagree15<2)]:
        g=X[m]; w=g[g.net>0].net.sum(); l=-g[g.net<0].net.sum()
        out.append(dict(set=name,n=len(g),days=g.day.nunique(),win=round((g.net>0).mean(),2),net=round(g.net.sum()),PF=round(w/l,2),perday=round(g.net.sum()/g.day.nunique())))
    print('\n',lab); print(pd.DataFrame(out).to_string(index=False))
    for h,mm in [('H1',X.t<mid),('H2',X.t>=mid)]:
        kp=X[mm&(X.xagree15>=2)]; bl=X[mm&(X.xagree15<2)]
        print(h,'keep n',len(kp),'net',round(kp.net.sum()),'PF',round(kp[kp.net>0].net.sum()/-kp[kp.net<0].net.sum(),2),'| block n',len(bl),'net',round(bl.net.sum()),'PF',round(bl[bl.net>0].net.sum()/-bl[bl.net<0].net.sum(),2))
    return X
run(0,0,'no costs'); run(20,1,'Rs20/order + 1pt/side slippage'); run(20,2,'Rs20/order + 2pt/side slippage (harsh)')
print(X.groupby('Symbol').Quantity.median().to_dict())
X['keep']=X.xagree15>=2
print(X.groupby(['Symbol','keep']).net.agg(['size','sum']).round())
