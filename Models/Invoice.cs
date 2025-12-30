namespace HotelApp.Models;

public class Invoice
{
    public int InvoiceId { get; set; }
    public int StayId { get; set; }
    public string InvoiceStatus { get; set; } = "Issued";
    public decimal SubtotalRoom { get; set; } = 0;
    public decimal SubtotalService { get; set; } = 0;
    public decimal TaxRate { get; set; } = 10;
    public decimal DiscountAmount { get; set; } = 0;
    public decimal TotalAmount { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? IssuedAt { get; set; }
    public DateTime? PaidAt { get; set; }

    // Navigation
    public Stay? Stay { get; set; }
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
