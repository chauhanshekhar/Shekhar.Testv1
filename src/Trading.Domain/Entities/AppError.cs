namespace Trading.Domain.Entities;

public class AppError
{
    public long Id { get; set; }
    public string Source { get; set; } = "";
    public string Message { get; set; } = "";
    public string? StackTrace { get; set; }
    public string? InnerMessage { get; set; }
    public string? Symbol { get; set; }
    public string? Context { get; set; }
    public string? RequestUrl { get; set; }
    public string Severity { get; set; } = "ERROR";
    public DateTime OccurredOn { get; set; }
}
