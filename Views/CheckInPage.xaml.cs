using HotelApp.ViewModels;

namespace HotelApp.Views;

public partial class CheckInPage : ContentPage
{
	public CheckInPage(CheckInViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
	}
}