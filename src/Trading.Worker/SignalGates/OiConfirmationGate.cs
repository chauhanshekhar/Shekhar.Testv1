using Trading.Domain;
using Trading.Domain.Entities;

namespace Trading.Worker.SignalGates;

public class OiConfirmationGate : IPostSignalGate
{
    /// <summary>
    /// Per-symbol StrategySettings switch. No row = ON (today's behaviour). Turning it off lets a
    /// built setup through without waiting for a 1m candle to confirm in the trade's direction.
    /// On 2026-10-05 the gate held setups 10-107 min, and the setups it rejected replayed as
    /// profitably as those it passed (docs/research/2026-10-05-late-entry) - hence a switch to
    /// test it off on paper, per symbol, rather than removing it.
    /// </summary>
    internal const string EnableKey = "EnableOiConfirmationGate";

    public Task<string?> BlockAsync(
        SignalContext ctx,
        MarketSnapshot market,
        Signal signal,
        CancellationToken ct)
    {
        if (!ctx.StrategySettings.GetBool(EnableKey, ctx.Symbol, true))
            return Task.FromResult<string?>(null);

        if (!market.OptionChain.Any())
            return Task.FromResult<string?>(null);

  
        var instrument=ctx.Instruments.GetActive();
        if (instrument == null)
            return Task.FromResult<string?>(
                $"Instrument not found for {ctx.Symbol}");
        // Find ATM Strike
        var interval = instrument.Where(s=>s.Symbol==ctx.Symbol).FirstOrDefault()!.StrikeInterval;

        var spot = market.OptionChain
            .Where(x => x.UnderlyingLtp > 0)
            .Select(x => x.UnderlyingLtp)
            .FirstOrDefault();

        if (spot <= 0)
            spot = market.Ltp;

        var atmStrike =
            Math.Round(
                spot / interval,
                MidpointRounding.AwayFromZero)
            * interval;

        // Use only ATM ±2 strikes
        var nearbyOptions = market.OptionChain
            .Where(o =>
                Math.Abs(o.StrikePrice - atmStrike) <= interval * 2)
            .ToList();

        long ceOiChange;
        long peOiChange;

        if (market.PrevOiByKey.Count > 0)
        {
            ceOiChange = nearbyOptions
                .Where(o => o.OptionType == TradingConst.OptionType.CE)
                .Sum(o =>
                {
                    market.PrevOiByKey.TryGetValue(
                        (o.StrikePrice, TradingConst.OptionType.CE),
                        out var prev);

                    return o.OpenInterest - prev;
                });

            peOiChange = nearbyOptions
                .Where(o => o.OptionType == TradingConst.OptionType.PE)
                .Sum(o =>
                {
                    market.PrevOiByKey.TryGetValue(
                        (o.StrikePrice, TradingConst.OptionType.PE),
                        out var prev);

                    return o.OpenInterest - prev;
                });
        }
        else
        {
            ceOiChange = nearbyOptions
                .Where(o => o.OptionType == TradingConst.OptionType.CE)
                .Sum(o => o.ChangeInOi);

            peOiChange = nearbyOptions
                .Where(o => o.OptionType == TradingConst.OptionType.PE)
                .Sum(o => o.ChangeInOi);
        }

        var priceChange =
            market.Candles1m.Count >= 2
                ? market.Candles1m.Last().Close -
                  market.Candles1m[^2].Close
                : 0m;

        bool confirmed;

        if (signal.OptionType == TradingConst.OptionType.CE)
        {
            // Bullish confirmation
            confirmed =
                ctx.Oi.IsLongBuildUp(ceOiChange, priceChange) ||
                ctx.Oi.IsShortCovering(ceOiChange, priceChange);
        }
        else
        {
            // Bearish confirmation
            confirmed =
                ctx.Oi.IsLongBuildUp(peOiChange, -priceChange) ||
                ctx.Oi.IsShortCovering(peOiChange, -priceChange);
        }

        return Task.FromResult(
            confirmed
                ? null
                : $"OI does not confirm {signal.SignalType} on {ctx.Symbol} (ATM±2) — signal suppressed");
    }
}