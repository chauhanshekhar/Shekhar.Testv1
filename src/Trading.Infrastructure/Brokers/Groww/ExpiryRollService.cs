using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Trading.Infrastructure.Brokers.Groww;

/// <summary>
/// Finds the next option expiry for an index from Groww's public instrument CSV.
///
/// WHY THE CSV: Groww's documented REST API has no call that lists expiries (the option-chain call
/// needs an expiry as INPUT). The instrument CSV is the documented source: each option row carries
/// exchange, underlying_symbol, instrument_type (CE/PE) and expiry_date. It needs no credentials and
/// is not part of the rate-limited trading API, so this never touches the Groww request budget.
///
/// The file is large, so it is streamed line by line and reduced to one date per symbol; it is never
/// held in memory. Columns are located by HEADER NAME, not position, so a reordered file still parses.
/// If a required column is missing, or no row matches, nothing is returned and the caller keeps
/// whatever date it already has — a date is never invented.
/// </summary>
public class ExpiryRollService
{
    public const string InstrumentCsvUrl = "https://growwapi-assets.groww.in/instruments/instrument.csv";

    /// <summary>A nearest expiry further out than this is treated as implausible and not applied.</summary>
    public const int MaxPlausibleDaysAhead = 45;

    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<ExpiryRollService> _logger;

    public ExpiryRollService(IHttpClientFactory httpFactory, ILogger<ExpiryRollService> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    /// <summary>
    /// Nearest expiry on or after <paramref name="today"/> for each (symbol, exchange) pair, as
    /// yyyy-MM-dd. Symbols with no match are absent from the result. Returns an empty dictionary on
    /// any download or parse failure.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> GetNearestExpiriesAsync(
        IEnumerable<(string Symbol, string Exchange)> instruments, DateTime today, CancellationToken ct = default)
    {
        try
        {
            var client = _httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(120);

            using var response = await client.GetAsync(InstrumentCsvUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Groww instrument CSV download failed: HTTP {Status}", (int)response.StatusCode);
                return new Dictionary<string, string>();
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);
            return await NearestExpiriesAsync(reader, instruments, today, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Groww instrument CSV could not be read");
            return new Dictionary<string, string>();
        }
    }

    /// <summary>Reduces the CSV to the nearest on-or-after-today option expiry per symbol. Exposed for testing.</summary>
    internal static async Task<IReadOnlyDictionary<string, string>> NearestExpiriesAsync(
        TextReader reader, IEnumerable<(string Symbol, string Exchange)> instruments, DateTime today, CancellationToken ct = default)
    {
        var wanted = instruments
            .GroupBy(i => i.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Exchange, StringComparer.OrdinalIgnoreCase);

        var best = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        var header = await reader.ReadLineAsync(ct);
        if (header is null) return new Dictionary<string, string>();

        var cols = SplitCsvLine(header)
            .Select((name, idx) => (name: name.Trim().ToLowerInvariant(), idx))
            .ToDictionary(c => c.name, c => c.idx);

        if (!cols.TryGetValue("underlying_symbol", out var iUnderlying)
            || !cols.TryGetValue("exchange", out var iExchange)
            || !cols.TryGetValue("instrument_type", out var iType)
            || !cols.TryGetValue("expiry_date", out var iExpiry))
        {
            return new Dictionary<string, string>();   // unknown layout — refuse to guess
        }

        var maxIdx = Math.Max(Math.Max(iUnderlying, iExchange), Math.Max(iType, iExpiry));

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            var f = SplitCsvLine(line);
            if (f.Count <= maxIdx) continue;

            var underlying = f[iUnderlying].Trim();
            if (!wanted.TryGetValue(underlying, out var exchange)) continue;
            if (!f[iExchange].Trim().Equals(exchange, StringComparison.OrdinalIgnoreCase)) continue;

            var type = f[iType].Trim();
            if (!type.Equals("CE", StringComparison.OrdinalIgnoreCase)
                && !type.Equals("PE", StringComparison.OrdinalIgnoreCase)) continue;

            if (!DateTime.TryParseExact(f[iExpiry].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date)) continue;
            if (date.Date < today.Date) continue;

            if (!best.TryGetValue(underlying, out var current) || date < current)
                best[underlying] = date.Date;
        }

        return best.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True when the stored value is missing, unparseable or already past, i.e. needs rolling. A date
    /// equal to today is NOT stale: expiry day itself must keep IsExpiryDay true.
    /// </summary>
    public static bool NeedsRoll(string? storedExpiry, DateTime today)
    {
        if (string.IsNullOrWhiteSpace(storedExpiry)) return true;

        return !DateTime.TryParseExact(storedExpiry, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                   DateTimeStyles.None, out var date)
               || date.Date < today.Date;
    }

    /// <summary>True when <paramref name="candidate"/> is within the plausible window from today.</summary>
    public static bool IsPlausible(string candidate, DateTime today)
        => DateTime.TryParseExact(candidate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
           && d.Date >= today.Date
           && (d.Date - today.Date).TotalDays <= MaxPlausibleDaysAhead;

    /// <summary>Minimal CSV splitter: commas separate fields, double quotes protect commas inside a field.</summary>
    internal static List<string> SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var sb = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(sb.ToString());
                sb.Clear();
            }
            else sb.Append(c);
        }

        fields.Add(sb.ToString());
        return fields;
    }
}
