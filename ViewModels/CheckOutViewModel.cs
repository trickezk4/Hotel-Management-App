using HotelApp.Models;
using HotelApp.Services;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace HotelApp.ViewModels;

[QueryProperty(nameof(BookingId), "bookingId")]
public class CheckOutViewModel : BaseViewModel
{
    private readonly BookingService _bookingService;
    private readonly InvoiceService _invoiceService;

    public CheckOutViewModel(BookingService bookingService, InvoiceService invoiceService)
    {
        _bookingService = bookingService;
        _invoiceService = invoiceService;

        ExtraCharges = new ObservableCollection<ExtraCharge>();
        PaymentItems = new ObservableCollection<PaymentItem>();

        AddChargeCommand = new Command(AddCharge);
        CreateInvoiceCommand = new Command(async () =>
        {
            if (InvoiceCreated)
            {
                await Application.Current.MainPage.DisplayAlert("Thông báo", "Đã tạo hóa đơn", "OK");
                return;
            }
            await PreviewInvoiceAsync();
        });
        ConfirmCreateInvoiceCommand = new Command(async () => await CreateInvoiceConfirmedAsync());
        PayCommand = new Command(async () => await PayAsync());
        ExportCsvCommand = new Command(async () => await ExportCsvAsync());
    }

    private int bookingId;
    public int BookingId
    {
        get => bookingId;
        set
        {
            if (SetProperty(ref bookingId, value))
                _ = LoadAsync(value);
        }
    }

    private Booking? booking;
    public Booking? Booking { get => booking; set => SetProperty(ref booking, value); }

    public string BookingDisplay => Booking == null ? "" : $"{Booking.Customer?.FullName} — Phòng {Booking.Room?.RoomNumber}";

    public ObservableCollection<ExtraCharge> ExtraCharges { get; }

    // Payment items for UI preview (room + extras + deposit)
    public ObservableCollection<PaymentItem> PaymentItems { get; }

    private string newChargeDescription = "";
    public string NewChargeDescription { get => newChargeDescription; set => SetProperty(ref newChargeDescription, value); }

    private string newChargeAmount = "";
    public string NewChargeAmount { get => newChargeAmount; set => SetProperty(ref newChargeAmount, value); }

    public ICommand AddChargeCommand { get; }
    public ICommand CreateInvoiceCommand { get; }        // Preview (now disabled after invoice created)
    public ICommand ConfirmCreateInvoiceCommand { get; } // Actually create invoice (checkout + persist)
    public ICommand PayCommand { get; }
    public ICommand ExportCsvCommand { get; }

    private int _stayId;
    private int _invoiceId;

    private bool isPreviewVisible;
    public bool IsPreviewVisible { get => isPreviewVisible; set => SetProperty(ref isPreviewVisible, value); }

    private bool invoiceCreated;
    public bool InvoiceCreated
    {
        get => invoiceCreated;
        set
        {
            if (SetProperty(ref invoiceCreated, value))
            {
                OnPropertyChanged(nameof(IsCreateEnabled));
                OnPropertyChanged(nameof(InvoiceCreated));
            }
        }
    }

    public bool IsCreateEnabled => !InvoiceCreated;

    // preview numeric values
    private decimal previewSubtotal;
    private decimal previewTax;
    private decimal previewTotal;
    private decimal previewTaxRate = 10m; // default 10%

    private void AddCharge()
    {
        if (string.IsNullOrWhiteSpace(NewChargeDescription)) return;
        if (!decimal.TryParse(NewChargeAmount, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amt)) return;

        ExtraCharges.Add(new ExtraCharge { Description = NewChargeDescription.Trim(), Amount = amt });
        NewChargeDescription = "";
        NewChargeAmount = "";
        RecalculatePreviewValues();
    }

    public string SubtotalDisplay
        => $"Tổng cộng: {previewSubtotal:N0} VND";

    public string TaxDisplay
        => $"VAT(10%): {previewTax:N0} VND";

    public string TotalDisplay
        => $"Thu: {previewTotal:N0} VND";

    private async Task LoadAsync(int id)
    {
        var b = await _booking_service_get_safe(id);
        if (b == null)
        {
            await Application.Current.MainPage.DisplayAlert("Lỗi", "Không tìm thấy đặt phòng.", "OK");
            return;
        }
        Booking = b;

        // Try to find active stay for this booking
        var stay = await _booking_service_getactive_safe(id);
        if (stay != null)
        {
            _stayId = stay.StayId;
        }

        // Reset preview
        PaymentItems.Clear();
        IsPreviewVisible = false;
        InvoiceCreated = false;
        previewSubtotal = previewTax = previewTotal = 0m;
        OnPropertyChanged(nameof(SubtotalDisplay));
        OnPropertyChanged(nameof(TaxDisplay));
        OnPropertyChanged(nameof(TotalDisplay));
    }

    // Preview invoice: compute line items (room by actual nights if possible + extras + deposit)
    private async Task PreviewInvoiceAsync()
    {
        if (Booking == null) { await Application.Current.MainPage.DisplayAlert("Lỗi", "Không có booking.", "OK"); return; }

        // Ensure we have stay info (active or already checked out)
        var stay = await _booking_service_getactive_safe(Booking.BookingId)
                    ?? await _booking_service_get_checkedout_safe(Booking.BookingId);

        DateTime actualCheckIn;
        DateTime actualCheckOut;

        if (stay != null)
        {
            actualCheckIn = stay.ActualCheckIn;
            // Use actual check-out when present; otherwise use the booking's planned CheckOutDate (not DateTime.UtcNow)
            actualCheckOut = stay.ActualCheckOut ?? Booking.CheckOutDate;
        }
        else
        {
            // Fallback to booking dates if no stay exists
            actualCheckIn = Booking.CheckInDate;
            actualCheckOut = Booking.CheckOutDate;
        }

        // Use date-only difference to compute nights (avoid off-by-one from time-of-day)
        var nights = Math.Max(1, (int)(actualCheckOut.Date - actualCheckIn.Date).TotalDays);
        var roomRate = Booking.Room?.RoomType?.BasePrice ?? 0m;
        var roomAmount = roomRate * nights;

        // Deposit from booking (may be 0)
        var deposit = Booking?.DepositAmount ?? 0m;

        PaymentItems.Clear();

        // Room line
        PaymentItems.Add(new PaymentItem { Description = $"Giá phòng ({nights} đêm @ {roomRate:N0} đ/ngày)", Amount = roomAmount });

        // Show deposit as a dedicated line; store as negative amount so sum subtracts it.
        if (deposit > 0)
            PaymentItems.Add(new PaymentItem { Description = "Đã đặt cọc", Amount = -deposit });

        // add extras from ExtraCharges collection
        foreach (var e in ExtraCharges)
            PaymentItems.Add(new PaymentItem { Description = e.Description, Amount = e.Amount });

        // compute preview totals: room - deposit + extras
        previewSubtotal = PaymentItems.Sum(pi => pi.Amount);
        previewTax = Math.Round(previewSubtotal * (previewTaxRate / 100m));
        previewTotal = previewSubtotal + previewTax;

        // notify UI
        IsPreviewVisible = true;
        OnPropertyChanged(nameof(SubtotalDisplay));
        OnPropertyChanged(nameof(TaxDisplay));
        OnPropertyChanged(nameof(TotalDisplay));
    }

    // Recalculate preview values (used when extras change while preview may or may not be visible)
    private void RecalculatePreviewValues()
    {
        // If PaymentItems already populated (preview shown), recompute from them; otherwise compute from Booking + ExtraCharges
        if (IsPreviewVisible && PaymentItems.Count > 0)
        {
            previewSubtotal = PaymentItems.Sum(pi => pi.Amount);
        }
        else
        {
            // compute room amount from booking (use booking dates if stay unknown)
            decimal roomAmount = 0m;
            if (Booking != null)
            {
                var nights = Math.Max(1, (int)(Booking.CheckOutDate.Date - Booking.CheckInDate.Date).TotalDays);
                var roomRate = Booking.Room?.RoomType?.BasePrice ?? 0m;
                roomAmount = roomRate * nights;
            }

            var extras = ExtraCharges.Sum(e => e.Amount);
            var deposit = Booking?.DepositAmount ?? 0m;

            // subtotal = room - deposit + extras
            previewSubtotal = roomAmount - deposit + extras;
        }

        previewTax = Math.Round(previewSubtotal * (previewTaxRate / 100m));
        previewTotal = previewSubtotal + previewTax;

        // if preview is visible, refresh PaymentItems to reflect new extras / deposit
        if (IsPreviewVisible)
        {
            // ensure room item exists; if not, rebuild preview via PreviewInvoiceAsync
            if (PaymentItems.Count == 0)
            {
                // best-effort: don't await here—caller expects sync. User can press Preview to rebuild.
                return;
            }

            // update PaymentItems: first item assumed room
            var first = PaymentItems.First();
            if (first.Description != null && first.Description.StartsWith("Giá phòng"))
            {
                // recompute room amount from booking (date-only)
                decimal roomAmt = 0m;
                if (Booking != null)
                {
                    var nights = Math.Max(1, (int)(Booking.CheckOutDate.Date - Booking.CheckInDate.Date).TotalDays);
                    var roomRate = Booking.Room?.RoomType?.BasePrice ?? 0m;
                    roomAmt = roomRate * nights;
                }
                first.Amount = roomAmt;
            }

            // ensure deposit line exists / updated (we keep deposit line as second item if present)
            var deposit = Booking?.DepositAmount ?? 0m;
            if (deposit > 0)
            {
                if (PaymentItems.Count >= 2 && PaymentItems[1].Description == "Đã đặt cọc")
                {
                    PaymentItems[1].Amount = -deposit;
                }
                else
                {
                    PaymentItems.Insert(1, new PaymentItem { Description = "Đã đặt cọc", Amount = -deposit });
                }
            }
            else
            {
                // remove deposit line if present
                if (PaymentItems.Count >= 2 && PaymentItems[1].Description == "Đã đặt cọc")
                    PaymentItems.RemoveAt(1);
            }

            // sync PaymentItems extras with ExtraCharges (items after room and possible deposit)
            // remove existing extras after the deposit line
            while (PaymentItems.Count > 1 + (deposit > 0 ? 1 : 0)) PaymentItems.RemoveAt((deposit > 0 ? 2 : 1));
            foreach (var e in ExtraCharges.ToList())
                PaymentItems.Add(new PaymentItem { Description = e.Description, Amount = e.Amount });
        }

        OnPropertyChanged(nameof(SubtotalDisplay));
        OnPropertyChanged(nameof(TaxDisplay));
        OnPropertyChanged(nameof(TotalDisplay));
    }

    // Confirm and create invoice: perform checkout then create invoice and persist extras
    private async Task CreateInvoiceConfirmedAsync()
    {
        if (InvoiceCreated)
        {
            await Application.Current.MainPage.DisplayAlert("Thông báo", "Đã tạo hóa đơn", "OK");
            return;
        }

        if (Booking == null) { await Application.Current.MainPage.DisplayAlert("Lỗi", "Không có booking.", "OK"); return; }

        try
        {
            // perform check-out (sets ActualCheckOut and returns stayId)
            _stayId = await _booking_service_checkout_safe(Booking.BookingId, $"Extra charges: {ExtraCharges.Count}");
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("Lỗi", $"Không thể trả phòng: {ex.Message}", "OK");
            return;
        }

        // Persist extras and apply deposit as discount
        var extrasSum = ExtraCharges.Sum(c => c.Amount);
        var deposit = Booking?.DepositAmount ?? 0m;

        // Pass deposit as discount so invoice computation subtracts it from room subtotal
        _invoiceId = await _invoice_service_create_safe(_stayId, previewTaxRate, deposit, extrasSum);

        var inv = await _invoice_service_get_safe(_invoiceId);
        if (inv != null)
        {
            // synchronize preview values with server-calculated invoice so message matches UI
            previewSubtotal = inv.SubtotalRoom + inv.SubtotalService - inv.DiscountAmount;
            previewTax = System.Math.Round(previewSubtotal * (inv.TaxRate / 100m));
            previewTotal = inv.TotalAmount;

            // refresh bindings so TotalDisplay shows updated number
            OnPropertyChanged(nameof(SubtotalDisplay));
            OnPropertyChanged(nameof(TaxDisplay));
            OnPropertyChanged(nameof(TotalDisplay));

            // show created notification using the previewTotal (which now matches invoice)
            InvoiceCreated = true;
            await Application.Current.MainPage.DisplayAlert("Đã tạo hóa đơn", $"Hóa đơn #{inv.InvoiceId} đã tạo. Thu: {previewTotal:N0} VND", "OK");
        }

        // after creation hide preview
        IsPreviewVisible = false;

        // refresh stay/invoice ids as needed
        _ = LoadAsync(Booking.BookingId);
    }

    private async Task PayAsync()
    {
        if (_invoiceId <= 0)
        {
            await Application.Current.MainPage.DisplayAlert("Lỗi", "Chưa có hóa đơn. Vui lòng tạo hóa đơn trước.", "OK");
            return;
        }

        var inv = await _invoice_service_get_safe(_invoiceId);
        if (inv == null) return;
        var total = inv.TotalAmount;

        await _invoice_service_pay_safe(_invoiceId, total, "Cash", null);
        await Application.Current.MainPage.DisplayAlert("Thành công", "Đã thanh toán.", "OK");
    }

    private async Task ExportCsvAsync()
    {
        if (_invoiceId <= 0)
        {
            await Application.Current.MainPage.DisplayAlert("Lỗi", "Chưa có hóa đơn để xuất.", "OK");
            return;
        }

        var path = await _invoice_service_export_safe(_invoiceId);
        var file = new ShareFile(path);
        var request = new ShareFileRequest
        {
            Title = $"Invoice_{_invoiceId}",
            File = file
        };
        await Share.RequestAsync(request);
    }

    // small helper class for extra charges
    public class ExtraCharge
    {
        public string Description { get; set; } = "";
        public decimal Amount { get; set; }
    }

    // payment item for UI
    public class PaymentItem
    {
        public string Description { get; set; } = "";
        public decimal Amount { get; set; }
    }

    // Service wrappers to keep code readable and testable
    private Task<Booking?> _booking_service_get_safe(int bookingId) => _bookingService.GetBookingAsync(bookingId);
    private Task<Stay?> _booking_service_getactive_safe(int bookingId) => _bookingService.GetActiveStayForBookingAsync(bookingId);
    private Task<Stay?> _booking_service_get_checkedout_safe(int bookingId) => _booking_service_getactive_safe(bookingId); // fallback same call; keep for clarity
    private Task<int> _booking_service_checkout_safe(int bookingId, string? notes) => _bookingService.CheckOutBookingAsync(bookingId, notes);

    private Task<int> _invoice_service_create_safe(int stayId, decimal tax, decimal discount, decimal extras)
        => _invoiceService.CreateInvoiceAsync(stayId, tax, discount, extras);
    private Task<Invoice?> _invoice_service_get_safe(int invoiceId) => _invoiceService.GetInvoiceAsync(invoiceId);
    private Task _invoice_service_pay_safe(int invoiceId, decimal amount, string method, string? refCode) => _invoiceService.PayAsync(invoiceId, amount, method, refCode);
    private Task<string> _invoice_service_export_safe(int invoiceId) => _invoiceService.ExportInvoiceCsvAsync(invoiceId);
}