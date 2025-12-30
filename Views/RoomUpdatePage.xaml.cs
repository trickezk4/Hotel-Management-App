using HotelApp.ViewModels;

namespace HotelApp.Views;

public partial class RoomUpdatePage : ContentPage
{
	public RoomUpdatePage(RoomUpdateViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
    }
}