using HotelApp.Models;
using HotelApp.Services;
using Microsoft.Maui.Controls;
using System.Threading.Tasks;

namespace HotelApp.ViewModels
{
    [QueryProperty(nameof(RoomId), "roomId")]
    public class RoomDetailViewModel : BaseViewModel
    {
        private readonly RoomService _roomService;

        // Thuộc tính Room để binding sang UI
        private Room room;
        public Room Room
        {
            get => room;
            set => SetProperty(ref room, value);
        }

        // Nhận roomId từ Shell
        private int roomId;
        public int RoomId
        {
            get => roomId;
            set
            {
                if (SetProperty(ref roomId, value))
                {
                    // Khi RoomId thay đổi, load dữ liệu
                    _ = LoadRoomAsync(value);
                }
            }
        }

        public RoomDetailViewModel(RoomService roomService)
        {
            _roomService = roomService;
        }

        // Hàm load dữ liệu phòng
        public async Task LoadRoomAsync(int id)
        {
            var room = await _roomService.GetRoomAsync(id);
            if (room != null)
            {
                Room = room;
            }
            else
            {
                await Application.Current.MainPage.DisplayAlert(
                    "Lỗi",
                    $"Không tìm thấy phòng với ID {id}",
                    "OK");
            }
        }
    }
}
