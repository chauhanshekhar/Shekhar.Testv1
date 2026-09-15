import io, sys

def load(p): return io.open(p, encoding='utf-8-sig').read()
def save(p, s): io.open(p, 'w', encoding='utf-8-sig', newline='').write(s)

def rep(s, label, old, new, count=1):
    n = s.count(old)
    if n != count:
        print("  [%s] count=%d expected=%d FAIL" % (label, n, count)); sys.exit(1)
    print("  [%s] ok" % label)
    return s.replace(old, new)

# ══ StrategyManager: confidence floors + expiry cutoff ═════════════════════
p = 'src/Trading.Worker/StrategyManager.cs'
s = load(p)

s = rep(s, "confidence",
'''        var minConfidence = ctx.StrategySettings.GetDecimal("MinSignalConfidence", symbol, 0m);

        if (decision.Strategy == SelectedStrategy.Directional)
        {
            var minDirectional = ctx.StrategySettings.GetDecimal("MinDirectionalConfidence", symbol, 55m);
            if (minDirectional > minConfidence) minConfidence = minDirectional;
        }''',
'''        // A DEACTIVATED ROW MEANS THE FLOOR DOES NOT APPLY - not that its default returns.
        // SettingValue = 0 is a real value here (0 = no floor); IsActive = 0 removes the check.
        var minConfidence = ctx.StrategySettings.IsSettingActive("MinSignalConfidence", symbol)
            ? ctx.StrategySettings.GetDecimal("MinSignalConfidence", symbol, 0m)
            : 0m;

        if (decision.Strategy == SelectedStrategy.Directional
            && ctx.StrategySettings.IsSettingActive("MinDirectionalConfidence", symbol))
        {
            var minDirectional = ctx.StrategySettings.GetDecimal("MinDirectionalConfidence", symbol, 55m);
            if (minDirectional > minConfidence) minConfidence = minDirectional;
        }''')

s = rep(s, "expiry-cutoff",
'''        var expiryCutoff = ctx.StrategySettings.GetDecimal("ExpiryDaySingleCutoffHour", symbol, 11.5m);
        if (expiryCutoff > 0''',
'''        // Deactivating the row switches the cutoff off entirely, same as setting it to 0.
        var expiryCutoff = ctx.StrategySettings.IsSettingActive("ExpiryDaySingleCutoffHour", symbol)
            ? ctx.StrategySettings.GetDecimal("ExpiryDaySingleCutoffHour", symbol, 11.5m)
            : 0m;

        if (expiryCutoff > 0''')
save(p, s)

# ══ DuplicateSignalGate: rejected-entry cooldown ═══════════════════════════
p = 'src/Trading.Worker/SignalGates/DuplicateSignalGate.cs'
s = load(p)
s = rep(s, "cooldown",
'            ctx.StrategySettings.GetInt("RejectedEntryCooldownMinutes", ctx.Symbol, 5);',
'''            ctx.StrategySettings.IsSettingActive("RejectedEntryCooldownMinutes", ctx.Symbol)
                ? ctx.StrategySettings.GetInt("RejectedEntryCooldownMinutes", ctx.Symbol, 5)
                : 0;   // deactivated row = no cooldown, rather than the compiled 5 minutes''')
save(p, s)

# ══ ExitDecisionService: ProtectedSL trailing ══════════════════════════════
p = 'src/Trading.Worker/Exits/ExitDecisionService.cs'
s = load(p)
s = rep(s, "protectedsl",
'''        var trailFactor      = _strategySettings.GetDecimal("ProtectedSlTrailFactor", signal.Symbol, 0.25m);
        var minMovePercent   = _strategySettings.GetDecimal("ProtectedSlMinMovePercent", signal.Symbol, 5.0m);
        var breakevenBuffer  = _strategySettings.GetDecimal("ProtectedSlBreakevenBufferPercent", signal.Symbol, 1.0m);''',
'''        var trailFactor      = _strategySettings.GetDecimal("ProtectedSlTrailFactor", signal.Symbol, 0.25m);
        var minMovePercent   = _strategySettings.GetDecimal("ProtectedSlMinMovePercent", signal.Symbol, 5.0m);
        var breakevenBuffer  = _strategySettings.GetDecimal("ProtectedSlBreakevenBufferPercent", signal.Symbol, 1.0m);

        // Deactivating ProtectedSlTrailFactor switches OFF protected-stop arming and trailing
        // altogether: IndexT1Hit is still recorded for the audit, but no ProtectedSL level is set
        // and rule 4 below can never fire (it requires ProtectedSL > 0). An existing level on a
        // signal already in flight is left untouched - the stop that is protecting a live position
        // is never silently withdrawn.
        var protectedSlActive = _strategySettings.IsSettingActive("ProtectedSlTrailFactor", signal.Symbol);''')

s = rep(s, "protectedsl-arm",
'''                signal.ProtectedSL = Math.Abs(optionMovePct) <= minMovePercent''',
'''                if (protectedSlActive)
                    signal.ProtectedSL = Math.Abs(optionMovePct) <= minMovePercent''')

s = rep(s, "protectedsl-ratchet",
'''        if (signal.IndexT1Hit && optionLtp > 0 && signal.OptionLtp > 0)
        {
            var move = optionLtp - signal.OptionLtp;''',
'''        if (protectedSlActive && signal.IndexT1Hit && optionLtp > 0 && signal.OptionLtp > 0)
        {
            var move = optionLtp - signal.OptionLtp;''')
save(p, s)

# ══ SignalEngineService: option SL %, legacy confidence bands, pullback ════
p = 'src/Trading.SignalEngine/Services/SignalEngineService.cs'
s = load(p)

s = rep(s, "option-sl-pct",
'        var optionSlPercent = _strategySettings.GetDecimal("OptionStopLossPercent", symbol, 30m);',
'''        // Deactivating the row means NO option stop is set (OptionSL = 0), the same state
        // HeroZero uses - StopLossOrderCoordinator refuses a non-positive trigger and the exit
        // ladder skips its OptionSL rule. It does NOT fall back to the compiled 30%.
        var optionSlPercent = _strategySettings.IsSettingActive("OptionStopLossPercent", symbol)
            ? _strategySettings.GetDecimal("OptionStopLossPercent", symbol, 30m)
            : 0m;''')

s = rep(s, "option-sl-apply",
'        optionSL = Math.Round(optionLtp * (1m - optionSlPercent / 100m), 2);',
'''        optionSL = optionSlPercent <= 0m
            ? 0m
            : Math.Round(optionLtp * (1m - optionSlPercent / 100m), 2);''')

# pullback band: deactivating either bound disables the contrarian alternative
s = rep(s, "call-pullback",
'''        var enableRsiPullback = _strategySettings.GetBool("EnableRsiPullbackFilter", symbol, false);
        var enableVwap        = _strategySettings.GetBool("EnableCallVwapFilter", symbol, false);''',
'''        var enableRsiPullback = _strategySettings.GetBool("EnableRsiPullbackFilter", symbol, false)
                             && _strategySettings.IsSettingActive("RsiCallPullbackFloor", symbol)
                             && _strategySettings.IsSettingActive("RsiCallPullbackCeiling", symbol);
        var enableVwap        = _strategySettings.GetBool("EnableCallVwapFilter", symbol, false);''')

s = rep(s, "put-bounceback",
'''        var enableRsiPullback = _strategySettings.GetBool("EnableRsiPullbackFilter", symbol, false);
        var enableVwap        = _strategySettings.GetBool("EnablePutVwapFilter", symbol, false);''',
'''        var enableRsiPullback = _strategySettings.GetBool("EnableRsiPullbackFilter", symbol, false)
                             && _strategySettings.IsSettingActive("RsiPutBouncebackFloor", symbol)
                             && _strategySettings.IsSettingActive("RsiPutBouncebackCeiling", symbol);
        var enableVwap        = _strategySettings.GetBool("EnablePutVwapFilter", symbol, false);''')

# retired PriceChangePct conditions (both sides) - deactivating the threshold disables them
s = rep(s, "price-change-retired",
'''        var priceChangeThreshold = _strategySettings.GetDecimal("PriceChangeThreshold", symbol, 0.10m);''',
'''        var priceChangeThreshold = _strategySettings.GetDecimal("PriceChangeThreshold", symbol, 0.10m);
        var priceChangeActive    = _strategySettings.IsSettingActive("PriceChangeThreshold", symbol);''',
 count=2)

s = rep(s, "call-pc-cond",
'''                !enablePriceChange || m.PriceChangePct > priceChangeThreshold),''',
'''                !enablePriceChange || !priceChangeActive || m.PriceChangePct > priceChangeThreshold),''')

s = rep(s, "put-pc-cond",
'''                !enablePriceChange || m.PriceChangePct < -priceChangeThreshold),''',
'''                !enablePriceChange || !priceChangeActive || m.PriceChangePct < -priceChangeThreshold),''')
save(p, s)

print("all wired")
