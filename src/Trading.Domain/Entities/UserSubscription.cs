using Trading.Domain;
namespace Trading.Domain.Entities;

public class UserSubscription
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int PlanId { get; set; }
    public string PlanName { get; set; } = "";
    public DateTime StartDate { get; set; } = IstClock.Now;
    public DateTime ExpiryDate { get; set; }
    public bool IsActive { get; set; } = true;
    public bool AutoRenew { get; set; } = false;
    public DateTime CreatedOn { get; set; } = IstClock.Now;
}
