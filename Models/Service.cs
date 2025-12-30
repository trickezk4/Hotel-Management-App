namespace HotelApp.Models;

public class Service
{
    public int ServiceId { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public string Unit { get; set; } = "";
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<ServiceUsage> ServiceUsages { get; set; } = new List<ServiceUsage>();
}
