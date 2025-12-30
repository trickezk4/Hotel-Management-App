using HotelApp.Models;
using HotelApp.Services;
using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace HotelApp.ViewModels;

[QueryProperty(nameof(RoomId), "roomId")]
public class RoomUpdateViewModel : BaseViewModel
{
    private readonly RoomService _roomService;

    public Room Room { get; set; } = new Room();

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

    private string roomNumber = string.Empty;
    public string RoomNumber
    {
        get => roomNumber;
        set
        {
            if (SetProperty(ref roomNumber, value))
            {
                Room.RoomNumber = value;
            }
        }
    }

    private int floor = 1;
    public int Floor
    {
        get => floor;
        set
        {
            if (SetProperty(ref floor, Math.Clamp(value, 1, 100)))
            {
                floorText = floor.ToString();
                OnPropertyChanged(nameof(FloorText));
                OnPropertyChanged(nameof(FloorValue));
                Room.Floor = floor;
            }
        }
    }

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
                    floorText = num.ToString();
                }
                else
                {
                    floorText = Floor.ToString();
                    OnPropertyChanged(nameof(FloorText));
                }
            }
        }
    }

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    private int roomId;
    public int RoomId
    {
        get => roomId;
        set
        {
            if (SetProperty(ref roomId, value))
            {
                _ = LoadForEditAsync(value);
            }
        }
    }

    public RoomUpdateViewModel(RoomService roomService)
    {
        _roomService = roomService;
        SaveCommand = new Command(async () => await OnSaveAsync());
        CancelCommand = new Command(OnCancel);
        _ = LoadRoomTypesAsync();
    }

    private async Task LoadRoomTypesAsync()
    {
        var types = await _roomService.GetRoomTypesAsync();
        RoomTypes.Clear();
        foreach (var t in types) RoomTypes.Add(t);
    }

    private async Task LoadForEditAsync(int id)
    {
        // ensure types loaded (so we can select the right type)
        if (RoomTypes.Count == 0) await LoadRoomTypesAsync();

        var existing = await _roomService.GetRoomAsync(id);
        if (existing is null)
        {
            await Application.Current.MainPage.DisplayAlert("Lỗi", $"Không tìm thấy phòng id={id}", "OK");
            await Shell.Current.GoToAsync("..");
            return;
        }

        Room = existing;
        OnPropertyChanged(nameof(Room));

        // populate form fields
        RoomNumber = existing.RoomNumber;
        Floor = existing.Floor;
        SelectedStatus = existing.Status;
        SelectedRoomType = RoomTypes.FirstOrDefault(rt => rt.RoomTypeId == existing.RoomTypeId)
                           ?? RoomTypes.FirstOrDefault();
    }

    private async Task OnSaveAsync()
    {
        if (string.IsNullOrWhiteSpace(RoomNumber))
        {
            await Application.Current.MainPage.DisplayAlert("Lỗi", "Số phòng là bắt buộc, vui lòng nhập.", "OK");
            return;
        }

        // Duplicate check: exclude current room
        var rooms = await _roomService.GetRoomsAsync(searchText: RoomNumber.Trim());
        if (rooms.Any(r => r.RoomNumber == RoomNumber.Trim() && r.RoomId != Room.RoomId))
        {
            await Application.Current.MainPage.DisplayAlert("Lỗi", $"Phòng {RoomNumber} đã tồn tại.", "OK");
            return;
        }

        // sync
        Room.RoomNumber = RoomNumber.Trim();
        Room.Floor = Floor;
        if (SelectedRoomType != null) Room.RoomTypeId = SelectedRoomType.RoomTypeId;
        if (!string.IsNullOrEmpty(SelectedStatus)) Room.Status = SelectedStatus;

        await _roomService.UpdateRoomAsync(Room);
        await Shell.Current.GoToAsync("..");
    }

    private void OnCancel()
    {
        _ = Shell.Current.GoToAsync("..");
    }
}