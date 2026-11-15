import pandas as pd, numpy as np
D='docs/research/2026-10-26-winrate-60-search/'
s=pd.read_csv(D+'snapshots-vwap.csv'); s=s[s.Vwap.notna()&s.AtrPct.notna()].copy()
s['d']=s.MinuteIst.str[:10]; s=s[~((s.Symbol=='SENSEX')&(s.d=='2026-10-09'))]
c=pd.read_csv(D+'candles-1m.csv'); c['sym']=c.Symbol.str.replace('_1m','')
c['m']=(pd.to_datetime(c.CandleTime)+pd.Timedelta(hours=5,minutes=30)).dt.strftime('%Y-%m-%d %H:%M')
rows=[]
for sym,g in c.groupby('sym'):
    g=g.drop_duplicates('m',keep='last').set_index('m').Close
    x=s[s.Symbol==sym].copy()
    ts=pd.to_datetime(x.MinuteIst)
    for h in (15,30):
        f=(ts+pd.Timedelta(minutes=h)).dt.strftime('%Y-%m-%d %H:%M')
        x[f'f{h}']=g.reindex(f).values-g.reindex(x.MinuteIst).values
    x['slope15']=x.Vwap-x.groupby('d').Vwap.shift(15)
    rows.append(x)
x=pd.concat(rows).dropna(subset=['f30'])
x['dist']=(x.Ltp-x.Vwap)/x.Ltp*100
x['f30a']=x.f30/(x.AtrPct/100*x.Ltp); x['f15a']=x.f15/(x.AtrPct/100*x.Ltp)
x['bucket']=pd.cut(x.dist,[-9,-.3,-.15,-.05,.05,.15,.3,9])
pd.set_option('display.width',200)
print('rows',len(x),'sessions',x.d.nunique())
def rep(df,name):
    g=df.groupby('bucket',observed=True).agg(n=('f30a','size'),mean30=('f30a','mean'),up30=('f30a',lambda a:(a>0).mean()),
       dayspos=('d',lambda k: 0)).drop(columns='dayspos')
    pos=df.groupby(['bucket','d'],observed=True).f30a.mean().groupby('bucket').apply(lambda a:f'{(a>0).sum()}/{len(a)}')
    g['days_up']=pos; print('\n',name); print(g.round(3))
rep(x,'ALL symbols: forward 30m move (ATR, + = up) by price-vs-VWAP %')
for sym in ('NIFTY','BANKNIFTY','SENSEX'): rep(x[x.Symbol==sym],sym)
x.to_csv(D+'vwap-forward.csv',index=False)
