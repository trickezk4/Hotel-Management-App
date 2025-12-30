namespace HotelApp.Models;

public class ServiceUsage
{
    public int ServiceUsageId { get; set; }
    public int StayId { get; set; }
    public int ServiceId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public DateTime UsedAt { get; set; } = DateTime.Now;
    public string? Notes { get; set; }

    // Navigation
    public Stay? Stay { get; set; }
    public Service? Service { get; set; }
}
