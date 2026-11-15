using Trading.Domain.Entities;
using Trading.Infrastructure.Services;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// IsActive semantics on StrategySettings.
///
/// THE INCIDENT THIS PINS (2026-09-10): ten Enable* filters were switched off with
/// `UPDATE StrategySettings SET IsActive = 0`, and every one of them stayed ENFORCED. Inactive
/// rows were filtered out of the cache, so each caller fell back to its compiled default — and
/// for every Enable* flag that default is `true`. Deactivating a filter turned it on. The
/// blocked-reason log named the filters that were supposedly disabled.
///
/// Now: an inactive BOOLEAN row means the feature is OFF. Numeric keys keep the old fallback,
/// because 0 is not a meaningful "off" for a threshold — it is permissive for a minimum and
/// prohibitive for a maximum, so only the compiled default is safe.
/// </summary>
public class StrategySettingsIsActiveTests
{
    private const string Symbol = "BANKNIFTY";

    private static StrategySettingsService WithRows(params StrategySetting[] rows)
    {
        // The database is never touched: BuildCache is pure and SetCacheForTest installs it.
        var svc = new StrategySettingsService(null!, Microsoft.Extensions.Logging.Abstractions
            .NullLogger<StrategySettingsService>.Instance);

        svc.SetCacheForTest(StrategySettingsService.BuildCache(rows));
        return svc;
    }

    private static StrategySetting Row(string key, decimal value, bool isActive, string? symbol = null) =>
        new() { SettingKey = key, SettingValue = value, IsActive = isActive, Symbol = symbol };

    // ── The bug ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void InactiveBooleanRow_TurnsTheFeatureOff_NotBackToItsDefault()
    {
        // SettingValue = 1 and IsActive = 0: exactly what the production UPDATE produced.
        var svc = WithRows(Row("EnableCandleFilter", 1m, isActive: false));

        // The caller's default is `true` - the old behaviour returned it and the filter stayed on.
        Assert.False(svc.GetBool("EnableCandleFilter", Symbol, defaultValue: true));
    }

    [Fact]
    public void ActiveBooleanRow_StillReadsItsValue()
    {
        Assert.True(WithRows(Row("EnableCandleFilter", 1m, true)).GetBool("EnableCandleFilter", Symbol, false));
        Assert.False(WithRows(Row("EnableCandleFilter", 0m, true)).GetBool("EnableCandleFilter", Symbol, true));
    }

    [Fact]
    public void MissingBooleanRow_StillFallsBackToTheDefault()
    {
        // "No row at all" is different from "row switched off" and must keep the old meaning,
        // otherwise every un-configured flag would silently turn off on deploy.
        var svc = WithRows();

        Assert.True(svc.GetBool("EnableCandleFilter", Symbol, defaultValue: true));
        Assert.False(svc.GetBool("EnableCandleFilter", Symbol, defaultValue: false));
    }

    // ── Numerics keep the old fallback ──────────────────────────────────────────────────────

    [Fact]
    public void InactiveNumericRow_FallsBackToTheDefault_NotZero()
    {
        // Returning 0 here would silently remove an ATR floor - permissive for a minimum, and
        // prohibitive if the same rule were applied to a maximum. The default is the safe read.
        var svc = WithRows(Row("MinAtrPercent", 0.035m, isActive: false));

        Assert.Equal(0.10m, svc.GetDecimal("MinAtrPercent", Symbol, defaultValue: 0.10m));
    }

    [Fact]
    public void ActiveNumericRow_IsUsed()
    {
        var svc = WithRows(Row("MinAtrPercent", 0.035m, isActive: true));

        Assert.Equal(0.035m, svc.GetDecimal("MinAtrPercent", Symbol, defaultValue: 0.10m));
    }

    [Fact]
    public void InactiveIntRow_FallsBackToTheDefault()
    {
        var svc = WithRows(Row("MaxStopLossRetries", 9m, isActive: false));

        Assert.Equal(3, svc.GetInt("MaxStopLossRetries", Symbol, defaultValue: 3));
    }

    // ── Symbol precedence ───────────────────────────────────────────────────────────────────

    [Fact]
    public void InactiveSymbolRow_TurnsItOffForThatSymbolOnly_AndDoesNotFallThroughToGlobal()
    {
        var svc = WithRows(
            Row("EnableCandleFilter", 1m, isActive: true),                      // global: ON
            Row("EnableCandleFilter", 1m, isActive: false, symbol: Symbol));    // BANKNIFTY: OFF

        Assert.False(svc.GetBool("EnableCandleFilter", Symbol, true));   // symbol row wins
        Assert.True(svc.GetBool("EnableCandleFilter", "NIFTY", true));   // others use global
    }

    [Fact]
    public void ActiveSymbolRow_StillOverridesGlobal()
    {
        var svc = WithRows(
            Row("MinAtrPercent", 0.030m, isActive: true),
            Row("MinAtrPercent", 0.010m, isActive: true, symbol: Symbol));

        Assert.Equal(0.010m, svc.GetDecimal("MinAtrPercent", Symbol, 0.10m));
        Assert.Equal(0.030m, svc.GetDecimal("MinAtrPercent", "NIFTY", 0.10m));
    }

    // ── IsSettingActive: the seam that lets a CONDITION skip itself ─────────────────────────

    [Fact]
    public void IsSettingActive_IsFalseWhenNoRowExists()
    {
        // No row means nothing has been configured for this symbol, so the condition does not
        // apply — it must not silently enforce whatever number is hardcoded at the call site.
        // GetDecimal still returns that compiled default on its own (see the next test); only
        // the paired IsSettingActive check treats "unconfigured" as "off".
        Assert.False(WithRows().IsSettingActive("CallMinAtrPercent", Symbol));
    }

    [Fact]
    public void GetDecimal_StillFallsBackToDefault_EvenThoughIsSettingActiveIsFalse()
    {
        // The two questions are independent: GetDecimal answers "what value", IsSettingActive
        // answers "does the condition apply". A confidence-only reader (BodyPctMinimum) only
        // ever asks the first question and is unaffected by this change.
        var svc = WithRows();

        Assert.Equal(0.03m, svc.GetDecimal("CallMinAtrPercent", Symbol, defaultValue: 0.03m));
        Assert.False(svc.IsSettingActive("CallMinAtrPercent", Symbol));
    }

    [Fact]
    public void IsSettingActive_TracksTheRowFlag_IndependentlyOfValue()
    {
        // SettingValue = 0 is a REAL value (an OI floor of exactly 0, say) and must not be
        // confused with "switched off". Only IsActive decides that.
        Assert.True(WithRows(Row("CallOiChangeMinimum", 0m, isActive: true))
            .IsSettingActive("CallOiChangeMinimum", Symbol));

        Assert.False(WithRows(Row("CallOiChangeMinimum", 0m, isActive: false))
            .IsSettingActive("CallOiChangeMinimum", Symbol));

        // A non-zero value on an inactive row is still off.
        Assert.False(WithRows(Row("CallRsiCeiling", 70m, isActive: false))
            .IsSettingActive("CallRsiCeiling", Symbol));
    }

    [Fact]
    public void IsSettingActive_HonoursSymbolPrecedence()
    {
        var svc = WithRows(
            Row("CallMinAtrPercent", 0.03m, isActive: true),
            Row("CallMinAtrPercent", 0.03m, isActive: false, symbol: Symbol));

        Assert.False(svc.IsSettingActive("CallMinAtrPercent", Symbol));
        Assert.True(svc.IsSettingActive("CallMinAtrPercent", "NIFTY"));
    }

    [Fact]
    public void DeactivatedCeiling_DoesNotBecomeZero()
    {
        // The hazard this design avoids: if an inactive CallRsiCeiling resolved to 0, the
        // condition would read "RSI <= 0" and no CALL could ever pass. GetDecimal returns the
        // caller's default instead, and the CONDITION is skipped via IsSettingActive.
        var svc = WithRows(Row("CallRsiCeiling", 70m, isActive: false));

        Assert.Equal(70m, svc.GetDecimal("CallRsiCeiling", Symbol, defaultValue: 70m));
        Assert.NotEqual(0m, svc.GetDecimal("CallRsiCeiling", Symbol, defaultValue: 70m));
        Assert.False(svc.IsSettingActive("CallRsiCeiling", Symbol));
    }

    // ── The ten filters from the incident ───────────────────────────────────────────────────

    [Theory]
    [InlineData("EnableAtrFilter")]
    [InlineData("EnableCandleFilter")]
    [InlineData("EnableCallPcrFilter")]
    [InlineData("EnablePutPcrFilter")]
    [InlineData("EnableCallOiFilter")]
    [InlineData("EnablePutOiFilter")]
    [InlineData("EnableCallPrev5mHighFilter")]
    [InlineData("EnablePutPrev5mLowFilter")]
    [InlineData("EnableCallDayFilter")]
    [InlineData("EnablePutDayFilter")]
    public void EveryFilterFromTheIncident_IsActuallyOffWhenDeactivated(string key)
    {
        var svc = WithRows(Row(key, 1m, isActive: false));

        Assert.False(svc.GetBool(key, Symbol, defaultValue: true));
    }
}
