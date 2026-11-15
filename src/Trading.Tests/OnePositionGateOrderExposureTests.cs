using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Worker.SignalGates;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// OnePositionGate's second check: live Orders show exposure the signal status no longer does.
///
/// Regression cover for 2026-09-30: signal 91 (SENSEX26O0172700CE, 1 lot = 20) had every SELL
/// rejected, yet left the open statuses at 10:04:54 with its lot still held - signal 92 fired on the
/// same contract. 92 then left ORDER_PLACED at 10:05:24 while its BUY was still filling - signal 93
/// fired. The hold window keeps a position closed in the Groww app (no SELL row) from halting
/// trading all day, the 2026-08-24 failure mode.
/// </summary>
public class OnePositionGateOrderExposureTests
{
    private const int Hold = OnePositionGate.DefaultOrderHoldMinutes;
    private const string Option = "SENSEX26O0172700CE";
    private static readonly DateTime Day = new(2026, 9, 30);

    private static DateTime At(int h, int m, int s = 0) => Day.AddHours(h).AddMinutes(m).AddSeconds(s);

    private static Order Buy(long signalId, long id, DateTime orderedAt, string status, int filled,
                             string brokerOrderId = "GMKFO260930")
        => new()
        {
            Id = id, SignalId = signalId, BrokerOrderId = brokerOrderId + id, Symbol = Option,
            Side = "BUY", OrderType = "MARKET", OrderPurpose = OrderPurpose.Entry,
            Status = status, Quantity = 1, FilledQty = filled, OrderedAt = orderedAt, UpdatedAt = orderedAt,
        };

    private static Order Sell(long signalId, long id, DateTime orderedAt, string status, int filled,
                              string purpose = OrderPurpose.Exit, string brokerOrderId = "GMKFO260930")
        => new()
        {
            Id = id, SignalId = signalId, BrokerOrderId = brokerOrderId + id, Symbol = Option,
            Side = "SELL", OrderType = "MARKET", OrderPurpose = purpose,
            Status = status, Quantity = 1, FilledQty = filled, OrderedAt = orderedAt, UpdatedAt = orderedAt,
        };

    /// <summary>Signal 91's orders as they stood at 10:04:54: BUY filled, every SELL rejected.</summary>
    private static List<Order> Signal91() =>
    [
        Buy(91, 530, At(9, 54, 41), OrderStatus.Complete, 20),
        Sell(91, 532, At(9, 55, 21), OrderStatus.Cancelled, 0, OrderPurpose.StopLoss),
        Sell(91, 535, At(9, 56, 36), OrderStatus.Rejected, 0, OrderPurpose.StopLoss),
        Sell(91, 539, At(9, 59, 9), OrderStatus.Rejected, 0, OrderPurpose.Target),
        Sell(91, 544, At(10, 3, 59), OrderStatus.Rejected, 0),
    ];

    [Fact]
    public void Signal91_BoughtWithNoConfirmedSell_Blocks()
    {
        var reason = OnePositionGate.FindUnconfirmedExposure(Signal91(), At(10, 4, 54), Hold);

        Assert.NotNull(reason);
        Assert.Contains("SignalId=91", reason);
    }

    [Fact]
    public void Signal93_EntryStillFilling_Blocks()
    {
        // 92's BUY was placed at 10:05:09 and only filled at 10:05:26.
        var orders = new List<Order> { Buy(92, 545, At(10, 5, 9), OrderStatus.Pending, 0) };

        var reason = OnePositionGate.FindUnconfirmedExposure(orders, At(10, 5, 24), Hold);

        Assert.NotNull(reason);
        Assert.Contains("in flight", reason);
    }

    [Fact]
    public void ClosedThroughTheBroker_DoesNotBlock()
    {
        // Signal 90: BUY 20, MARKET exit filled 20.
        var orders = new List<Order>
        {
            Buy(90, 526, At(9, 32, 53), OrderStatus.Complete, 20),
            Sell(90, 528, At(9, 33, 43), OrderStatus.Cancelled, 0, OrderPurpose.StopLoss),
            Sell(90, 529, At(9, 47, 3), OrderStatus.Complete, 20),
        };

        Assert.Null(OnePositionGate.FindUnconfirmedExposure(orders, At(9, 50), Hold));
    }

    [Fact]
    public void BrokerStopLossFill_CountsAsTheSell()
    {
        var orders = new List<Order>
        {
            Buy(90, 526, At(9, 32, 53), OrderStatus.Complete, 20),
            Sell(90, 527, At(9, 33, 6), OrderStatus.Complete, 20, OrderPurpose.StopLoss),
        };

        Assert.Null(OnePositionGate.FindUnconfirmedExposure(orders, At(9, 40), Hold));
    }

    [Fact]
    public void RejectedEntry_NothingFilled_DoesNotBlock()
    {
        var orders = new List<Order> { Buy(93, 547, At(10, 5, 39), OrderStatus.Rejected, 0) };

        Assert.Null(OnePositionGate.FindUnconfirmedExposure(orders, At(10, 6, 30), Hold));
    }

    [Fact]
    public void HoldWindowExpires_NoAllDayHalt()
    {
        // Closed by hand in the Groww app: no SELL row ever arrives. 16 minutes after the last order
        // was placed the gate must release (the 2026-08-24 lesson).
        Assert.Null(OnePositionGate.FindUnconfirmedExposure(Signal91(), At(10, 3, 59).AddMinutes(Hold + 1), Hold));
    }

    [Fact]
    public void StuckInFlightEntry_ReleasesAfterTheWindow()
    {
        var orders = new List<Order> { Buy(23, 205, At(10, 50, 27), OrderStatus.Pending, 0) };

        Assert.NotNull(OnePositionGate.FindUnconfirmedExposure(orders, At(10, 51, 20), Hold));
        Assert.Null(OnePositionGate.FindUnconfirmedExposure(orders, At(10, 50, 27).AddMinutes(Hold + 1), Hold));
    }

    [Fact]
    public void PaperOrders_AreIgnored()
    {
        // Paper exits write no SELL Orders row, so a paper BUY would otherwise look open forever.
        var orders = new List<Order> { Buy(98, 554, At(12, 24, 22), OrderStatus.Complete, 80, brokerOrderId: "PAPER-") };

        Assert.Null(OnePositionGate.FindUnconfirmedExposure(orders, At(12, 30), Hold));
    }

    [Fact]
    public void HoldZero_TurnsTheCheckOff()
        => Assert.Null(OnePositionGate.FindUnconfirmedExposure(Signal91(), At(10, 4, 54), 0));

    [Fact]
    public void OnlyTheExposedSignalIsReported()
    {
        var orders = new List<Order>
        {
            Buy(90, 526, At(9, 32, 53), OrderStatus.Complete, 20),
            Sell(90, 529, At(9, 47, 3), OrderStatus.Complete, 20),
        };
        orders.AddRange(Signal91());

        var reason = OnePositionGate.FindUnconfirmedExposure(orders, At(10, 4, 54), Hold);

        Assert.NotNull(reason);
        Assert.Contains("SignalId=91", reason);
        Assert.DoesNotContain("SignalId=90", reason);
    }
}
