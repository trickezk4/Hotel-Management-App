using HotelApp.ViewModels;

namespace HotelApp.Views;

public partial class CheckOutPage : ContentPage
{
    public CheckOutPage(CheckOutViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}