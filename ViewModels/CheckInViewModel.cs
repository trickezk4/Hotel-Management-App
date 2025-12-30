// ViewModels/CheckInViewModel.cs
using System.Collections.ObjectModel;
using System.Windows.Input;
using HotelApp.Models;
using HotelApp.Services;

namespace HotelApp.ViewModels;

public class CheckInViewModel
{
    private readonly StayService _stayService;

    public ObservableCollection<Stay> Stays { get; } = new();
    public string? SearchText { get; set; }

    public ICommand RefreshCommand { get; }
    public ICommand CheckOutCommand { get; }

    public CheckInViewModel(StayService stayService)
    {
        _stayService = stayService;
        RefreshCommand = new Command(async () => await LoadStaysAsync());
        CheckOutCommand = new Command<Stay>(async s => await CheckoutAsync(s));
        Task.Run(async () => await LoadStaysAsync());
    }

    private async Task LoadStaysAsync()
    {
        var list = await _stayService.GetActiveStaysAsync();
        // Filter đơn giản theo SearchText
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            list = list.Where(s =>
                (s.Customer.FullName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (s.Room.RoomNumber?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (s.Customer.Phone?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
            ).ToList();
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Stays.Clear();
            foreach (var s in list) Stays.Add(s);
        });
    }

    private async Task CheckoutAsync(Stay? stay)
    {
        if (stay is null) return;
        await _stayService.CheckoutAsync(stay.StayId);
        await Application.Current.MainPage.DisplayAlert("Thành công", "Đã trả phòng.", "OK");
        await LoadStaysAsync();
    }
}
