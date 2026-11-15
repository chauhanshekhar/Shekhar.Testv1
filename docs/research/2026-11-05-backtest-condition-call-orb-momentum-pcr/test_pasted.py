"""Pasted CALL hypotheses A/B/C on the 2026-10-26 snapshot+outcome export (candle-derived outcomes).
Features at the row minute only; ORH uses candles 09:15-09:29 IST, usable from 09:30. Non-overlap per symbol.
Costs: Rs40 flat round trip (brokerage only; slippage NOT modelled - no stored lot/slippage data).
    python test_pasted.py   (run from this folder)"""
import pandas as pd, numpy as np
R = "../"
d = pd.read_csv(R+"2026-10-26-backtest-condition-both/snapshots-outcomes.csv"); d = d[d.Dir=="CALL"].copy()
d["t"]=pd.to_datetime(d.MinuteIst); d["day"]=d.t.dt.date
dc=pd.read_csv(R+"2026-10-28-backtest-condition-call/daily-open-close.csv"); dc["Symbol"]=dc.Symbol.str.replace("_1m","")
dc["d"]=pd.to_datetime(dc.d).dt.date; dc=dc.sort_values(["Symbol","d"])
dc["PC"]=dc.groupby("Symbol").DayClose.shift(); dc["PD"]=dc.groupby("Symbol").d.shift()
dc["GapPct"]=(dc.DayOpen-dc.PC)/dc.PC*100
dc.loc[(pd.to_datetime(dc.d)-pd.to_datetime(dc.PD)).dt.days>4,"GapPct"]=np.nan
d=d.merge(dc[["Symbol","d","GapPct"]],left_on=["Symbol","day"],right_on=["Symbol","d"],how="left")
d["Gap"]=np.select([d.GapPct>=.3,d.GapPct<=-.3,d.GapPct.notna()],["GapUp","GapDown","Normal"],"Missing")
# candles (UTC -> IST)
c=pd.read_csv(R+"2026-10-26-winrate-60-search/candles-1m.csv"); c["Symbol"]=c.Symbol.str.replace("_1m","")
c["t"]=pd.to_datetime(c.CandleTime)+pd.Timedelta(hours=5,minutes=30); c["day"]=c.t.dt.date
c=c.sort_values(["Symbol","t"]).reset_index(drop=True)
def st(g,n=10,m=3.0):
    h,l,cl=g.High.values,g.Low.values,g.Close.values; pc=np.r_[cl[0],cl[:-1]]
    tr=np.maximum(h-l,np.maximum(abs(h-pc),abs(l-pc))); atr=pd.Series(tr).ewm(alpha=1/n,adjust=False).mean().values
    hl=(h+l)/2; ub=hl+m*atr; lb=hl-m*atr; fu,fl=ub.copy(),lb.copy(); bull=np.ones(len(g),bool)
    for i in range(1,len(g)):
        fu[i]=ub[i] if ub[i]<fu[i-1] or pc[i]>fu[i-1] else fu[i-1]
        fl[i]=lb[i] if lb[i]>fl[i-1] or pc[i]<fl[i-1] else fl[i-1]
        bull[i]=True if cl[i]>fu[i-1] else False if cl[i]<fl[i-1] else bull[i-1]
    return pd.Series(bull,index=g.index)
c["StBull"]=c.groupby("Symbol",group_keys=False).apply(st)   # continuous across days (small warm-up bias)
orr=c[(c.t.dt.hour==9)&(c.t.dt.minute>=15)&(c.t.dt.minute<30)].groupby(["Symbol","day"]).agg(ORH=("High","max"),ORL=("Low","min")).reset_index()
d=d.merge(orr,on=["Symbol","day"],how="left")
c["tm"]=c.t.dt.floor("min"); d["tm"]=d.t.dt.floor("min")
d=d.merge(c[["Symbol","tm","StBull"]],on=["Symbol","tm"],how="left")
d=d.sort_values(["Symbol","t"]); d["Pcr0"]=d.groupby(["Symbol","day"]).Pcr.transform("first")
d["dPcr"]=d.Pcr-d.Pcr0
d["after15"]=d.t.dt.time>=pd.Timestamp("09:30").time()
A=(d.Gap=="GapUp")&d.after15&(d.Ltp>d.ORH)&(d.Vwap.notna())&(d.Ltp>d.Vwap)&(d.Ema20>d.Ema50)
A_noV=(d.Gap=="GapUp")&d.after15&(d.Ltp>d.ORH)&(d.Ema20>d.Ema50)
B=(d.Rsi5m>50)&(d.Ema20_5m>d.Ema50_5m)&(d.PriceChangePct>0)&(d.StBull==True)
Bc=B&d.after15&(d.Ltp>d.ORH)
PR=d.dPcr>0.03; PF=d.dPcr<-0.03; PU=~PR&~PF
low0=d.Pcr0<d.groupby("day").Pcr0.transform("median")
cells={"BASE":pd.Series(True,index=d.index),
 "LIVE RSI5m45-70":(d.Rsi5m>=45)&(d.Rsi5m<=70),
 "A full (GapUp,ORH brk,>VWAP,EMA20>50)":A,"A w/o VWAP (GapUp,ORH brk,EMA)":A_noV,
 "ORH break only (any gap)":d.after15&(d.Ltp>d.ORH),
 "B momentum":B,"B + ORH confirm":Bc,
 "C lowOpenPCR & rising":low0&PR,"C lowOpenPCR & falling":low0&PF,"C lowOpenPCR & unchanged":low0&PU,
 "C highOpenPCR & rising":~low0&PR,"C any & rising":PR,"C any & falling":PF}
days=sorted(d.day.unique()); n=len(days); c1,c2=days[int(n*.6)],days[int(n*.8)]
d["Split"]=np.where(d.day<c1,"train",np.where(d.day<c2,"val","hold"))
def nonov(g):
    k,f=[],{}
    for i,s,t,m in zip(g.index,g.Symbol,g.t,g.MinsToExit.fillna(45)):
        if s in f and t<f[s]: continue
        k.append(i); f[s]=t+pd.Timedelta(minutes=max(m,1))
    return g.loc[k]
def stats(g,cost=40):
    g=g[(g.Result!="NA")&g.PnlRsEst.notna()]
    if not len(g): return dict(N=0)
    p=g.PnlRsEst-cost; w=p[p>0].sum(); l=-p[p<0].sum(); bd=g.groupby("day").PnlRsEst.sum()
    return dict(N=len(g),Days=g.day.nunique(),Win=round((p>0).mean()*100,1),Gross=round(g.PnlRsEst.sum()),Net=round(p.sum()),
                PF=round(w/l,2) if l else np.inf,Exp=round(p.mean(),1),ExpR=round(g.OutR.mean(),3))
rows=[]
for k,m in cells.items():
    for gap in ["ALL","GapUp","Normal","GapDown"]:
        mm=m if gap=="ALL" else m&(d.Gap==gap); g=nonov(d[mm]); o={"Cell":k,"Gap":gap}
        for sp in ["train","val","hold","all"]:
            s=stats(g if sp=="all" else g[g.Split==sp]); o.update({f"{sp}_{a}":b for a,b in s.items()})
        rows.append(o)
r=pd.DataFrame(rows); r.to_csv("cells.csv",index=False)
pd.set_option("display.width",250); pd.set_option("display.max_columns",40)
print("days",n,"train<",c1,"hold>=",c2,"Vwap rows",int(d.Vwap.notna().sum()),"of",len(d))
print(r[r.Gap=="ALL"][["Cell","all_N","all_Days","all_Win","all_Net","all_PF","all_ExpR","train_Net","val_Net","hold_N","hold_Net","hold_PF"]].to_string(index=False))
print(r[(r.Gap=="GapUp")][["Cell","all_N","all_Days","all_Net","all_PF","train_Net","val_Net","hold_N","hold_Net","hold_PF"]].to_string(index=False))
