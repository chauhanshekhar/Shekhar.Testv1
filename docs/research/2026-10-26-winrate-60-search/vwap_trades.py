import pandas as pd, numpy as np
D='docs/research/2026-10-26-winrate-60-search/'
s=pd.read_csv(D+'snapshots-vwap.csv'); s=s[s.Vwap.notna()].copy()
s['d']=s.MinuteIst.str[:10]; s=s[~((s.Symbol=='SENSEX')&(s.d=='2026-10-09'))]
s['dist']=(s.Ltp-s.Vwap)/s.Ltp*100; s['distATR']=(s.Ltp-s.Vwap)/(s.AtrPct/100*s.Ltp)
t=pd.read_csv(D+'trades.csv'); t['side']=t.Configuration.str[:3].map({'CAL':'CALL','PUT':'PUT'}); t=t[t.Symbol.isin(['NIFTY','BANKNIFTY','SENSEX'])]
t['net']=t.PnlRs-t.CostRs; t['win']=t.net>0
m=t.merge(s,left_on=['Symbol','EntryIst'],right_on=['Symbol','MinuteIst'])
m['sgn']=np.where(m.side=='CALL',1,-1); m['rel']=m.dist*m.sgn; m['relATR']=m.distATR*m.sgn   # + = price on the trade's favourable side of VWAP
print('trades joined',len(m),'sessions',m.d.nunique())
def tab(x,col,bins,lab=None):
    x=x.assign(b=pd.cut(x[col],bins))
    g=x.groupby('b',observed=True).agg(n=('win','size'),win=('win','mean'),net=('net','sum'),days=('d','nunique'),
        gp=('net',lambda a:a[a>0].sum()),gl=('net',lambda a:-a[a<0].sum()))
    g['pf']=g.gp/g.gl.replace(0,np.nan); return g[['n','days','win','pf','net']].round(2)
pd.set_option('display.width',200)
bins=[-9,-.3,-.15,-.05,0,.05,.15,.3,9]
for side in ('CALL','PUT'):
    x=m[m.side==side]; print(f'\n##### {side} all symbols (rel = % price is on favourable side of VWAP; + = CALL above / PUT below)'); print(tab(x,'rel',bins))
    for sym in ('NIFTY','BANKNIFTY','SENSEX'):
        y=x[x.Symbol==sym]; print(f'--- {side} {sym} n={len(y)} base win {y.win.mean():.1%}'); print(tab(y,'rel',[-9,-.1,0,.1,9]))
m.to_csv(D+'trades-with-vwap.csv',index=False)
