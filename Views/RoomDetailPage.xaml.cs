using HotelApp.ViewModels;

namespace HotelApp.Views;

public partial class RoomDetailPage : ContentPage
{
    public RoomDetailPage(RoomDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(".."); // quay lại trang trước (trang quản lý)
    }
}
