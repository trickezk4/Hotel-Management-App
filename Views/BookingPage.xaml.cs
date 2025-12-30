// Views/BookingPage.xaml.cs
using HotelApp.ViewModels;

namespace HotelApp.Views;

public partial class BookingPage : ContentPage
{
    public BookingPage(BookingViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm; // Gắn ViewModel qua DI
    }
}
