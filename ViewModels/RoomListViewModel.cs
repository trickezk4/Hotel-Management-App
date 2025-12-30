using System.Collections.ObjectModel;
using System.Windows.Input;
using HotelApp.Models;
using HotelApp.Services;
using HotelApp.Views;

namespace HotelApp.ViewModels;

// ViewModel cho màn danh sách phòng: kết nối dữ liệu và command UI
public class RoomListViewModel
{
    private readonly RoomService _roomService;
    private readonly BookingService _bookingService;

    // Nguồn dữ liệu bind lên UI
    public ObservableCollection<Room> Rooms { get; } = new();
    // Danh sách loại phòng để hiển thị trong Picker
    public ObservableCollection<RoomType> RoomTypes { get; } = new();
    public List<string> RoomStatuses { get; } = new() { "Any", "Available", "Booked", "Occupied", "Maintenance" };

    // Giá trị filter từ UI
    public RoomType? SelectedRoomType { get; set; }
    public string? SelectedStatus { get; set; }
    public string? SearchText { get; set; }

    // Command được bind từ XAML (nút Làm mới/Thêm phòng)
    public ICommand RefreshCommand { get; }
    public ICommand AddRoomCommand { get; }
    public ICommand ViewRoomCommand { get; }
    public ICommand EditRoomCommand { get; }
    public ICommand DeleteRoomCommand { get; }

    public RoomListViewModel(RoomService roomService, BookingService bookingService)
    {
        _roomService = roomService;
        _bookingService = bookingService;

        // Khởi tạo command: gọi hàm async tải dữ liệu
        RefreshCommand = new Command(async () => await LoadRoomsAsync());
        AddRoomCommand = new Command(async () => await Shell.Current.GoToAsync(nameof(RoomEditPage)));
        // Command xem chi tiết
        ViewRoomCommand = new Command<Room>(async (room) =>
        {
            if (room == null) return;

            var status = room.Status ?? string.Empty;

            // For Booked or Occupied try to open active booking detail
            if (string.Equals(status, "Booked", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "Occupied", StringComparison.OrdinalIgnoreCase))
            {
                var booking = await _booking_service_getactive_safe(room.RoomId);
                if (booking != null)
                {
                    await Shell.Current.GoToAsync($"{nameof(HotelApp.Views.BookingDetailPage)}?bookingId={booking.BookingId}");
                    return;
                }
            }

            // If room is Available we may still have a recent cancelled booking -> show cancellation detail
            if (string.Equals(status, "Available", StringComparison.OrdinalIgnoreCase))
            {
                var latest = await _booking_service_getlatest_safe(room.RoomId);
                if (latest != null && string.Equals(latest.BookingStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    await Shell.Current.GoToAsync($"{nameof(HotelApp.Views.BookingDetailPage)}?bookingId={latest.BookingId}");
                    return;
                }
            }

            // For Maintenance or fallback show room details page
            await Shell.Current.GoToAsync($"{nameof(RoomDetailPage)}?roomId={room.RoomId}");
        });

        // Command sửa
        EditRoomCommand = new Command<Room>(async (room) =>
        {
            if (room != null)
            {
                // navigate to the new page that uses RoomUpdateViewModel
                await Shell.Current.GoToAsync($"{nameof(RoomUpdatePage)}?roomId={room.RoomId}");
            }
        });

        // Command xóa
        DeleteRoomCommand = new Command<Room>(async (room) =>
        {
            if (room != null)
            {
                bool confirm = await Application.Current.MainPage.DisplayAlert(
                    "Xóa phòng",
                    $"Bạn có chắc muốn xóa phòng {room.RoomNumber}?",
                    "Xóa", "Hủy");

                if (confirm)
                {
                    await _roomService.DeleteRoomAsync(room.RoomId);
                    await LoadRoomsAsync();
                }
            }
        });
        // Tải dữ liệu ban đầu (không chặn UI)
        Task.Run(async () =>
        {
            await LoadRoomTypesAsync();
            await LoadRoomsAsync();
        });
    }

    private Task<Booking?> _booking_service_getactive_safe(int roomId) => _bookingService.GetActiveBookingForRoomAsync(roomId);
    private Task<Booking?> _booking_service_getlatest_safe(int roomId) => _bookingService.GetLatestBookingForRoomAsync(roomId);

    // Tải danh sách loại phòng để hiển thị filter
    private async Task LoadRoomTypesAsync()
    {
        var types = await _room_service_gettypes_safe();
        MainThread.BeginInvokeOnMainThread(() =>
        {
            RoomTypes.Clear();
            //Thêm option "Any" đầu tiên
            RoomTypes.Add(new RoomType()
            {
                RoomTypeId = 0,
                Name = "Any"
            });
            foreach (var t in types) RoomTypes.Add(t);

            //Mặc định chọn "Any"
            SelectedRoomType = RoomTypes.FirstOrDefault();
            SelectedStatus = "Any";
        });
    }

    private Task<List<RoomType>> _room_service_gettypes_safe() => _roomService.GetRoomTypesAsync();

    // Tải danh sách phòng theo filter hiện tại
    public async Task LoadRoomsAsync()
    {
        int? typeId = null;
        if (SelectedRoomType != null && SelectedRoomType.Name != "Any")
            typeId = SelectedRoomType.RoomTypeId;

        string? status = null;
        if (!string.IsNullOrEmpty(SelectedStatus) && SelectedStatus != "Any")
            status = SelectedStatus;

        var rooms = await _roomService.GetRoomsAsync(typeId, status, SearchText);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Rooms.Clear();
            foreach (var r in rooms) Rooms.Add(r);
        });
    }
}
