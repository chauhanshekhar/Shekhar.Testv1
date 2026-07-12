using Trading.Application.Interfaces;
using Trading.Domain.Entities;
using Trading.Infrastructure;

namespace Trading.Infrastructure.Brokers.Groww;

/// <summary>
/// Hero-Zero strategy — real Groww orders but always exactly 1 lot.
/// Purpose: validate signals in the live market with minimal capital exposure
/// before scaling to full lot size (Live mode).
///
/// "Hero" = winning signals proven on real market.
/// "Zero" = losing signals cost only 1 lot instead of full capital.
/// </summary>
public sealed class HeroZeroOrderStrategy(IGrowwHttpService growwHttp, IInstrumentCache instruments)
    : LiveOrderStrategy(growwHttp, instruments)
{
    // Override Template Method: always trade 1 lot regardless of Signal.Quantity.
    protected override int GetLots(Signal signal) => 1;
}
