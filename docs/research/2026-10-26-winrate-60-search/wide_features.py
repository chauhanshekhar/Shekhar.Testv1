import pandas as pd, numpy as np, warnings, pickle
warnings.filterwarnings('ignore')
D='docs/research/2026-10-26-winrate-60-search/'
LOT={'NIFTY':65,'BANKNIFTY':30,'SENSEX':20}; LOTS=4; DELTA=.45; S,T=1.25,2.0
c=pd.read_csv(D+'candles-1m.csv'); c['sym']=c.Symbol.str.replace('_1m','')
c['ist']=pd.to_datetime(c.CandleTime)+pd.Timedelta(hours=5,minutes=30)
c=c.drop_duplicates(['sym','ist'],keep='last').sort_values(['sym','ist'])
c=c[(c.ist.dt.hour*60+c.ist.dt.minute>=555)&(c.ist.dt.hour*60+c.ist.dt.minute<=930)]
def rsi(x,n=14):
    d=x.diff(); up=d.clip(lower=0).ewm(alpha=1/n,adjust=False).mean(); dn=(-d.clip(upper=0)).ewm(alpha=1/n,adjust=False).mean(); return 100-100/(1+up/dn.replace(0,np.nan))
out=[]
for sym,g in c.groupby('sym'):
    g=g.reset_index(drop=True); g['d']=g.ist.dt.strftime('%Y-%m-%d'); g['mo']=g.ist.dt.hour*60+g.ist.dt.minute-555
    C=g.Close; tr=np.maximum(g.High-g.Low,np.maximum((g.High-C.shift()).abs(),(g.Low-C.shift()).abs())); atr=tr.ewm(alpha=1/14,adjust=False).mean()
    f=pd.DataFrame({'sym':sym,'d':g.d,'ist':g.ist,'mo':g.mo,'close':C})
    f['atr']=atr; f['atrpct']=atr/C*100
    e20=C.ewm(span=20,adjust=False).mean(); e50=C.ewm(span=50,adjust=False).mean()
    f['d20']=(C-e20)/atr; f['d50']=(C-e50)/atr; f['gap2050']=(e20-e50)/atr; f['slope20']=(e20-e20.shift(5))/atr
    f['rsi1']=rsi(C)
    g5=g.set_index('ist').Close.resample('5min',label='left',closed='left').last().dropna()   # bar stamped at open; usable after +5m
    r5=rsi(g5); e5a=g5.ewm(span=20,adjust=False).mean(); e5b=g5.ewm(span=50,adjust=False).mean()
    m5=pd.DataFrame({'rsi5':r5,'ema5gap':(e5a-e5b)/ (g5*0.0005)},index=g5.index); m5.index=m5.index+pd.Timedelta(minutes=5)   # closed-bar only
    f=f.merge(m5,left_on='ist',right_index=True,how='left'); f[['rsi5','ema5gap']]=f.groupby('d')[['rsi5','ema5gap']].ffill() if False else f[['rsi5','ema5gap']]
    f[['rsi5','ema5gap']]=f[['rsi5','ema5gap']].ffill()
    for n in (5,15,30,60): f[f'ret{n}']=(C-C.shift(n))/atr
    dopen=g.groupby('d').Open.transform('first'); prev=g.groupby('d').Close.last().shift().reindex(g.d).values
    f['daychg']=(C-dopen)/dopen*100; f['gapopen']=(dopen-prev)/prev*100
    hi=g.groupby('d').High.cummax(); lo=g.groupby('d').Low.cummin()
    f['dayrangepos']=(C-lo)/(hi-lo).replace(0,np.nan); f['dist_hi']=(hi-C)/atr; f['dist_lo']=(C-lo)/atr; f['range_atr']=(hi-lo)/atr
    f['body']=(g.Close-g.Open)/(g.High-g.Low).replace(0,np.nan); f['bar_atr']=(g.High-g.Low)/atr
    up=(C.diff()>0).astype(int); f['upshare10']=up.rolling(10).mean()
    path=(C.diff().abs()).groupby(g.d).cumsum(); f['eff']=(C-dopen).abs()/path.replace(0,np.nan)
    # outcomes for both sides: stop S*atr, target T*atr, stop first on tie, EOD close
    H=g.High.values;L=g.Low.values;Cl=C.values;dd=g.d.values;A=atr.values
    for side,dr in (('CALL',1),('PUT',-1)):
        res=np.full(len(g),np.nan); ex=np.full(len(g),np.nan)
        starts={};
        idx=np.arange(len(g)); 
        for dte in np.unique(dd):
            ii=idx[dd==dte]; lo_,hi_=ii[0],ii[-1]
            for i in ii[:-1]:
                a=A[i]
                if not a>0: continue
                e=Cl[i]; h=H[i+1:hi_+1];l=L[i+1:hi_+1]
                fav=((h-e) if dr>0 else (e-l))/a; adv=((e-l) if dr>0 else (h-e))/a
                hs=adv>=S; ht=fav>=T; si=hs.argmax() if hs.any() else 10**9; ti=ht.argmax() if ht.any() else 10**9
                if si==10**9 and ti==10**9: r=((Cl[hi_]-e)*dr)/a; x=hi_-i
                elif si<=ti: r=-S; x=si+1
                else: r=T; x=ti+1
                res[i]=r; ex[i]=x
        f[f'r_{side}']=res; f[f'x_{side}']=ex
    out.append(f)
F=pd.concat(out); F=F[(F.mo>=20)&(F.mo<=345)].dropna(subset=['rsi5','d50','ret60','r_CALL'])
F.to_pickle(D+'wide-features.pkl'); print(len(F),F.d.nunique(),F.groupby('sym').size().to_dict())
