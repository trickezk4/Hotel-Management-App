using HotelApp.ViewModels;

namespace HotelApp.Views;

public partial class UserManagementPage : ContentPage
{
    public UserManagementPage()
    {
        InitializeComponent();
    }

    public UserManagementPage(UserManagementViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}