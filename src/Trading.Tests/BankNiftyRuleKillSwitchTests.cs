using Trading.Domain.Entities;
using Trading.SignalEngine.Strategies;
using Trading.Tests.Fakes;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// The BANKNIFTY PUT BREAKDOWN rule's kill switch.
///
/// Before this existed the rule was evaluated unconditionally and could only be disabled by
/// editing code and redeploying — on a live account that is the wrong thing to need in a hurry.
/// The thresholds themselves stay hardcoded on purpose (a runtime override would silently
/// invalidate the measured win rate); only ELIGIBILITY is switchable.
///
/// Default MUST be enabled, so deploying the switch changes nothing until a StrategySettings row
/// says otherwise.
/// </summary>
public class BankNiftyRuleKillSwitchTests
{
    /// <summary>A snapshot satisfying every condition of the rule.</summary>
    private static IndicatorSnapshot Matching() => new()
    {
        Symbol = "BANKNIFTY",
        TrendDirection = "Bearish",
        MarketState = "Breakdown",
        Ltp = 56_800m,
        Ema20 = 56_900m,
        Ema50 = 57_000m,
        Ema20_5m = 56_850m,
        Ema50_5m = 56_950m,
        Rsi = 30m,                  // <= 35
        PriceChangePct = -0.45m,    // <= -0.20
        AtrPct = 0.041m,            // >= 0.030
        Confidence = 50m,
    };

    private static SingleSignalRuleEvaluator WithSetting(decimal? enabled)
    {
        var settings = new FakeStrategySettingsService();
        if (enabled.HasValue)
            settings.Set(SingleSignalRuleEvaluator.EnabledKey, enabled.Value);

        return new SingleSignalRuleEvaluator(settings);
    }

    [Fact]
    public void NoSettingsRow_RuleStaysEnabled()
    {
        // Deploying the switch must not change live behaviour on its own.
        var result = WithSetting(null).Evaluate(Matching());

        Assert.True(result.Matched);
        Assert.Equal(SingleSignalRuleEvaluator.BankNiftyPutBreakdownRuleId, result.RuleId);
    }

    [Fact]
    public void ParameterlessConstructor_RuleStaysEnabled()
    {
        // The constructor every existing test uses — no settings service at all.
        Assert.True(new SingleSignalRuleEvaluator().Evaluate(Matching()).Matched);
    }

    [Fact]
    public void SettingSetToOne_RuleEnabled()
    {
        Assert.True(WithSetting(1m).Evaluate(Matching()).Matched);
    }

    [Fact]
    public void SettingSetToZero_RuleSuppressed_AndSaysWhy()
    {
        var result = WithSetting(0m).Evaluate(Matching());

        Assert.False(result.Matched);
        Assert.Null(result.RuleId);
        Assert.Contains(SingleSignalRuleEvaluator.EnabledKey, result.Reason);
    }

    [Fact]
    public void SettingSetToZero_DoesNotAlterTheRuleItself()
    {
        // The switch gates eligibility only. The pure evaluation must still report the true
        // verdict, so a disabled rule can still be measured against live data.
        var evaluator = WithSetting(0m);

        Assert.False(evaluator.IsBankNiftyRuleEnabled());
        Assert.True(evaluator.EvaluateBankNiftyPutBreakdown(Matching()).Matched);
    }
}
