namespace HotelApp.Models;

public class Payment
{
    public int PaymentId { get; set; }
    public int InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = "";
    public string? ReferenceCode { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.Now;
    public string? Notes { get; set; }

    // Navigation
    public Invoice? Invoice { get; set; }
}
