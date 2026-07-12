using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Brokers.Groww;

namespace Trading.Worker.SignalGates;

/// <summary>
/// Bid/ask spread check on the option leg(s) the engine actually selected.
/// Queries GetQuoteAsync only for the chosen leg(s) — never the full chain —
/// so it runs once per signal attempt, not hundreds of times per cycle.
/// </summary>
public class SpreadGate : IPostSignalGate
{
    public async Task<string?> BlockAsync(
        SignalContext ctx,
        MarketSnapshot market,
        Signal signal,
        CancellationToken ct)
    {
        var exchange = ctx.Instruments.GetExchange(ctx.Symbol);

        var reason = await CheckLegAsync(ctx, exchange, signal.OptionSymbol, signal.OptionLtp);
        if (reason != null) return reason;

        if (signal.StrategyType != TradingConst.StrategyType.Single && !string.IsNullOrEmpty(signal.Leg2Symbol))
        {
            reason = await CheckLegAsync(ctx, exchange, signal.Leg2Symbol, signal.Leg2Ltp);
            if (reason != null) return reason;
        }

        return null;
    }

    private static async Task<string?> CheckLegAsync(
       SignalContext ctx,
       string exchange,
       string tradingSymbol,
       decimal premium)
    {
        var quote = await ctx.LiveData.GetQuoteAsync(
            exchange,
            GrowwSegment.Fno,
            tradingSymbol);

        if (quote == null)
            return $"Spread check: no quote for {tradingSymbol}";


        var bid = quote.Depth?
            .Buy
            .OrderByDescending(x => x.Price)
            .FirstOrDefault()
            ?.Price ?? 0m;


        var ask = quote.Depth?
            .Sell
            .OrderBy(x => x.Price)
            .FirstOrDefault()
            ?.Price ?? 0m;


        if (bid <= 0 || ask <= 0)
        {
            return
              $"Spread check: no depth quote for {tradingSymbol}";
        }


        var spreadPct =
            (ask - bid) / ask * 100m;


        var maxSpreadPct = premium switch
        {
            < 50m => 5m,

            <= 200m => 3m,

            _ => 2m
        };


        return spreadPct <= maxSpreadPct
            ? null
            : $"Spread too wide on {tradingSymbol}: {spreadPct:F2}% > {maxSpreadPct}% (Bid {bid} / Ask {ask})";
    }
}
