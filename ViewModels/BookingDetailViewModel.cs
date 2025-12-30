using HotelApp.Models;
using HotelApp.Services;
using Microsoft.Maui.Controls;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace HotelApp.ViewModels
{
    [QueryProperty(nameof(BookingId), "bookingId")]
    public class BookingDetailViewModel : BaseViewModel
    {
        private readonly BookingService _bookingService;
        private readonly InvoiceService _invoiceService;

        private Booking? booking;
        public Booking? Booking
        {
            get => booking;
            set
            {
                if (SetProperty(ref booking, value))
                {
                    UpdateFlags();
                }
            }
        }

        private bool isBookingSectionVisible;
        public bool IsBookingSectionVisible { get => isBookingSectionVisible; set => SetProperty(ref isBookingSectionVisible, value); }

        private bool isInvoiceSectionVisible;
        public bool IsInvoiceSectionVisible { get => isInvoiceSectionVisible; set => SetProperty(ref isInvoiceSectionVisible, value); }

        private bool isRoomSectionVisible = true;
        public bool IsRoomSectionVisible { get => isRoomSectionVisible; set => SetProperty(ref isRoomSectionVisible, value); }

        private bool isCancelled;
        public bool IsCancelled { get => isCancelled; set => SetProperty(ref isCancelled, value); }

        private bool isMaintenance;
        public bool IsMaintenance { get => isMaintenance; set => SetProperty(ref isMaintenance, value); }

        private decimal estimatedCost;
        public decimal EstimatedCost { get => estimatedCost; set => SetProperty(ref estimatedCost, value); }

        // Cancellation display props
        private string cancelledByDisplay = "";
        public string CancelledByDisplay { get => cancelledByDisplay; set => SetProperty(ref cancelledByDisplay, value); }

        private string cancelledAtDisplay = "";
        public string CancelledAtDisplay { get => cancelledAtDisplay; set => SetProperty(ref cancelledAtDisplay, value); }

        // Invoice/payment items (shown when booking CheckedOut)
        public ObservableCollection<PaymentItem> PaymentItems { get; } = new();
        private decimal invoiceSubtotal;
        private decimal invoiceTax;
        private decimal invoiceTotal;
        public string InvoiceSubtotalDisplay => $"Tổng cộng: {invoiceSubtotal:N0} VND";
        public string InvoiceTaxDisplay => $"VAT(10%): {invoiceTax:N0} VND";
        public string InvoiceTotalDisplay => $"Thu: {invoiceTotal:N0} VND";

        private int bookingId;
        public int BookingId
        {
            get => bookingId;
            set
            {
                if (SetProperty(ref bookingId, value))
                {
                    _ = LoadBookingAsync(value);
                }
            }
        }

        public BookingDetailViewModel(BookingService bookingService, InvoiceService invoiceService)
        {
            _bookingService = bookingService;
            _invoiceService = invoiceService;
        }

        private void UpdateFlags()
        {
            if (Booking == null)
            {
                IsBookingSectionVisible = false;
                IsInvoiceSectionVisible = false;
                IsCancelled = false;
                IsMaintenance = false;
                CancelledByDisplay = "";
                CancelledAtDisplay = "";
                return;
            }

            var bookingStatus = Booking.BookingStatus ?? string.Empty;
            var roomStatus = Booking.Room?.Status ?? string.Empty;

            // If booking is CheckedOut -> show invoice section instead of standard booking info
            if (string.Equals(bookingStatus, "CheckedOut", StringComparison.OrdinalIgnoreCase))
            {
                IsInvoiceSectionVisible = true;
                IsBookingSectionVisible = false;
            }
            else
            {
                IsInvoiceSectionVisible = false;
                IsBookingSectionVisible =
                      string.Equals(bookingStatus, "Booked", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(bookingStatus, "CheckedIn", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(roomStatus, "Booked", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(roomStatus, "Occupied", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(bookingStatus, "Cancelled", StringComparison.OrdinalIgnoreCase);
            }

            // Maintenance flag
            IsMaintenance = string.Equals(roomStatus, "Maintenance", StringComparison.OrdinalIgnoreCase);

            // Cancellation display
            IsCancelled = string.Equals(bookingStatus, "Cancelled", StringComparison.OrdinalIgnoreCase);
            if (IsCancelled)
            {
                CancelledByDisplay = string.IsNullOrWhiteSpace(Booking.CancelledBy) ? "System" : Booking.CancelledBy!;
                CancelledAtDisplay = Booking.CancelledAt.HasValue ? Booking.CancelledAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : "—";
            }
            else
            {
                CancelledByDisplay = "";
                CancelledAtDisplay = "";
            }

            // Ensure room section is visible
            IsRoomSectionVisible = true;
        }

        public async Task LoadBookingAsync(int id)
        {
            var b = await _booking_service_get_safe(id);
            if (b == null)
            {
                await Application.Current.MainPage.DisplayAlert("Lỗi", $"Không tìm thấy đặt phòng {id}", "OK");
                return;
            }

            Booking = b;

            // Calculate estimated cost (same as before)
            try
            {
                EstimatedCost = await _bookingService.CalculateEstimatedCostAsync(b.RoomId, b.CheckInDate, b.CheckOutDate);
            }
            catch
            {
                EstimatedCost = 0;
            }

            // If booking is CheckedOut prepare invoice/payment items
            if (string.Equals(Booking.BookingStatus, "CheckedOut", StringComparison.OrdinalIgnoreCase))
            {
                await LoadInvoiceItemsAsync();
            }
            else
            {
                // clear invoice section
                PaymentItems.Clear();
                invoiceSubtotal = invoiceTax = invoiceTotal = 0;
                OnPropertyChanged(nameof(InvoiceSubtotalDisplay));
                OnPropertyChanged(nameof(InvoiceTaxDisplay));
                OnPropertyChanged(nameof(InvoiceTotalDisplay));
            }
        }

        private async Task LoadInvoiceItemsAsync()
        {
            PaymentItems.Clear();

            // Prefer the stored Invoice (by stay or booking) and use its fields directly
            // Try load stay and then invoice for that stay; if none, try invoice by booking id
            var stay = await _booking_service_getstay_safe(Booking!.BookingId);
            Invoice? inv = null;

            if (stay != null)
            {
                inv = await _invoice_service_get_by_stay_safe(stay.StayId);
            }

            if (inv == null)
            {
                inv = await _invoice_service_get_by_stay_safe(Booking.BookingId); // fallback: invoices stored against booking id
            }

            if (inv != null)
            {
                // Build UI lines from Invoice columns (do not recompute totals from room nights etc.)
                PaymentItems.Add(new PaymentItem { Description = "Giá phòng", Amount = inv.SubtotalRoom });

                if (inv.DiscountAmount > 0)
                    PaymentItems.Add(new PaymentItem { Description = "Đã đặt cọc", Amount = -inv.DiscountAmount });

                if (inv.SubtotalService > 0)
                    PaymentItems.Add(new PaymentItem { Description = "Chi phí phát sinh", Amount = inv.SubtotalService });

                // Use invoice fields directly for display:
                // subtotal shown as (SubtotalRoom - Discount + SubtotalService) to match what is stored
                invoiceSubtotal = inv.SubtotalRoom - inv.DiscountAmount + inv.SubtotalService;
                // tax amount derived from stored TotalAmount so UI matches DB: tax = TotalAmount - (room - discount + service)
                invoiceTax = Math.Round(inv.TotalAmount - invoiceSubtotal);
                invoiceTotal = inv.TotalAmount;

                OnPropertyChanged(nameof(InvoiceSubtotalDisplay));
                OnPropertyChanged(nameof(InvoiceTaxDisplay));
                OnPropertyChanged(nameof(InvoiceTotalDisplay));
                return;
            }

            // Fallback: if no invoice row exists, continue to show calculated lines from Stay (existing behavior)
            var fallbackStay = await _booking_service_getstay_safe(Booking.BookingId);
            if (fallbackStay == null)
            {
                // nothing to show
                invoiceSubtotal = invoiceTax = invoiceTotal = 0;
                OnPropertyChanged(nameof(InvoiceSubtotalDisplay));
                OnPropertyChanged(nameof(InvoiceTaxDisplay));
                OnPropertyChanged(nameof(InvoiceTotalDisplay));
                return;
            }

            // Determine nights using date-only difference (keeps previous behavior)
            var checkIn = fallbackStay.ActualCheckIn;
            var checkOut = fallbackStay.ActualCheckOut ?? fallbackStay.Booking?.CheckOutDate ?? checkIn.AddDays(1);
            var nights = Math.Max(1, (int)(checkOut.Date - checkIn.Date).TotalDays);
            var rate = fallbackStay.Room?.RoomType?.BasePrice ?? 0m;
            var roomAmount = rate * nights;

            PaymentItems.Add(new PaymentItem { Description = $"Giá phòng ({nights} đêm @ {rate:N0} đ/ngày)", Amount = roomAmount });

            var deposit = Booking?.DepositAmount ?? fallbackStay.Booking?.DepositAmount ?? 0m;
            if (deposit > 0)
            {
                PaymentItems.Add(new PaymentItem { Description = "Đã đặt cọc", Amount = -deposit });
            }

            if (fallbackStay.ServiceUsages != null && fallbackStay.ServiceUsages.Count > 0)
            {
                foreach (var su in fallbackStay.ServiceUsages)
                {
                    var desc = su.Service?.Name ?? su.Notes ?? "Chi phí phát sinh";
                    PaymentItems.Add(new PaymentItem { Description = desc, Amount = su.Quantity * su.UnitPrice });
                }

                invoiceSubtotal = PaymentItems.Sum(p => p.Amount);
                invoiceTax = Math.Round(invoiceSubtotal * 0.10m);
                invoiceTotal = invoiceSubtotal + invoiceTax;
            }
            else
            {
                invoiceSubtotal = PaymentItems.Sum(p => p.Amount);
                invoiceTax = Math.Round(invoiceSubtotal * 0.10m);
                invoiceTotal = invoiceSubtotal + invoiceTax;
            }

            OnPropertyChanged(nameof(InvoiceSubtotalDisplay));
            OnPropertyChanged(nameof(InvoiceTaxDisplay));
            OnPropertyChanged(nameof(InvoiceTotalDisplay));
        }

        private Task<Booking?> _booking_service_get_safe(int id) => _bookingService.GetBookingAsync(id);
        private Task<Stay?> _booking_service_getstay_safe(int bookingId) => _bookingService.GetStayForBookingAsync(bookingId);
        private Task<Invoice?> _invoice_service_get_by_stay_safe(int stayOrBookingId) => _invoiceService.GetInvoiceByStayAsync(stayOrBookingId);

        // helper class for UI binding
        public class PaymentItem
        {
            public string Description { get; set; } = "";
            public decimal Amount { get; set; }
        }
    }
}