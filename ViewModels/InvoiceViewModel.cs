using System.Collections.ObjectModel;
using System.Windows.Input;
using HotelApp.Models;
using HotelApp.Services;

namespace HotelApp.ViewModels;

public class InvoiceViewModel
{
    private readonly InvoiceService _invoiceService;

    public ObservableCollection<(string Description, decimal Amount)> InvoiceItems { get; } = new();
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }

    public List<string> PaymentMethods { get; } = new() { "Cash", "Card", "Transfer", "E-Wallet" };
    public string SelectedMethod { get; set; } = "Cash";

    public ICommand CreateInvoiceCommand { get; }
    public ICommand PayCommand { get; }

    private int _invoiceId;

    public InvoiceViewModel(InvoiceService invoiceService)
    {
        _invoiceService = invoiceService;
        CreateInvoiceCommand = new Command<int>(async stayId => await CreateAsync(stayId));
        PayCommand = new Command(async () => await PayAsync());
    }

    private async Task CreateAsync(int stayId)
    {
        // Demo: tạo hóa đơn với thuế 10% và discount 0
        _invoiceId = await _invoiceService.CreateInvoiceAsync(stayId, 10, 0);

        var inv = await _invoiceService.GetInvoiceAsync(_invoiceId);
        if (inv is null) return;

        // Cập nhật UI
        InvoiceItems.Clear();
        InvoiceItems.Add(("Tiền phòng", inv.SubtotalRoom));
        InvoiceItems.Add(("Dịch vụ", inv.SubtotalService));

        Discount = inv.DiscountAmount;
        Subtotal = inv.SubtotalRoom + inv.SubtotalService;
        Tax = (Subtotal - Discount) * (inv.TaxRate / 100m);
        Total = inv.TotalAmount;
    }

    private async Task PayAsync()
    {
        if (_invoiceId <= 0) { await Application.Current.MainPage.DisplayAlert("Lỗi", "Chưa có hóa đơn.", "OK"); return; }
        await _invoiceService.PayAsync(_invoiceId, Total, SelectedMethod);
        await Application.Current.MainPage.DisplayAlert("Thành công", "Đã thanh toán.", "OK");
    }
}
