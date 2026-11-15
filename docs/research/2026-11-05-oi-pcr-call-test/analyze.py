"""CALL hypothesis C (opening vs entry PCR x Put/Call OI change x underlying rising) on 20 sessions 10-07..11-04.
Unfiltered base rate = every symbol-minute 09:30-14:30 IST. Outcome (index-level, in R, NO costs/slippage):
entry = next-minute candle open, stop = 1.25*AtrRaw, target = 1.2R, max 45 min (timeout = close R); same-candle hit = stop.
OI change = (sum OI now - sum OI 15 min earlier) / earlier, over the 5 strikes nearest spot at the EARLIER minute, CE and PE
separately (past data only). Non-overlap per symbol. Chronological split by session 60/20/20."""
import pandas as pd, numpy as np
s=pd.read_csv("snap.csv",parse_dates=["SnapshotTime"]); c=pd.read_csv("candles.csv",parse_dates=["CandleTime"]); o=pd.read_csv("oi.csv",parse_dates=["MinuteTime"])
c["Symbol"]=c.Symbol.str.replace("_1m",""); c["t"]=c.CandleTime+pd.Timedelta(hours=5,minutes=30)
s["m"]=s.SnapshotTime.dt.floor("min"); s=s.drop_duplicates(["Symbol","m"]).sort_values(["Symbol","m"]); s["day"]=s.m.dt.date
s=s[(s.m.dt.time>=pd.Timestamp("09:30").time())|True]
s["Pcr0"]=s.groupby(["Symbol","day"]).Pcr.transform("first"); s["dPcr"]=s.Pcr-s.Pcr0
# OI lookup
oi={}
for (sym,m),g in o.groupby(["Symbol","MinuteTime"]): oi[(sym,m)]=g
def oichg(sym,m,lag=15):
    b=oi.get((sym,m-pd.Timedelta(minutes=lag))); n=oi.get((sym,m))
    if b is None or n is None: return np.nan,np.nan
    u=b.UnderlyingLtp.iloc[0]; ks=sorted(b.StrikePrice.unique(),key=lambda k:abs(k-u))[:5]; r=[]
    for ot in ("CE","PE"):
        b0=b[(b.OptionType==ot)&b.StrikePrice.isin(ks)].set_index("StrikePrice").OpenInterest
        n0=n[(n.OptionType==ot)&n.StrikePrice.isin(ks)].set_index("StrikePrice").OpenInterest
        j=pd.concat([b0,n0],axis=1,keys=["b","n"]).dropna()
        r.append((j.n.sum()-j.b.sum())/j.b.sum()*100 if len(j)>=3 and j.b.sum()>0 else np.nan)
    return r[0],r[1]
s=s[(s.m.dt.time>=pd.Timestamp("09:30").time())&(s.m.dt.time<=pd.Timestamp("14:30").time())].copy()
v=[oichg(a,b) for a,b in zip(s.Symbol,s.m)]; s["CeChg"]=[x[0] for x in v]; s["PeChg"]=[x[1] for x in v]
# underlying 15 min ago
s["Ltp15"]=s.groupby("Symbol").Ltp.shift(15)  # approx: previous rows are minutes
s["Rising"]=s.Ltp>s.Ltp15
# outcomes
cs={k:g.set_index("t") for k,g in c.groupby("Symbol")}
def outcome(sym,m,atr):
    g=cs[sym]; e=m+pd.Timedelta(minutes=1)
    if e not in g.index or not atr>0: return np.nan,np.nan
    w=g.loc[e:e+pd.Timedelta(minutes=45)]; ent=w.Open.iloc[0]; r=1.25*atr
    for i,(t,x) in enumerate(w.iterrows()):
        if x.Low<=ent-r: return -1.0,i+1
        if x.High>=ent+1.2*r: return 1.2,i+1
    return (w.Close.iloc[-1]-ent)/r,len(w)
v=[outcome(a,b,x) for a,b,x in zip(s.Symbol,s.m,s.AtrRaw)]; s["R"]=[x[0] for x in v]; s["Mins"]=[x[1] for x in v]
s=s.dropna(subset=["R"]); days=sorted(s.day.unique()); n=len(days); c1,c2=days[int(n*.6)],days[int(n*.8)]
s["Split"]=np.where(s.day<c1,"train",np.where(s.day<c2,"val","hold"))
s["Gap"]=np.select([(s.DayOpen/s.YesterdayClose-1)*100>=.3,(s.DayOpen/s.YesterdayClose-1)*100<=-.3],["GapUp","GapDown"],"Normal")
low0=s.Pcr0<s.groupby("day").Pcr0.transform("median"); PR=s.dPcr>.03; PF=s.dPcr<-.03
PEup=s.PeChg>1; CEdn=s.CeChg<0
cells={"BASE":s.R.notna(),"Underlying rising":s.Rising,"PutOI up":PEup,"CallOI down":CEdn,"PutOI up & CallOI down":PEup&CEdn,
 "PutOI up & CallOI down & rising":PEup&CEdn&s.Rising,"PCR rising":PR,"PCR rising & PutOI up & CallOI down":PR&PEup&CEdn,
 "lowOpenPCR & PCR rising":low0&PR,"lowOpenPCR & rising & PutOI up & CallOI down & Ltp rising":low0&PR&PEup&CEdn&s.Rising,
 "lowOpenPCR & PCR falling":low0&PF,"PCR rising but CallOI up (not bullish)":PR&~CEdn&~PEup}
def nonov(g):
    k,f=[],{}
    for i,sy,t,mm in zip(g.index,g.Symbol,g.m,g.Mins):
        if sy in f and t<f[sy]: continue
        k.append(i); f[sy]=t+pd.Timedelta(minutes=mm)
    return g.loc[k]
def st(g):
    if not len(g): return "n=0"
    w=g.R[g.R>0].sum(); l=-g.R[g.R<0].sum()
    return f"n={len(g)} d={g.day.nunique()} win={(g.R>0).mean()*100:.0f}% ExpR={g.R.mean():+.3f} PF={w/l if l else float('inf'):.2f}"
out=[]
for k,m in cells.items():
    g=nonov(s[m.fillna(False)])
    out.append(f"{k:70s} ALL {st(g)} | train {st(g[g.Split=='train'])} | val {st(g[g.Split=='val'])} | hold {st(g[g.Split=='hold'])}")
    for gp in ["GapUp","Normal","GapDown"]:
        gg=g[g.Gap==gp]
        if len(gg): out.append(f"      {gp:8s} {st(gg)}")
txt=f"sessions={n} train<{c1} hold>={c2} rows_with_OI={int(s.PeChg.notna().sum())}/{len(s)}\n"+"\n".join(out)
open("results.txt","w").write(txt); print(txt)
