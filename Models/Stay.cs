namespace HotelApp.Models;

public class Stay
{
    public int StayId { get; set; }
    public int? BookingId { get; set; }
    public int CustomerId { get; set; }
    public int RoomId { get; set; }
    public DateTime ActualCheckIn { get; set; }
    public DateTime? ActualCheckOut { get; set; }
    public string StayStatus { get; set; } = "CheckedIn";
    public string? Notes { get; set; }

    // Navigation
    public Booking? Booking { get; set; }
    public Customer? Customer { get; set; }
    public Room? Room { get; set; }
    public ICollection<ServiceUsage> ServiceUsages { get; set; } = new List<ServiceUsage>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
