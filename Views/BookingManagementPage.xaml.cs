using HotelApp.ViewModels;
using HotelApp.Helpers;

namespace HotelApp.Views;

public partial class BookingManagementPage : ContentPage
{
    private readonly BookingViewModel? _vm;

    // DI constructor used at runtime
    public BookingManagementPage(BookingViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // If ViewModel is available, use ExecuteAsyncSafe to invoke commands.
        // Fallback: call methods directly if commands are not available.
        if (_vm is not null)
        {
            // Load bookings + rooms immediately when page appears
            if (_vm.LoadBookingsCommand != null)
                await _vm.LoadBookingsCommand.ExecuteAsyncSafe(null);
            else
                await _vm.LoadBookingsAsync();

            if (_vm.LoadRoomsCommand != null)
                await _vm.LoadRoomsCommand.ExecuteAsyncSafe(null);
            else
                await _vm.LoadRoomsAsync();
        }
        else if (BindingContext is BookingViewModel vm)
        {
            if (vm.LoadBookingsCommand != null)
                await vm.LoadBookingsCommand.ExecuteAsyncSafe(null);
            else
                await vm.LoadBookingsAsync();

            if (vm.LoadRoomsCommand != null)
                await vm.LoadRoomsCommand.ExecuteAsyncSafe(null);
            else
                await vm.LoadRoomsAsync();
        }
    }

    // Handler for the Reload button: play a short press animation and refresh bookings+rooms.
    private async void OnReloadClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn) return;

        try
        {
            btn.IsEnabled = false;

            // quick pressed animation
            try { await btn.ScaleTo(0.95, 80, Easing.CubicIn); } catch { }
            try { await btn.ScaleTo(1.0, 80, Easing.CubicOut); } catch { }

            // Refresh through ViewModel directly to ensure collections update immediately
            if (_vm != null)
            {
                await _vm.LoadBookingsAsync();
                await _vm.LoadRoomsAsync();
            }
            else if (BindingContext is BookingViewModel vm)
            {
                await vm.LoadBookingsAsync();
                await vm.LoadRoomsAsync();
            }
        }
        finally
        {
            btn.IsEnabled = true;
        }
    }
}