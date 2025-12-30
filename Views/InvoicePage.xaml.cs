using HotelApp.ViewModels;

namespace HotelApp.Views;

public partial class InvoicePage : ContentPage
{
    public InvoicePage(InvoiceViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
