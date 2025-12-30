using System.Collections.ObjectModel;
using System.Windows.Input;
using HotelApp.Models;
using HotelApp.Services;
using Microsoft.Maui.Controls;

namespace HotelApp.ViewModels;

public class BookingViewModel : BaseViewModel
{
    private readonly BookingService _bookingService;
    private readonly RoomService _roomService;

    // Dữ liệu cho UI
    public ObservableCollection<Room> AvailableRooms { get; } = new();
    public ObservableCollection<Booking> Bookings { get; } = new();

    // Trường nhập liệu
    private string customerName = "";
    public string CustomerName { get => customerName; set => SetProperty(ref customerName, value); }

    private string phone = "";
    public string Phone { get => phone; set => SetProperty(ref phone, value); }

    private Room? selectedRoom;
    public Room? SelectedRoom
    {
        get => selectedRoom;
        set
        {
            if (SetProperty(ref selectedRoom, value))
            {
                _ = RecalculateEstimatedCostAsync();
            }
        }
    }

    private DateTime checkInDate = DateTime.Today;
    public DateTime CheckInDate
    {
        get => checkInDate;
        set
        {
            if (SetProperty(ref checkInDate, value))
            {
                _ = RecalculateEstimatedCostAsync();
            }
        }
    }

    private TimeSpan checkInTime = new TimeSpan(14, 0, 0); // default 14:00
    public TimeSpan CheckInTime
    {
        get => checkInTime;
        set
        {
            if (SetProperty(ref checkInTime, value))
            {
                _ = RecalculateEstimatedCostAsync();
            }
        }
    }

    private DateTime checkOutDate = DateTime.Today.AddDays(1);
    public DateTime CheckOutDate
    {
        get => checkOutDate;
        set
        {
            if (SetProperty(ref checkOutDate, value))
            {
                _ = RecalculateEstimatedCostAsync();
            }
        }
    }

    private TimeSpan checkOutTime = new TimeSpan(12, 0, 0); // default 12:00
    public TimeSpan CheckOutTime
    {
        get => checkOutTime;
        set
        {
            if (SetProperty(ref checkOutTime, value))
            {
                _ = RecalculateEstimatedCostAsync();
            }
        }
    }

    private decimal depositAmount;
    public decimal DepositAmount { get => depositAmount; set => SetProperty(ref depositAmount, value); }

    public List<string> BookingStatuses { get; } = new() { "Pending", "Confirmed", "Cancelled", "CheckedIn" };
    private string selectedStatus = "Pending";
    public string SelectedStatus { get => selectedStatus; set => SetProperty(ref selectedStatus, value); }

    // Estimated cost
    private decimal estimatedCost;
    public decimal EstimatedCost { get => estimatedCost; set => SetProperty(ref estimatedCost, value); }

    // Command
    public ICommand LoadRoomsCommand { get; }
    public ICommand ValidateAvailabilityCommand { get; }
    public ICommand SaveBookingCommand { get; }

    // Management commands
    public ICommand LoadBookingsCommand { get; }
    public ICommand CancelBookingCommand { get; }
    public ICommand CheckInCommand { get; }
    public ICommand ViewBookingCommand { get; } // new: navigate to booking detail
    public ICommand CheckOutCommand { get; } // NEW

    public BookingViewModel(BookingService bookingService, RoomService roomService)
    {
        _bookingService = bookingService;
        _roomService = roomService;

        LoadRoomsCommand = new Command(async () => await LoadRoomsAsync());
        ValidateAvailabilityCommand = new Command(async () => await ValidateAsync());
        SaveBookingCommand = new Command(async () => await SaveAsync());

        LoadBookingsCommand = new Command(async () => await LoadBookingsAsync());
        CancelBookingCommand = new Command<Booking>(async b => await CancelBookingAsync(b));
        CheckInCommand = new Command<Booking>(async b => await CheckInAsync(b));

        ViewBookingCommand = new Command<Booking>(async b =>
        {
            if (b == null) return;
            await Shell.Current.GoToAsync($"{nameof(HotelApp.Views.BookingDetailPage)}?bookingId={b.BookingId}");
        });

        // Navigation to CheckOut page for checked-in bookings
        CheckOutCommand = new Command<Booking>(async b =>
        {
            if (b == null) return;
            // Ensure there is an active stay if needed; simple navigation:
            await Shell.Current.GoToAsync($"{nameof(HotelApp.Views.CheckOutPage)}?bookingId={b.BookingId}");
        });
    }

    // Made public so page code-behind can call it directly
    public async Task LoadRoomsAsync()
    {
        // Only show rooms with Status == "Available"
        var rooms = (await _roomService.GetRoomsAsync()).Where(r => string.Equals(r.Status, "Available", StringComparison.OrdinalIgnoreCase)).ToList();

        // Ensure UI update happens on UI thread and awaited so callers get latest state
        await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
        {
            AvailableRooms.Clear();
            foreach (var r in rooms) AvailableRooms.Add(r);
            // select first available by default
            if (AvailableRooms.Count > 0 && SelectedRoom == null) SelectedRoom = AvailableRooms.First();
        });
    }

    private async Task ValidateAsync()
    {
        if (SelectedRoom is null) { await Application.Current.MainPage.DisplayAlert("Lỗi", "Chọn phòng.", "OK"); return; }
        var from = CheckInDate.Date + CheckInTime;
        var to = CheckOutDate.Date + CheckOutTime;
        var ok = await _bookingService.IsRoomAvailableAsync(SelectedRoom.RoomId, from, to);
        await Application.Current.MainPage.DisplayAlert("Kiểm tra lịch", ok ? "Phòng trống." : "Phòng đã có lịch.", "OK");
    }

    private async Task SaveAsync()
    {
        if (SelectedRoom is null) { await Application.Current.MainPage.DisplayAlert("Lỗi", "Chọn phòng.", "OK"); return; }
        var from = CheckInDate.Date + CheckInTime;
        var to = CheckOutDate.Date + CheckOutTime;
        if (to <= from) { await Application.Current.MainPage.DisplayAlert("Lỗi", "Ngày trả không hợp lệ.", "OK"); return; }

        var customer = new Customer { FullName = CustomerName?.Trim() ?? "", Phone = Phone?.Trim(), CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now };

        var booking = new Booking
        {
            Customer = customer,
            RoomId = SelectedRoom.RoomId,
            CheckInDate = from,
            CheckOutDate = to,
            BookingStatus = SelectedStatus,
            DepositAmount = DepositAmount,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        try
        {
            var ok = await _booking_service_safe(booking.RoomId, booking.CheckInDate, booking.CheckOutDate);
            if (!ok) { await Application.Current.MainPage.DisplayAlert("Lỗi", "Phòng đã trùng lịch.", "OK"); return; }

            await _bookingService.CreateBookingAsync(booking);
            await Application.Current.MainPage.DisplayAlert("Thành công", "Đã lưu đặt phòng.", "OK");

            // reload bookings and rooms and clear form
            await LoadBookingsAsync();
            await LoadRoomsAsync();
            CustomerName = Phone = "";
            SelectedRoom = AvailableRooms.FirstOrDefault();
            DepositAmount = 0;
            EstimatedCost = 0;
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("Lỗi", $"Không thể tạo đặt phòng: {ex.Message}", "OK");
        }
    }

    private Task<bool> _booking_service_safe(int roomId, DateTime from, DateTime to)
        => _bookingService.IsRoomAvailableAsync(roomId, from, to);

    // Management methods
    public async Task LoadBookingsAsync()
    {
        var list = await _booking_service_getall_safe();

        // Make sure collection update happens on UI thread and is awaited so changes reflect immediately
        await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
        {
            Bookings.Clear();
            foreach (var b in list) Bookings.Add(b);
        });
    }

    private Task<List<Booking>> _booking_service_getall_safe() => _bookingService.GetBookingsAsync();

    private async Task CancelBookingAsync(Booking? b)
    {
        if (b is null) return;

        // Do not allow cancelling after check-in
        if (string.Equals(b.BookingStatus, "CheckedIn", StringComparison.OrdinalIgnoreCase))
        {
            await Application.Current.MainPage.DisplayAlert("Không thể hủy", "Không thể hủy do đã Check-in", "OK");
            return;
        }

        // Confirm cancellation
        bool confirm = await Application.Current.MainPage.DisplayAlert("Hủy đặt phòng", $"Hủy đặt phòng của {b.Customer?.FullName} (Phòng {b.Room?.RoomNumber})?", "Hủy", "Không");
        if (!confirm) return;

        // Cancel booking in service (sets CancelledBy/CancelledAt)
        await _bookingService.CancelBookingAsync(b.BookingId);
        await Application.Current.MainPage.DisplayAlert("Thành công", "Đã hủy đặt phòng.", "OK");

        // Ensure the related room is set to Available if not Occupied (force update from UI layer)
        try
        {
            var room = await _roomService.GetRoomAsync(b.RoomId);
            if (room != null && !string.Equals(room.Status, "Occupied", StringComparison.OrdinalIgnoreCase))
            {
                room.Status = "Available";
                await _room_service_update_safe(room);
            }
        }
        catch
        {
            // ignore room-update failure (service already attempted), but we still refresh UI lists
        }

        // Refresh lists immediately
        await LoadBookingsAsync();
        await LoadRoomsAsync(); // freed room may re-appear
    }

    private Task _room_service_update_safe(Room r) => _roomService.UpdateRoomAsync(r);

    private async Task CheckInAsync(Booking? b)
    {
        if (b is null) return;

        // Prevent check-in if booking was cancelled or already checked in
        if (string.Equals(b.BookingStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            await Application.Current.MainPage.DisplayAlert("Lỗi", "Đã hủy đặt phòng, không thể nhận phòng.", "OK");
            return;
        }
        if (string.Equals(b.BookingStatus, "CheckedIn", StringComparison.OrdinalIgnoreCase))
        {
            await Application.Current.MainPage.DisplayAlert("Lỗi", "Booking đã được nhận phòng.", "OK");
            return;
        }

        bool confirm = await Application.Current.MainPage.DisplayAlert("Check-in", $"Xác nhận nhận phòng cho {b.Customer?.FullName}?", "Check-in", "Hủy");
        if (!confirm) return;

        try
        {
            await _bookingService.CheckInBookingAsync(b.BookingId);
            await Application.Current.MainPage.DisplayAlert("Thành công", "Khách đã nhận phòng.", "OK");

            // Immediately refresh bookings and rooms so UI reflects CheckedIn status
            await LoadBookingsAsync();
            await LoadRoomsAsync();
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("Lỗi", $"Không thể check-in: {ex.Message}", "OK");
        }
    }

    // Recalculate estimated cost using Room info (room type base price)
    private async Task RecalculateEstimatedCostAsync()
    {
        if (SelectedRoom is null)
        {
            EstimatedCost = 0;
            return;
        }

        try
        {
            var from = CheckInDate.Date + CheckInTime;
            var to = CheckOutDate.Date + CheckOutTime;
            if (to <= from)
            {
                EstimatedCost = 0;
                return;
            }

            var cost = await _bookingService.CalculateEstimatedCostAsync(SelectedRoom.RoomId, from, to);
            EstimatedCost = cost;
        }
        catch
        {
            EstimatedCost = 0;
        }
    }
}