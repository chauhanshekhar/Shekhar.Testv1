namespace Trading.Domain.Entities;

public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string Role { get; set; } = "Trader";   // Admin | Trader
    public DateTime CreatedOn { get; set; }
    public DateTime? LastLoginOn { get; set; }
}
