using Trading.Domain;
namespace Trading.Domain.Entities;

public class EconomicEvent
{
    public long Id { get; set; }
    public string Name { get; set; } = "";       // "RBI Policy", "FOMC", "CPI India"
    public string Impact { get; set; } = "HIGH";   // HIGH / MEDIUM / LOW
    public DateTime EventTimeUtc { get; set; }
    public int BlackoutBefore { get; set; } = 30;       // minutes before event — suppress signals
    public int BlackoutAfter { get; set; } = 30;       // minutes after event — suppress signals
    public bool EnableNewsEntry { get; set; } = false;    // treat as a fireable news event once it fires (blocks trading)
    public decimal NewsEntryThresholdPct { get; set; } = 0.5m;    // min index move % to confirm momentum
    public bool IsProcessed { get; set; } = false;    // unused now that news events only block, never auto-enter
    public DateTime CreatedOn { get; set; } = IstClock.Now;
}
