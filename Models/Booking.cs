namespace HotelApp.Models;

public class Booking
{
    public int BookingId { get; set; }
    public int CustomerId { get; set; }
    public int RoomId { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public string BookingStatus { get; set; } = "Pending";
    public decimal DepositAmount { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    // Property khi Hủy booking
    public string? CancelledBy { get; set; }
    public DateTime? CancelledAt { get; set; }

    // Navigation
    public Customer? Customer { get; set; }
    public Room? Room { get; set; }
    public ICollection<Stay> Stays { get; set; } = new List<Stay>();
}
