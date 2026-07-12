using Trading.Application.Models;
using System.Text.Json;
using Trading.Application.Interfaces;
using Trading.Domain.Entities;

namespace Trading.Infrastructure.Brokers.Groww;

// Fetches OHLCV candles from Groww historical data endpoint.
// GET /v1/historical/candle/range?exchange=NSE&segment=FNO&trading_symbol=NIFTY2661626100CE
//     &start_time=2025-09-24 10:56:00&end_time=2025-09-24 15:21:00&candle_interval=10
public class GrowwHistoricalDataService : IHistoricalDataService
{
    private readonly IGrowwHttpService _growwHttp;

    public GrowwHistoricalDataService(IGrowwHttpService growwHttp)
    {
        _growwHttp = growwHttp;
    }

    // exchange  — NSE / BSE
    // token     — base trading symbol (e.g. WIPRO, NIFTY24DEC24000CE)
    // interval  — use GrowwInterval constants (1minute, 5minute, 15minute, …)
    // from/to   — datetime range
    public async Task<List<Candle>> GetCandlesAsync(
        string exchange,
        string token,
        string interval,
        DateTime from,
        DateTime to,
        string segment = GrowwSegment.Cash)
    {
        var symbol = token;
        var startTime = from.ToString("yyyy-MM-dd HH:mm:ss");
        var endTime = to.ToString("yyyy-MM-dd HH:mm:ss");
        var url = GrowwEndpoint.HistoricalCandles +
                  $"?exchange={Uri.EscapeDataString(exchange)}" +
                  $"&segment={Uri.EscapeDataString(segment)}" +
                  $"&trading_symbol={Uri.EscapeDataString(symbol)}" +
                  $"&start_time={Uri.EscapeDataString(startTime)}" +
                  $"&end_time={Uri.EscapeDataString(endTime)}" +
                  $"&candle_interval={Uri.EscapeDataString(interval)}";

        var result = await _growwHttp.GetAsync<JsonElement>(url);
        if (!result.IsSuccess)
            throw new InvalidOperationException(
                $"Groww historical API error {result.StatusCode}: {result.ErrorMessage}");

        var doc = result.Data;

        if (!doc.TryGetProperty("payload", out var payloadEl))
            return [];

        if (!payloadEl.TryGetProperty("candles", out var candlesEl))
            return [];

        var candles = new List<Candle>();

        foreach (var row in candlesEl.EnumerateArray())
        {
            var arr = row.EnumerateArray().ToArray();
            if (arr.Length < 6) continue;

            // arr[0]=epoch seconds, arr[1]=open, arr[2]=high, arr[3]=low, arr[4]=close, arr[5]=volume, arr[6]=open_interest
            DateTime candleTime;
            if (arr[0].ValueKind == JsonValueKind.Number)
                candleTime = DateTimeOffset.FromUnixTimeSeconds(arr[0].GetInt64()).UtcDateTime;
            else if (arr[0].ValueKind == JsonValueKind.String)
                candleTime = DateTime.SpecifyKind(DateTime.Parse(arr[0].GetString()!), DateTimeKind.Utc);
            else
                continue;

            candles.Add(new Candle
            {
                Symbol = token,
                CandleTime = candleTime,
                Open = arr[1].GetDecimal(),
                High = arr[2].GetDecimal(),
                Low = arr[3].GetDecimal(),
                Close = arr[4].GetDecimal(),
                Volume = arr[5].ValueKind == JsonValueKind.Null ? 0 : arr[5].GetInt64(),
                OpenInterest = arr.Length > 6 && arr[6].ValueKind != JsonValueKind.Null
                                   ? arr[6].GetInt64()
                                   : 0
            });
        }

        return candles;
    }



    // Get available contract symbols for a given expiry date.
    public async Task<List<string>> GetContractsAsync(
        string exchange,
        string underlyingSymbol,
        string expiryDate)
    {
        var url = GrowwEndpoint.HistoricalContracts +
                  $"?exchange={Uri.EscapeDataString(exchange)}" +
                  $"&underlying_symbol={Uri.EscapeDataString(underlyingSymbol)}" +
                  $"&expiry_date={Uri.EscapeDataString(expiryDate)}";

        var result = await _growwHttp.GetAsync<GrowwResponse<ContractsPayload>>(url);
        return result.Data?.Payload?.Contracts ?? [];
    }
}
