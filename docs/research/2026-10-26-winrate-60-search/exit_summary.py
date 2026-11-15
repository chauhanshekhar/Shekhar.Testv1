import pandas as pd
D='docs/research/2026-10-26-winrate-60-search/'
R=pd.read_csv(D+'exit-grid.csv'); pd.set_option('display.width',250)
cols=['S','T','H','BE','win','pf','net','pf1','pf2']
out=[]
for (side,sym),x in R.groupby(['side','sym']):
    a=x[x.arm=='real']; b=x[x.arm=='control']
    ref=a[(a.S==1.25)&(a['T']==2)&(a.H==999)&(a.BE.isna())].iloc[0]
    print(f"\n== {side} {sym} n={ref.n} days={ref.days} | ref S1.25/T2: win {ref.win:.1%} PF {ref.pf:.2f} net {ref.net:,.0f}")
    z=a[(a.pf>=ref.pf)&(a.win>ref.win)].sort_values('win',ascending=False).head(3)
    print(' higher win AND PF>=ref:'); print(z[cols].round(2).to_string(index=False) if len(z) else '  none')
    q=a[(a.win>=.6)]
    print(' configs with win>=60%%: %d; best PF among them %.2f; net>0 among them: %d'%(len(q),q.pf.max() if len(q) else 0,(q.net>0).sum()))
    print(' best PF overall (both-halves PF>1 only):'); print(a[(a.pf1>1)&(a.pf2>1)].sort_values('pf',ascending=False).head(3)[cols].round(2).to_string(index=False))
    print(' control best PF %.2f; control cfgs PF>1 both halves: %d of %d | real: %d'%(b.pf.max(),((b.pf1>1)&(b.pf2>1)).sum(),len(b),((a.pf1>1)&(a.pf2>1)).sum()))
