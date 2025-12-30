namespace HotelApp.Models;

public class AuditLog
{
    public int AuditLogId { get; set; }
    public int UserId { get; set; }
    public string Action { get; set; } = "";
    public string Entity { get; set; } = "";
    public int EntityId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string? Detail { get; set; }

    // Navigation
    public User? User { get; set; }
}
