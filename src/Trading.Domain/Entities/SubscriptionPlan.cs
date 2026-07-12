using Trading.Domain;
namespace Trading.Domain.Entities;

public class SubscriptionPlan
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public int DurationDays { get; set; } = 30;
    public string Features { get; set; } = "[]"; // JSON array stored as string
    public bool IsActive { get; set; } = true;
    public DateTime CreatedOn { get; set; } = IstClock.Now;
}
