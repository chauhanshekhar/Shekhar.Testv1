using Trading.Application.Interfaces;
using Trading.Domain;
using Trading.Infrastructure;
using Trading.Infrastructure.Brokers.Groww;
using Trading.Domain.Entities;
using Trading.Infrastructure.Repositories;
using Trading.Worker.SignalGates;

namespace Trading.Worker.Exits;

/// <summary>
/// Per-cycle reads for the exit path — open signals, latest ticks, the current option-chain
/// snapshot, and the latest ATR per symbol. Extracted from ExitWorker unchanged and kept static:
/// these are pure queries with no state, batched once per cycle rather than once per signal.
/// </summary>
public static class ExitDataLoader
{
    // ── Open signals ──────────────────────────────────────────────────────────
    public static async Task<List<Signal>> LoadOpenSignalsAsync(
        IRepository<Signal> signalRepo,
        IRepository<TradeJournal> journalRepo,
        DateTime todayStartUtc)
    {
        var todaySignals = await signalRepo.FindAsync(s => s.CreatedOn >= todayStartUtc);

        var closedIds = (await journalRepo.FindAsync(
                j => j.TradeDate >= todayStartUtc && j.SignalId.HasValue && !j.IsPartial))
            .Select(j => j.SignalId!.Value)
            .ToHashSet();

        return todaySignals
            .Where(s =>
                (
                    s.Status == SignalStatus.OrderPlaced ||
                    s.Status == SignalStatus.Active ||
                    s.Status == SignalStatus.PartialExitPending
                )
                && !closedIds.Contains(s.Id))
            .ToList();
    }

    // ── Latest ticks ──────────────────────────────────────────────────────────
    public static async Task<Dictionary<string, decimal>> LoadLatestTicksAsync(
        IRepository<MarketTick> tickRepo)
    {
        var cutoff = IstClock.Now.AddSeconds(-30);
        return (await tickRepo.FindAsync(t => t.TickTime >= cutoff))
            .GroupBy(t => t.Symbol)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.TickTime).First().LastPrice);
    }

    // ── Latest option chain snapshot ──────────────────────────────────────────
    // One snapshot per SYMBOL, not one globally. Each SnapshotId covers exactly one symbol, so
    // taking the single newest id returned only whichever symbol the feed happened to write last
    // (in practice SENSEX, which is written third) — every NIFTY and BANKNIFTY position then
    // resolved optionLtp = 0 and lost every option-price-based exit: T1, T2, option SL, trailing.
    public static async Task<Dictionary<string, List<OptionChainData>>> LoadOptionChainSnapshotAsync(
        IRepository<OptionChainData> optionRepo)
    {
        var recent = await optionRepo.FindAsync(o => o.CreatedOn >= IstClock.Now.AddMinutes(-2));
        return SelectLatestPerSymbol(recent);
    }

    /// <summary>
    /// Keeps, for each symbol, only the rows belonging to that symbol's most recent snapshot.
    /// Pure so the per-symbol selection can be tested without a repository.
    /// </summary>
    internal static Dictionary<string, List<OptionChainData>> SelectLatestPerSymbol(
        IEnumerable<OptionChainData> recent)
    {
        return recent
            .GroupBy(o => o.Symbol)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var latestId = g.OrderByDescending(x => x.CreatedOn).First().SnapshotId;
                    return g.Where(x => x.SnapshotId == latestId).ToList();
                });
    }

    // ── Latest ATR per symbol ─────────────────────────────────────────────────
    // One batched query per cycle, not per signal. Reads the most recent IndicatorSnapshot row
    // (SignalWorker's audit table) instead of recalculating ATR here — the exit path stays
    // execution-only. Falls back to Signal.AtrRawAtEntry (in TrailingStopService) when no
    // snapshot is fresh enough (table not populated yet, or a gap).
    public static async Task<Dictionary<string, decimal>> LoadLatestAtrBySymbolAsync(
        IRepository<IndicatorSnapshot> snapshotRepo,
        List<string> symbols)
    {
        if (symbols.Count == 0)
            return new Dictionary<string, decimal>();

        var cutoff = IstClock.Now.AddMinutes(-5);
        var recent = await snapshotRepo.FindAsync(s => symbols.Contains(s.Symbol) && s.SnapshotTime >= cutoff);

        return recent
            .GroupBy(s => s.Symbol)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.SnapshotTime).First().AtrRaw);
    }

    /// <summary>
    /// LIVE option premiums for the open positions, one targeted quote per distinct option symbol.
    ///
    /// WHY: option prices otherwise reach this worker only through OptionChainData snapshots, which
    /// OptionChainWorker writes every 30 SECONDS and which LoadOptionChainSnapshotAsync will accept
    /// up to 2 minutes old. The tick feed carries index symbols only. So ExitWorker ran every second
    /// against a price that moved twice a minute — MfePrice/MaePrice sampled at that rate (one
    /// observation for a 55-second trade on 2026-08-31), and, far worse, the OptionSL/T1/T2 checks
    /// were evaluated on stale prices, so a target touched between refreshes could be missed
    /// entirely.
    ///
    /// RATE LIMIT: Groww allows 10/sec and 300/min for live data. OnePositionGate permits one open
    /// position at a time, so this is normally 1 call/second = 60/min. MaxQuotesPerCycle caps it
    /// defensively if that ever changes.
    ///
    /// Failures are non-fatal by design: any symbol that does not return a usable quote is simply
    /// absent from the dictionary, and ResolveLtps falls back to the chain snapshot exactly as
    /// before. A broker hiccup must never stop exits being evaluated.
    /// </summary>
    private const int MaxQuotesPerCycle = 10;

    public static async Task<Dictionary<string, decimal>> LoadLiveOptionQuotesAsync(
        IGrowwLiveDataService liveData,
        IInstrumentCache instruments,
        List<Signal> openSignals,
        ILogger logger,
        CancellationToken ct)
    {
        var quotes = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        var symbols = openSignals
            .Where(s => !string.IsNullOrWhiteSpace(s.OptionSymbol))
            .Select(s => (s.OptionSymbol, Exchange: instruments.GetExchange(s.Symbol)))
            .Distinct()
            .Take(MaxQuotesPerCycle)
            .ToList();

        foreach (var (optionSymbol, exchange) in symbols)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var quote = await liveData.GetQuoteAsync(exchange, GrowwSegment.Fno, optionSymbol);
                if (quote?.Ltp is > 0)
                    quotes[optionSymbol] = quote.Ltp.Value;
            }
            catch (Exception ex)
            {
                // Logged, never thrown: the chain snapshot remains as the fallback.
                logger.LogWarning(ex,
                    "Live quote failed for {OptionSymbol} — falling back to the chain snapshot", optionSymbol);
            }
        }

        return quotes;
    }

    // ── Per-signal leg prices from the cycle's chain snapshot ─────────────────
    /// <param name="liveQuotes">
    /// Live premiums keyed by option trading symbol. Takes precedence over the chain snapshot for
    /// PRICE only — delta still comes from the chain, since quotes carry no greeks.
    /// </param>
    public static (decimal optionLtp, decimal leg2Ltp, decimal currentDelta) ResolveLtps(
        Signal signal,
        Dictionary<string, List<OptionChainData>> chainBySymbol,
        Dictionary<string, decimal>? liveQuotes = null)
    {
        decimal optionLtp = 0;
        decimal leg2Ltp = 0;
        decimal currentDelta = 0;

        if (chainBySymbol.TryGetValue(signal.Symbol, out var chain))
        {
            var leg1 = chain.FirstOrDefault(x => x.TradingSymbol == signal.OptionSymbol);
            optionLtp = leg1?.Ltp ?? 0;
            currentDelta = leg1?.Delta ?? 0;   // greeks are chain-only

            // Prefer the live premium when one was obtained this cycle.
            if (liveQuotes != null
                && !string.IsNullOrWhiteSpace(signal.OptionSymbol)
                && liveQuotes.TryGetValue(signal.OptionSymbol, out var liveLtp)
                && liveLtp > 0)
            {
                optionLtp = liveLtp;
            }

            if (signal.StrategyType != TradingConst.StrategyType.Single)
            {
                leg2Ltp =
                    chain.FirstOrDefault(x =>
                        x.OptionType == signal.Leg2OptionType &&
                        x.StrikePrice == signal.Leg2StrikePrice)
                    ?.Ltp ?? 0;
            }
        }
        return (optionLtp, leg2Ltp, currentDelta);
    }

    // ── Daily loss limit check ────────────────────────────────────────────────
    public static async Task<bool> IsDailyLossBreachedAsync(
        IRepository<TradeJournal> journalRepo,
        IRepository<PaperTrade> paperRepo,
        IRepository<MarketTick> tickRepo,
        decimal maxDailyLoss,
        DateTime todayStartUtc,
        CancellationToken ct)
    {
        var tomorrowStart = todayStartUtc.AddDays(1);
        var todayJournal  = await journalRepo.FindAsync(
            j => j.TradeDate >= todayStartUtc && j.TradeDate < tomorrowStart);
        var realizedPnl = todayJournal.Sum(x => x.ProfitLoss);

        if (realizedPnl <= -maxDailyLoss)
        {
            return true;
        }

        var unrealizedPnl = await ComputeUnrealizedPnlAsync(paperRepo, tickRepo);
        if (realizedPnl + unrealizedPnl <= -maxDailyLoss)
        {
            return true;
        }

        return false;
    }

    // ── Unrealized P&L from open paper trades ─────────────────────────────────
    public static async Task<decimal> ComputeUnrealizedPnlAsync(
        IRepository<PaperTrade> paperRepo,
        IRepository<MarketTick> tickRepo)
    {
        var cutoff       = IstClock.Now.AddSeconds(-30);
        var ltpBySymbol  = (await tickRepo.FindAsync(t => t.TickTime >= cutoff))
            .GroupBy(t => t.Symbol)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.TickTime).First().LastPrice);

        var openTrades   = await paperRepo.FindAsync(p => p.Status == "OPEN");
        var unrealized   = 0m;

        foreach (var p in openTrades)
        {
            var sym = !string.IsNullOrEmpty(p.OptionSymbol) ? p.OptionSymbol : p.Symbol;
            if (!ltpBySymbol.TryGetValue(sym, out var currentLtp)) continue;

            var side   = p.Side?.Equals("CALL", StringComparison.OrdinalIgnoreCase) == true ? 1m : -1m;
            unrealized += (currentLtp - p.EntryPrice) * p.Quantity * side;

            if (!string.IsNullOrEmpty(p.Leg2OptionSymbol) && p.Leg2EntryPrice > 0
                && ltpBySymbol.TryGetValue(p.Leg2OptionSymbol, out var leg2Ltp))
            {
                // Leg2 is SHORT option for spreads.
                // Profit when Leg2 premium falls.
                unrealized +=
                    (p.Leg2EntryPrice - leg2Ltp) * p.Quantity;
            }
        }

        return unrealized;
    }
}
