namespace Trading.Domain;

/// <summary>
/// Explicit intent of an Order row — used instead of inferring intent from OrderType/Side
/// (e.g. "SELL" + "SL_M" happens to mean stop-loss today, but that's fragile once entry SL
/// orders, spread-leg SL orders, or manual SL orders exist). Reconciliation logic (e.g.
/// BrokerOrderSyncWorker) should branch on OrderPurpose, not OrderType/Side.
/// </summary>
public static class OrderPurpose
{
    public const string Entry       = "ENTRY";
    public const string StopLoss    = "STOPLOSS";
    public const string Target      = "TARGET";
    public const string PartialExit = "PARTIAL_EXIT";
    public const string ForceExit   = "FORCE_EXIT";
    public const string Exit        = "EXIT"; // generic full-exit fallback (HeroZero/ProtectedSL/IndexSL)
}
