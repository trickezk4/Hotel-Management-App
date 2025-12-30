using HotelApp.ViewModels;

namespace HotelApp.Views;

public partial class BookingDetailPage : ContentPage
{
    public BookingDetailPage(BookingDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}