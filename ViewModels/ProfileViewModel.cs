using HotelApp.Models;
using HotelApp.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using System.Threading;
using System.Windows.Input;

namespace HotelApp.ViewModels;

public class ProfileViewModel : BaseViewModel
{
    private readonly AuthService _auth;
    private readonly CurrentUserService _current;

    public User? CurrentUser => _current.User;

    public string CurrentPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";

    public ICommand SaveProfileCommand { get; }
    public ICommand ChangePasswordCommand { get; }

    // Semaphore to serialize navigation calls and avoid "Pending Navigations" errors
    private static readonly SemaphoreSlim _navLock = new SemaphoreSlim(1, 1);

    public ProfileViewModel(AuthService auth, CurrentUserService current)
    {
        _auth = auth;
        _current = current;
        SaveProfileCommand = new Command(async () => await SaveAsync());
        ChangePasswordCommand = new Command(async () => await ChangePasswordAsync());
    }

    private async Task SaveAsync()
    {
        if (CurrentUser == null)
        {
            await Application.Current.MainPage.DisplayAlert("Error", "No user signed in.", "OK");
            return;
        }

        try
        {
            await _auth.UpdateProfileAsync(CurrentUser);
            await Application.Current.MainPage.DisplayAlert("Success", "Profile saved.", "OK");

            // Schedule navigation on UI thread after a short delay so AppShell finishes rebuilding menu
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                // small delay to allow AppShell to process OnChanged & rebuild items
                await Task.Delay(120);

                await _navLock.WaitAsync();
                try
                {
                    await Shell.Current.GoToAsync("//rooms");
                }
                catch (Exception ex)
                {
                    // optional: retry once if pending navigation still occurs
                    try
                    {
                        await Task.Delay(200);
                        await Shell.Current.GoToAsync("//rooms");
                    }
                    catch
                    {
                        await Application.Current.MainPage.DisplayAlert("Navigation error", ex.Message, "OK");
                    }
                }
                finally
                {
                    _navLock.Release();
                }
            });
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("Error", $"Failed to save profile: {ex.Message}", "OK");
        }
    }

    private async Task ChangePasswordAsync()
    {
        if (CurrentUser == null)
        {
            await Application.Current.MainPage.DisplayAlert("Error", "No user signed in.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(CurrentPassword) || string.IsNullOrWhiteSpace(NewPassword))
        {
            await Application.Current.MainPage.DisplayAlert("Error", "Provide current and new password", "OK");
            return;
        }

        try
        {
            var ok = await _auth.ChangePasswordAsync(CurrentUser.UserId, CurrentPassword, NewPassword);
            if (!ok)
            {
                await Application.Current.MainPage.DisplayAlert("Error", "Current password invalid", "OK");
                CurrentPassword = "";
                OnPropertyChanged(nameof(CurrentPassword));
                return;
            }

            // Clear sensitive fields
            CurrentPassword = NewPassword = "";
            OnPropertyChanged(nameof(CurrentPassword));
            OnPropertyChanged(nameof(NewPassword));

            await Application.Current.MainPage.DisplayAlert("Success", "Password changed. Please sign in again.", "OK");

            // Logout and clear session first
            try
            {
                await _auth.LogoutAsync();
            }
            catch
            {
                // ignore logout errors
            }

            // Trigger menu rebuild
            _current.User = null;

            // Schedule navigation to login on UI thread after Shell/menu rebuild completes
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                // small delay to allow AppShell to rebuild flyout items
                await Task.Delay(120);

                await _navLock.WaitAsync();
                try
                {
                    await Shell.Current.GoToAsync("//login");
                }
                catch (Exception ex)
                {
                    // optional retry
                    try
                    {
                        await Task.Delay(200);
                        await Shell.Current.GoToAsync("//login");
                    }
                    catch
                    {
                        await Application.Current.MainPage.DisplayAlert("Navigation error", ex.Message, "OK");
                    }
                }
                finally
                {
                    _navLock.Release();
                }
            });
        }
        catch (Exception ex)
        {
            // Clear sensitive fields on failure
            CurrentPassword = NewPassword = "";
            OnPropertyChanged(nameof(CurrentPassword));
            OnPropertyChanged(nameof(NewPassword));
            await Application.Current.MainPage.DisplayAlert("Error", $"Failed to change password: {ex.Message}", "OK");
        }
    }
}