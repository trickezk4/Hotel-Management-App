using HotelApp.ViewModels;

namespace HotelApp.Views;

public partial class RoomEditPage : ContentPage
{
    public RoomEditPage(RoomEditViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
