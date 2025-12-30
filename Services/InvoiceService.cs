using HotelApp.Data;
using HotelApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace HotelApp.Services;

public class InvoiceService
{
    private readonly HotelDbContext _db;
    public InvoiceService(HotelDbContext db) => _db = db;

    // Tạo hóa đơn từ Stay: tính tổng tiền phòng theo số ngày, cộng dịch vụ
    // extras: additional service subtotal passed by caller (e.g. ad-hoc charges)
    public async Task<int> CreateInvoiceAsync(int stayId, decimal taxRate = 10, decimal discount = 0, decimal extras = 0)
    {
        var stay = await _db.Stays.Include(s => s.Room).ThenInclude(r => r.RoomType)
                                  .Include(s => s.ServiceUsages).ThenInclude(su => su.Service)
                                  .FirstOrDefaultAsync(s => s.StayId == stayId);
        if (stay is null) throw new InvalidOperationException("Stay không tồn tại.");
        if (stay.ActualCheckOut is null) throw new InvalidOperationException("Chưa trả phòng.");

        // Use date-only difference to compute nights (matches preview)
        var days = Math.Max(1, (int)(stay.ActualCheckOut.Value.Date - stay.ActualCheckIn.Date).TotalDays);
        var roomRate = stay.Room.RoomType!.BasePrice;
        var subtotalRoom = roomRate * days;

        // Tổng dịch vụ = persisted service usages + extras provided by caller
        var persistedService = stay.ServiceUsages.Sum(su => su.Quantity * su.UnitPrice);
        var subtotalService = persistedService + Math.Max(0, extras);

        // Thuế và tổng
        var taxAmount = (subtotalRoom + subtotalService - discount) * (taxRate / 100m);
        var total = subtotalRoom + subtotalService - discount + taxAmount;

        var invoice = new Invoice
        {
            StayId = stayId,
            InvoiceStatus = "Issued",
            SubtotalRoom = subtotalRoom,
            SubtotalService = subtotalService,
            TaxRate = taxRate,
            DiscountAmount = discount,
            TotalAmount = total,
            CreatedAt = DateTime.Now,
            IssuedAt = DateTime.Now
        };

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync();
        return invoice.InvoiceId;
    }

    // Thanh toán hóa đơn
    public async Task PayAsync(int invoiceId, decimal amount, string method, string? refCode = null)
    {
        var invoice = await _db.Invoices.FindAsync(invoiceId);
        if (invoice is null) throw new InvalidOperationException("Invoice không tồn tại.");
        if (invoice.InvoiceStatus == "Paid") return;

        var payment = new Payment
        {
            InvoiceId = invoiceId,
            Amount = amount,
            Method = method,
            ReferenceCode = refCode,
            PaidAt = DateTime.Now
        };

        _db.Payments.Add(payment);

        // Đánh dấu đã thanh toán nếu đủ tiền
        var totalPaid = await _db.Payments.Where(p => p.InvoiceId == invoiceId).SumAsync(p => p.Amount) + amount;
        if (totalPaid >= invoice.TotalAmount)
        {
            invoice.InvoiceStatus = "Paid";
            invoice.PaidAt = DateTime.Now;
        }

        await _db.SaveChangesAsync();
    }

    public async Task<Invoice?> GetInvoiceAsync(int invoiceId)
        => await _db.Invoices.Include(i => i.Payments).FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);

    // new: get invoice by stay (latest)
    public async Task<Invoice?> GetInvoiceByStayAsync(int stayId)
        => await _db.Invoices
            .Include(i => i.Payments)
            .Where(i => i.StayId == stayId)
            .OrderByDescending(i => i.IssuedAt)
            .FirstOrDefaultAsync();

    // Export invoice to CSV (Excel-compatible). Returns full file path.
    public async Task<string> ExportInvoiceCsvAsync(int invoiceId)
    {
        var inv = await _db.Invoices
            .Include(i => i.Payments)
            .Include(i => i.Stay).ThenInclude(s => s.Room).ThenInclude(r => r.RoomType)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);

        if (inv is null) throw new InvalidOperationException("Invoice not found.");

        var sb = new StringBuilder();
        sb.AppendLine("InvoiceId,StayId,InvoiceStatus,SubtotalRoom,SubtotalService,TaxRate,Discount,TotalAmount,IssuedAt");
        sb.AppendLine($"{inv.InvoiceId},{inv.StayId},{inv.InvoiceStatus},{inv.SubtotalRoom},{inv.SubtotalService},{inv.TaxRate},{inv.DiscountAmount},{inv.TotalAmount},{inv.IssuedAt:O}");

        sb.AppendLine();
        sb.AppendLine("Payments:");
        sb.AppendLine("PaymentId,Amount,Method,ReferenceCode,PaidAt");
        if (inv.Payments != null)
        {
            foreach (var p in inv.Payments)
            {
                sb.AppendLine($"{p.PaymentId},{p.Amount},{p.Method},{p.ReferenceCode},{p.PaidAt:O}");
            }
        }

        // Save to temp path
        var fileName = $"invoice_{inv.InvoiceId}_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        var path = Path.Combine(FileSystem.CacheDirectory, fileName);
        await File.WriteAllTextAsync(path, sb.ToString(), Encoding.UTF8);
        return path;
    }
}
