using Trading.Application.Interfaces;
using Trading.Application.Models;
using Trading.Domain.Entities;
using Trading.Infrastructure.Brokers.Groww;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// Paper entries fill at the LIVE option quote at order time, not the signal's chain snapshot.
/// The chain refreshes about once a minute; on 27 paper entries 2026-09-30..10-05 the snapshot was
/// 42 s old on average and the fill was off by 7.3 premium points (Rs 781 per trade).
/// </summary>
public class PaperEntryFillTests
{
    private sealed class FakeLiveData : IGrowwLiveDataService
    {
        public decimal? Ltp { get; init; }
        public bool Throw { get; init; }
        public int Calls { get; private set; }
        public string? LastExchange { get; private set; }
        public string? LastSegment { get; private set; }
        public string? LastSymbol { get; private set; }

        public Task<GrowwQuotePayload?> GetQuoteAsync(string exchange, string segment, string tradingSymbol)
        {
            Calls++; LastExchange = exchange; LastSegment = segment; LastSymbol = tradingSymbol;
            if (Throw) throw new HttpRequestException("quote endpoint down");
            return Task.FromResult<GrowwQuotePayload?>(new GrowwQuotePayload { Ltp = Ltp });
        }

        public Task<Dictionary<string, decimal>> GetLtpAsync(string segment, IEnumerable<string> exchangeSymbols) => throw new NotSupportedException();
        public Task<Dictionary<string, OhlcEntry>> GetOhlcAsync(string segment, IEnumerable<string> exchangeSymbols) => throw new NotSupportedException();
        public Task<OptionChainPayload?> GetOptionChainAsync(string exchange, string underlying, string expiryDate) => throw new NotSupportedException();
        public Task<GreeksPayload?> GetGreeksAsync(string exchange, string underlying, string tradingSymbol, string expiry) => throw new NotSupportedException();
    }

    // Signal 129 (2026-10-05): built on the 14:00:31 snapshot (420.55); the market moved meanwhile.
    private static Signal Signal129() => new()
    {
        Symbol = "SENSEX", Exchange = "BSE", OptionSymbol = "SENSEX26O0872500CE",
        OptionLtp = 420.55m, EntryPrice = 72317.56m,
    };

    // ── Fill price ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void LiveQuote_WinsOverTheChainPremium()
        => Assert.Equal(414.45m, PaperOrderStrategy.PaperEntryPrice(414.45m, 420.55m, 72317.56m));

    [Theory]
    [InlineData(null)]
    [InlineData(0d)]
    public void NoUsableQuote_FallsBackToTheChainPremium(double? quote)
        => Assert.Equal(420.55m, PaperOrderStrategy.PaperEntryPrice((decimal?)quote, 420.55m, 72317.56m));

    [Fact]
    public void NoQuoteAndNoPremium_FallsBackToTheEntryPrice()
        => Assert.Equal(72317.56m, PaperOrderStrategy.PaperEntryPrice(null, 0m, 72317.56m));

    // ── Quote lookup ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FetchLiveQuote_ReturnsTheQuoteForTheTradedOption()
    {
        var live = new FakeLiveData { Ltp = 414.45m };

        var quote = await PaperOrderStrategy.FetchLiveQuoteAsync(live, Signal129(), null);

        Assert.Equal(414.45m, quote);
        Assert.Equal(("BSE", GrowwSegment.Fno, "SENSEX26O0872500CE"), (live.LastExchange, live.LastSegment, live.LastSymbol));
    }

    [Fact]
    public async Task FetchLiveQuote_ServiceThrows_ReturnsNullWithoutThrowing()
        => Assert.Null(await PaperOrderStrategy.FetchLiveQuoteAsync(new FakeLiveData { Throw = true }, Signal129(), null));

    [Fact]
    public async Task FetchLiveQuote_ZeroQuote_ReturnsNull()
        => Assert.Null(await PaperOrderStrategy.FetchLiveQuoteAsync(new FakeLiveData { Ltp = 0m }, Signal129(), null));

    [Fact]
    public async Task FetchLiveQuote_NoService_ReturnsNull()
        => Assert.Null(await PaperOrderStrategy.FetchLiveQuoteAsync(null, Signal129(), null));

    [Fact]
    public async Task FetchLiveQuote_NoOptionSymbol_DoesNotCallTheService()
    {
        var live = new FakeLiveData { Ltp = 414.45m };
        var signal = Signal129();
        signal.OptionSymbol = "";

        Assert.Null(await PaperOrderStrategy.FetchLiveQuoteAsync(live, signal, null));
        Assert.Equal(0, live.Calls);
    }
}
