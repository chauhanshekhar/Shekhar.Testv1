namespace Trading.Domain.Entities;

public class ExchangeHoliday
{
    public int      Id          { get; set; }
    public DateTime HolidayDate { get; set; }           // date only (time = 00:00)
    public string   Description { get; set; } = "";     // "Diwali Muhurat", "Republic Day", etc.
    public bool     IsActive    { get; set; } = true;   // soft-delete / disable without removing
}
