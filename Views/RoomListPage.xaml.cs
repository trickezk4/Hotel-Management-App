using HotelApp.ViewModels;

namespace HotelApp.Views;

// Code-behind: nhận ViewModel qua DI và gán làm BindingContext
public partial class RoomListPage : ContentPage
{
    public RoomListPage(RoomListViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm; // Kết nối XAML tới ViewModel
    }
}
