
using HotelApp.Models;
using HotelApp.Services;
using Microsoft.Maui.Controls;
using System.Windows.Input;

namespace HotelApp.ViewModels;

public class LoginViewModel : BaseViewModel
{
    private readonly AuthService _auth;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";

    public ICommand LoginCommand { get; }

    public LoginViewModel(AuthService auth)
    {
        _auth = auth;
        LoginCommand = new Command(async () => await LoginAsync());
    }

    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            await Application.Current.MainPage.DisplayAlert("Error", "Please enter username and password", "OK");
            return;
        }

        User? user;
        try
        {
            user = await _auth.AuthenticateAsync(Username.Trim(), Password);
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("Error", $"Authentication failed: {ex.Message}", "OK");
            return;
        }

        if (user == null)
        {
            await Application.Current.MainPage.DisplayAlert("Error", "Invalid credentials", "OK");
            return;
        }

        // Navigate to main app area using absolute route. Route "rooms" is defined in AppShell.xaml.
        try
        {
            // Use '//' to navigate to the route as absolute (reset navigation stack)
            await Shell.Current.GoToAsync("//rooms");
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("Navigation error", ex.Message, "OK");
        }
        finally
        {
            // Clear password for security
            Password = "";
        }
    }
}