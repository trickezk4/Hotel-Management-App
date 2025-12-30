using HotelApp.Models;
using HotelApp.Services;
using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using System.Windows.Input;
//using static System.Net.Mime.MediaTypeNames;


namespace HotelApp.ViewModels;

public class RoomEditViewModel : BaseViewModel
{
    private readonly RoomService _roomService;

    // Entity đang chỉnh sửa
    public Room Room { get; set; } = new Room();

    // Danh sách loại phòng để hiển thị trong Picker
    public ObservableCollection<RoomType> RoomTypes { get; set; } = new();

    private RoomType? selectedRoomType;
    public RoomType? SelectedRoomType
    {
        get => selectedRoomType;
        set
        {
            if (SetProperty(ref selectedRoomType, value) && value != null)
            {
                Room.RoomTypeId = value.RoomTypeId;
            }
        }
    }

    // Danh sách trạng thái phòng
    public ObservableCollection<string> RoomStatuses { get; set; } =
        new ObservableCollection<string> { "Available", "Occupied", "Maintenance" };

    private string? selectedStatus;
    public string? SelectedStatus
    {
        get => selectedStatus;
        set
        {
            if (SetProperty(ref selectedStatus, value) && value != null)
            {
                Room.Status = value;
            }
        }
    }

    //Xử lý nhập số phòng 
    private string roomNumber;
    public string RoomNumber
    {
        get => roomNumber;
        set
        {
            if (SetProperty(ref roomNumber, value))
            {
                //Đồng bộ sang entity
                Room.RoomNumber = value;
            }

        }
    }

    // Text để binding với Entry, xử lý nhập số
    private int floor = 1;
    public int Floor
    {
        get => floor;
        set
        {
            if (SetProperty(ref floor, Math.Clamp(value, 1, 100)))
            {
                // Khi Floor thay đổi thì cập nhật lại FloorText và FloorValue
                floorText = floor.ToString();
                OnPropertyChanged(nameof(FloorText));
                OnPropertyChanged(nameof(FloorValue));

                // Cập nhật giá trị Floor trong Room entity
                Room.Floor = floor;
            }
        }
    }

    // Dùng cho Stepper
    public double FloorValue
    {
        get => Floor;
        set
        {
            int num = (int)Math.Clamp(value, 1, 100);
            if (Floor != num)
            {
                Floor = num;
            }
        }
    }

    // Dùng cho Entry
    private string floorText = "1";
    public string FloorText
    {
        get => floorText;
        set
        {
            if (SetProperty(ref floorText, value))
            {
                if (int.TryParse(value, out var num))
                {
                    num = Math.Clamp(num, 1, 100);
                    Floor = num;
                    // đồng bộ lại text để loại bỏ ký tự ngoài số
                    floorText = num.ToString();
                }
                else
                {
                    // Nếu nhập ký tự không phải số → reset về giá trị hiện tại
                    floorText = Floor.ToString();
                    OnPropertyChanged(nameof(FloorText));
                }
            }
        }
    }

    // Command
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    // Constructor
    public RoomEditViewModel(RoomService roomService)
    {
        _roomService = roomService;

        SaveCommand = new Command(async () => await OnSave());
        CancelCommand = new Command(OnCancel);

        // Load dữ liệu hỗ trợ
        _ = LoadRoomTypes();
    }

    // Load danh sách loại phòng từ DB
    private async Task LoadRoomTypes()
    {
        var types = await _roomService.GetRoomTypesAsync();
        RoomTypes.Clear();
        foreach (var t in types)
            RoomTypes.Add(t);

        //Set mặc định loại phòng "Standard"
        SelectedRoomType = RoomTypes.FirstOrDefault(rt => rt.Name == "Standard");
    }

    // Lưu dữ liệu phòng
    private async Task OnSave()
    {
        // Validate số phòng là bắt buộc
        if (string.IsNullOrWhiteSpace(RoomNumber))
        {
            await Application.Current.MainPage.DisplayAlert(
                "Lỗi",
                "Số phòng là bắt buộc, vui lòng nhập.",
                "OK");
            return;
        }

        // đồng bộ lại trước khi lưu
        Room.RoomNumber = RoomNumber.Trim();
        Room.Floor = Floor;

        // Kiểm tra trùng số phòng
        var duplicate = await _roomService.ExistsRoomAsync(Room.RoomNumber);

        if (duplicate)
        {
            await Application.Current.MainPage.DisplayAlert(
                "Lỗi",
                $"Phòng {Room.RoomNumber} đã tồn tại.",
                "OK");
            return;
        }

        if (Room.RoomId == 0)
        {
            await _roomService.AddRoomAsync(Room);
        }
        else
        {
            await _roomService.UpdateRoomAsync(Room);
        }

        // TODO: điều hướng quay lại RoomListPage
        await Shell.Current.GoToAsync("..");
    }

    // Hủy bỏ chỉnh sửa
    private void OnCancel()
    {
        // Quay lại trang trước
        Shell.Current.GoToAsync("..");
    }

    // Hàm hỗ trợ binding (INotifyPropertyChanged)
    protected bool SetProperty<T>(ref T backingStore, T value,
        [System.Runtime.CompilerServices.CallerMemberName] string propertyName = "",
        Action? onChanged = null)
    {
        if (EqualityComparer<T>.Default.Equals(backingStore, value))
            return false;

        backingStore = value;
        onChanged?.Invoke();
        OnPropertyChanged(propertyName);
        return true;
    }
}
