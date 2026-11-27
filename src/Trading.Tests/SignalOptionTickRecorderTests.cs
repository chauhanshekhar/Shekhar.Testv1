using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Trading.Domain.Entities;
using Trading.Worker.Exits;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// The option-tick audit row must record the price the exit ladder acted on (ResolveLtps), say where
/// it came from, and must never be able to throw into ExitWorker. Added after signal 10449
/// (2026-11-25) exited PROTECTED_SL_HIT on an intra-minute dip that no stored data could confirm.
/// </summary>
public class SignalOptionTickRecorderTests
{
    private const string OptionSymbol = "SENSEX26NOV84200CE";
    private static readonly DateTime Now = new(2026, 11, 25, 10, 57, 58);

    private static Signal Open(long id = 10449) => new()
    {
        Id = id,
        Symbol = "SENSEX",
        StrategyType = "SINGLE",
        OptionSymbol = OptionSymbol,
        OptionSL = 419.88m,
        ProtectedSL = 473.30m,
        Status = "Active",
    };

    private static Dictionary<string, List<OptionChainData>> Chain(decimal ltp) => new()
    {
        ["SENSEX"] = [new OptionChainData { TradingSymbol = OptionSymbol, Ltp = ltp }]
    };

    private static Dictionary<string, decimal> Quotes(decimal? ltp) =>
        ltp is { } v
            ? new(StringComparer.OrdinalIgnoreCase) { [OptionSymbol] = v }
            : new(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void LiveQuote_IsRecordedAsQuote_WithTheStopsTheDecisionUsed()
    {
        var rows = SignalOptionTickRecorder.BuildRows(
            [Open()], Chain(476.35m), Quotes(473.10m), new() { ["SENSEX"] = 84203.71m }, Now);

        var row = Assert.Single(rows);
        Assert.Equal(10449, row.SignalId);
        Assert.Equal(473.10m, row.OptionLtp);
        Assert.Equal(SignalOptionTickSource.Quote, row.Source);
        Assert.Equal(84203.71m, row.UnderlyingLtp);
        Assert.Equal(473.30m, row.ProtectedSL);
        Assert.Equal(419.88m, row.OptionSL);
        Assert.Equal(Now, row.TickTime);
    }

    [Fact]
    public void NoQuote_FallsBackToChain_AndIsLabelledChain()
    {
        var row = Assert.Single(SignalOptionTickRecorder.BuildRows(
            [Open()], Chain(476.35m), Quotes(null), new() { ["SENSEX"] = 84203.71m }, Now));

        Assert.Equal(476.35m, row.OptionLtp);
        Assert.Equal(SignalOptionTickSource.Chain, row.Source);
    }

    [Fact]
    public void NoPriceAtAll_IsRecordedAsNone_SoGapsAreVisible()
    {
        var row = Assert.Single(SignalOptionTickRecorder.BuildRows(
            [Open()], new(), Quotes(null), new(), Now));

        Assert.Equal(0m, row.OptionLtp);
        Assert.Equal(SignalOptionTickSource.None, row.Source);
        Assert.Equal(0m, row.UnderlyingLtp);
    }

    [Fact]
    public void SignalWithoutOptionSymbol_IsSkipped()
    {
        var signal = Open();
        signal.OptionSymbol = "";

        Assert.Empty(SignalOptionTickRecorder.BuildRows([signal], new(), Quotes(null), new(), Now));
    }

    [Fact]
    public async Task RecordAsync_NeverThrows_WhenTheWriteFails()
    {
        // An empty provider cannot resolve IRepository<SignalOptionTick>, which stands in for any
        // storage failure (e.g. the table not migrated yet). The exit path must be unaffected.
        var scopes = new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var rows = SignalOptionTickRecorder.BuildRows([Open()], Chain(476.35m), Quotes(null), new(), Now);

        var ex = await Record.ExceptionAsync(() =>
            SignalOptionTickRecorder.RecordAsync(scopes, rows, NullLogger.Instance));

        Assert.Null(ex);
    }
}
