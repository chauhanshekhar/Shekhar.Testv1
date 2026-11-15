using System.Text.Json;
using Trading.Application.Interfaces;
using Trading.Application.Models;
using Trading.Domain.Entities;
using Trading.Infrastructure.Brokers.Groww;
using Trading.SignalEngine.Services;
using Trading.Worker;
using Trading.Worker.SignalGates;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// Until 2026-09-30 the interval was sent as candle_interval=5min to /v1/historical/candle/range,
/// which does not know that parameter, so Groww returned 1-minute candles for "SYMBOL_5m" too and
/// every "5m" indicator (Rsi5m, Ema20_5m/Ema50_5m, Prev5mHigh/Low) was 1-minute data lagged ~5 min.
/// </summary>
public class CandleIntervalTests
{
    private sealed class CapturingHttp : IGrowwHttpService
    {
        private readonly string _json;
        public string? LastUrl { get; private set; }
        public CapturingHttp(string json) => _json = json;

        public Task<GrowwHttpResult<T>> GetAsync<T>(string url, string? signalId = null, string? orderReferenceId = null)
        {
            LastUrl = url;
            var data = JsonDocument.Parse(_json).RootElement.Clone();
            return Task.FromResult(new GrowwHttpResult<T> { IsSuccess = true, Data = (T)(object)data });
        }

        public Task<GrowwHttpResult<T>> PostAsync<T>(string url, object body, string? signalId = null, string? orderReferenceId = null)
            => throw new NotSupportedException();
        public Task<GrowwHttpResult<T>> PutAsync<T>(string url, object body, string? signalId = null, string? orderReferenceId = null)
            => throw new NotSupportedException();
    }

    private static string Payload(int servedMinutes) =>
        "{\"status\":\"SUCCESS\",\"payload\":{\"candles\":[[1727669700,100.0,105.0,99.0,104.0,0]]," +
        $"\"start_time\":\"2026-09-30 09:15:00\",\"end_time\":\"2026-09-30 09:20:00\",\"interval_in_minutes\":{servedMinutes}}}}}";

    // ── interval -> minutes ──────────────────────────────────────────────────

    [Theory]
    [InlineData(GrowwInterval.OneMinute, 1)]
    [InlineData(GrowwInterval.FiveMinute, 5)]
    [InlineData(GrowwInterval.FifteenMinute, 15)]
    [InlineData(GrowwInterval.OneHour, 60)]
    [InlineData(GrowwInterval.OneDay, 1440)]
    [InlineData("10", 10)]
    public void ToMinutes_MapsEveryUsedInterval(string interval, int minutes)
        => Assert.Equal(minutes, GrowwInterval.ToMinutes(interval));

    [Theory]
    [InlineData("5minute")]
    [InlineData(GrowwInterval.OneMonth)]
    [InlineData("0")]
    public void ToMinutes_RefusesWhatItCannotExpress(string interval)
        => Assert.Throws<ArgumentOutOfRangeException>(() => GrowwInterval.ToMinutes(interval));

    // ── the request ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Request_SendsIntervalInMinutes_NotCandleInterval()
    {
        var http = new CapturingHttp(Payload(5));

        await new GrowwHistoricalDataService(http).GetCandlesAsync(
            GrowwExchange.Nse, "NIFTY", GrowwInterval.FiveMinute,
            new DateTime(2026, 9, 30, 9, 15, 0), new DateTime(2026, 9, 30, 9, 20, 0));

        Assert.Contains("interval_in_minutes=5", http.LastUrl);
        Assert.DoesNotContain("candle_interval", http.LastUrl);
    }

    [Fact]
    public async Task Response_WithTheRequestedInterval_IsParsed()
    {
        var candles = await new GrowwHistoricalDataService(new CapturingHttp(Payload(5))).GetCandlesAsync(
            GrowwExchange.Nse, "NIFTY", GrowwInterval.FiveMinute,
            new DateTime(2026, 9, 30, 9, 15, 0), new DateTime(2026, 9, 30, 9, 20, 0));

        var c = Assert.Single(candles);
        Assert.Equal(104.0m, c.Close);
    }

    [Fact]
    public async Task Response_WithADifferentInterval_IsRefused()
    {
        // Groww served 1-minute bars for a 5-minute request: must not reach SYMBOL_5m.
        var svc = new GrowwHistoricalDataService(new CapturingHttp(Payload(1)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.GetCandlesAsync(
            GrowwExchange.Nse, "NIFTY", GrowwInterval.FiveMinute,
            new DateTime(2026, 9, 30, 9, 15, 0), new DateTime(2026, 9, 30, 9, 20, 0)));
    }

    // ── RSI stamped on the signal: 5-minute in both columns ──────────────────

    private static MarketSnapshot Snapshot(decimal rsi1m, decimal? rsi5m) => new(
        Candles1m: new List<Candle>(), Candles5m: new List<Candle>(), OptionChain: new List<OptionChainData>(),
        PrevOiByKey: new Dictionary<(decimal, string), long>(),
        MarketRegime: new MarketRegime(MarketTrend.Uptrend, MarketVolatility.High, false, false, false, 0m),
        Ema20: 0m, Ema50: 0m, Ema20_5m: 0m, Ema50_5m: 0m,
        Rsi: rsi1m, Rsi5m: rsi5m, AtrPct: 0m, AtrRaw: 0m, Ltp: 0m, YesterdayClose: 0m,
        CurrentVolume: 0m, AvgVolume20: 0m, PriceChangePct: 0m,
        DayOpen: 0m, DayLow: 0m, DayHigh: 0m, DayChangePct: 0m, Vwap: null, PcrValue: 0m,
        StrategyHint: "", NewsEvent: null,
        LastTickAge: TimeSpan.Zero, LastOptionChainAge: TimeSpan.Zero, LastClosed5mCandle: null);

    [Fact]
    public void EntryRsi_BothColumnsGetThe5MinuteRsi()
    {
        // Signal 98's values: 1m 71.58, 5m 51.82 — the Signals table must show 51.82 in both.
        var (rsiAtEntry, rsi5mAtEntry) = SignalOrderPlacementService.EntryRsi(Snapshot(71.58m, 51.82m));

        Assert.Equal(51.82m, rsiAtEntry);
        Assert.Equal(51.82m, rsi5mAtEntry);
    }

    [Fact]
    public void EntryRsi_No5MinuteRsiYet_FallsBackTo1Minute_AndMarksItNull()
    {
        var (rsiAtEntry, rsi5mAtEntry) = SignalOrderPlacementService.EntryRsi(Snapshot(71.58m, null));

        Assert.Equal(71.58m, rsiAtEntry);
        Assert.Null(rsi5mAtEntry);
    }
}
