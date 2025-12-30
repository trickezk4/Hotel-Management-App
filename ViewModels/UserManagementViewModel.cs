using HotelApp.Models;
using HotelApp.Services;
using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace HotelApp.ViewModels;

public class UserManagementViewModel : BaseViewModel
{
    private readonly AuthService _auth;
    public ObservableCollection<User> Users { get; } = new();
    public ObservableCollection<Role> Roles { get; } = new();

    // create user fields
    private string username = "";
    public string Username { get => username; set => SetProperty(ref username, value); }

    private string fullName = "";
    public string FullName { get => fullName; set => SetProperty(ref fullName, value); }

    private string password = "";
    public string Password { get => password; set => SetProperty(ref password, value); }

    private Role? selectedRole;
    public Role? SelectedRole { get => selectedRole; set => SetProperty(ref selectedRole, value); }

    public ICommand LoadCommand { get; }
    public ICommand CreateAdminCommand { get; }
    public ICommand CreateUserCommand { get; }
    public ICommand LoadRolesCommand { get; }

    public UserManagementViewModel(AuthService auth)
    {
        _auth = auth;
        LoadCommand = new Command(async () => await LoadAsync());
        LoadRolesCommand = new Command(async () => await LoadRolesAsync());
        CreateAdminCommand = new Command(async () => await CreateAdminAsync());
        CreateUserCommand = new Command(async () => await CreateUserAsync());
        _ = LoadAsync();
        _ = LoadRolesAsync();
    }

    private async Task LoadAsync()
    {
        var list = await _auth.GetUsersAsync();
        Users.Clear();
        foreach (var u in list) Users.Add(u);
    }

    private async Task LoadRolesAsync()
    {
        var list = await _auth.GetRolesAsync();
        Roles.Clear();
        foreach (var r in list) Roles.Add(r);
        // Optionally pre-select first role
        if (Roles.Count > 0 && SelectedRole == null) SelectedRole = Roles[0];
    }

    // quick helper to create admin for development/testing
    private async Task CreateAdminAsync()
    {
        if (Users.Any(u => u.Username == "admin"))
        {
            await Application.Current.MainPage.DisplayAlert("Info", "Admin already exists", "OK");
            return;
        }

        var adminRole = await _auth.GetRoleByNameAsync("Admin");
        if (adminRole == null)
        {
            adminRole = await _auth.CreateRoleAsync("Admin", "Administrator");
            await LoadRolesAsync();
        }

        var user = new User
        {
            Username = "admin",
            FullName = "Administrator",
            RoleId = adminRole.RoleId,
            IsActive = true
        };

        await _auth.CreateUserAsync(user, "admin123");
        await LoadAsync();
    }

    // create user from the UI
    private async Task CreateUserAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password) || SelectedRole == null)
        {
            await Application.Current.MainPage.DisplayAlert("Error", "Enter username, password and select role", "OK");
            return;
        }

        if (Users.Any(u => u.Username == Username.Trim()))
        {
            await Application.Current.MainPage.DisplayAlert("Error", "Username already exists", "OK");
            return;
        }

        var user = new User
        {
            Username = Username.Trim(),
            FullName = FullName?.Trim() ?? "",
            RoleId = SelectedRole.RoleId,
            IsActive = true
        };

        await _auth.CreateUserAsync(user, Password);
        // Clear inputs
        Username = "";
        FullName = "";
        Password = "";
        SelectedRole = Roles.FirstOrDefault();
        await LoadAsync();
        await Application.Current.MainPage.DisplayAlert("Success", "User created", "OK");
    }
}