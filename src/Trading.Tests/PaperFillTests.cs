using Trading.Infrastructure.Brokers.Groww;
using Trading.SignalEngine.Services;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// Paper mode used to leave every entry ORDER_PLACED (PaperOrderStrategy.GetOrderStatusAsync
/// returned null), so ExitWorker refused every exit as "entry never filled" and paper trades never
/// closed. Paper orders now report COMPLETE through the same BrokerOrderSyncWorker path as live.
/// DB-backed lookups are covered by the Worker running against the paper DB; these cover the pure
/// parts and the branches that never touch the DB.
/// </summary>
public class PaperFillTests
{
    // ── close-fill id round trip ─────────────────────────────────────────────

    [Fact]
    public void CloseFillId_RoundTripsTradeLotsAndPrice()
    {
        var id = PaperOrderStrategy.FormatCloseFillId(5, 2, 320.17m);

        Assert.True(PaperOrderStrategy.TryParseCloseFillId(id, out var tradeId, out var lots, out var price));
        Assert.Equal(5, tradeId);
        Assert.Equal(2, lots);
        Assert.Equal(320.17m, price);
    }

    [Fact]
    public void CloseFillId_IsUniquePerClose_AndFitsTheColumn()
    {
        // Orders.BrokerOrderId is UNIQUE and nvarchar(50): a partial close and the final close of the
        // same paper trade must not collide.
        var a = PaperOrderStrategy.FormatCloseFillId(123456, 2, 99999.99m);
        var b = PaperOrderStrategy.FormatCloseFillId(123456, 2, 99999.99m);

        Assert.NotEqual(a, b);
        Assert.True(a.Length <= 50);
        Assert.True(PaperOrderStrategy.TryParseCloseFillId(a, out _, out _, out var price));
        Assert.Equal(99999.99m, price);
    }

    [Theory]
    [InlineData("PAPER-5")]
    [InlineData("PAPER-SL-5-abc")]
    [InlineData("GMKFO26093009470814SWFDEX3J73")]
    public void CloseFillId_OnlyParsesCloseFills(string id)
        => Assert.False(PaperOrderStrategy.TryParseCloseFillId(id, out _, out _, out _));

    // ── status branches that do not need the DB ──────────────────────────────

    [Fact]
    public async Task Status_LiveOrderId_IsNotHandledByPaper()
        => Assert.Null(await new PaperOrderStrategy(null!).GetOrderStatusAsync("GMKFO26093009470814SWFDEX3J73"));

    [Fact]
    public async Task Status_PaperStop_IsResting()
        => Assert.Equal("OPEN", (await new PaperOrderStrategy(null!).GetOrderStatusAsync("PAPER-SL-99-abcdef"))!.Status);

    [Fact]
    public async Task Status_FailedPaperClose_IsRejected_NotFilled()
        => Assert.Equal("REJECTED", (await new PaperOrderStrategy(null!).GetOrderStatusAsync("PAPER-CLOSE-NOTFOUND-abc"))!.Status);

    // ── paper fill price ─────────────────────────────────────────────────────

    [Fact]
    public void FillPrice_UsesTheDecisionPrice_NotTheStaleChain()
        // Signal 101, 2026-09-30: T1 fired with the premium above 280.06 while the last chain
        // snapshot (33 s old) still said 266.55. The fill must be the decision price.
        => Assert.Equal(281.40m, PaperOrderStrategy.PaperFillPrice(281.40m, 266.55m, 262.90m));

    [Fact]
    public void FillPrice_FallsBackToChain_ThenEntry()
    {
        Assert.Equal(266.55m, PaperOrderStrategy.PaperFillPrice(null, 266.55m, 262.90m));
        Assert.Equal(266.55m, PaperOrderStrategy.PaperFillPrice(0m, 266.55m, 262.90m));
        Assert.Equal(262.90m, PaperOrderStrategy.PaperFillPrice(null, null, 262.90m));
    }

    // ── partial close: broker FilledQty is contracts, lots are lots ──────────

    [Theory]
    [InlineData(40, 2, 20, 2)]    // SENSEX: 2 lots = 40 contracts -> 2 lots (was treated as 40)
    [InlineData(20, 1, 20, 1)]
    [InlineData(130, 2, 65, 2)]   // NIFTY
    [InlineData(0, 2, 20, 2)]     // no fill qty reported -> ordered lots
    [InlineData(2, 2, 1, 2)]      // lot size unknown (1) -> take the number as is
    public void PartialCloseLots_ConvertsContractsToLots(int filled, int ordered, int lotSize, int expected)
        => Assert.Equal(expected, TradeCompletionService.PartialCloseLots(filled, ordered, lotSize));
}
