using Microsoft.Extensions.DependencyInjection;
using Trading.Application.Interfaces;
using Trading.Domain.Entities;
using Trading.Infrastructure;

namespace Trading.Worker.SignalGates;

public class RiskGate : IPostSignalGate
{
    public async Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, Signal signal, CancellationToken ct)
    {
        var s              = ctx.Settings;
        var totalContracts = ctx.Instruments.GetLotSize(ctx.Symbol) * Math.Max(1, signal.Quantity);

        // Use entry trigger value (netDebit / totalPremium / optionLtp) so risk is on same scale as OptionSL.
        var entryValue = PositionTrigger.EntryValue(signal);

        bool canTrade = entryValue > 0 && signal.OptionSL > 0
            ? ctx.Risk.CanTrade(s.Capital, s.RiskPercent, entryValue, signal.OptionSL, totalContracts)
            : ctx.Risk.CanTrade(s.Capital, s.RiskPercent, signal.EntryPrice, signal.StopLoss, totalContracts);

        if (!canTrade)
            return $"Risk check failed — exposure exceeds {s.RiskPercent}% budget ({ctx.Symbol} {signal.Quantity} lots)";

        // ── Broker margin check — skip in paper mode (IGrowwPortfolioService may be a stub) ──
        try
        {
            var portfolio = ctx.ServiceProvider?.GetService<IGrowwPortfolioService>();
            if (portfolio is not null)
            {
                var positions = await portfolio.GetPositionsAsync();
                var usedMargin = positions
                    .Where(p => p.Quantity != 0)
                    .Sum(p => Math.Abs((p.DebitPrice ?? 0m) * (p.Quantity ?? 0)));
                var availableMargin = s.Capital - usedMargin;
                var requiredMargin  = entryValue > 0
                    ? entryValue * totalContracts
                    : signal.EntryPrice * totalContracts;

                if (requiredMargin > availableMargin)
                    return $"Insufficient margin — need ₹{requiredMargin:N0}, available ₹{availableMargin:N0} ({ctx.Symbol})";
            }
        }
        catch
        {
            // Non-fatal — broker API failure should not block signal generation
        }

        return null;
    }
}
