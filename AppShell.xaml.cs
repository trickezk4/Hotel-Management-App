using HotelApp.Services;
using HotelApp.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Graphics;

namespace HotelApp
{
    public partial class AppShell : Shell
    {
        // use FlyoutItem references (matches XAML)
        private FlyoutItem? _loginItem;
        private FlyoutItem? _roomsItem;
        private FlyoutItem? _bookingsItem;
        private FlyoutItem? _usersItem;
        private FlyoutItem? _profileItem;

        private readonly CurrentUserService _current;
        private readonly AuthService _auth;

        public AppShell()
        {
            InitializeComponent();

            // Get FlyoutItem references (created from XAML)
            _loginItem = LoginItem;
            _roomsItem = RoomsItem;
            _bookingsItem = BookingsItem;
            _usersItem = UsersItem;
            _profileItem = ProfileItem;

            var services = (Application.Current?.Handler?.MauiContext?.Services)
                           ?? throw new InvalidOperationException("Unable to resolve services from MauiContext.");

            _current = services.GetRequiredService<CurrentUserService>();
            _auth = services.GetRequiredService<AuthService>();

            Routing.RegisterRoute("login", typeof(LoginPage));
            Routing.RegisterRoute("rooms", typeof(RoomListPage));
            Routing.RegisterRoute("bookings", typeof(BookingManagementPage));
            Routing.RegisterRoute("users", typeof(UserManagementPage));
            Routing.RegisterRoute("profile", typeof(ProfilePage));

            // Register detail/edit/update pages so Shell can resolve named routes with query parameters
            Routing.RegisterRoute(nameof(RoomDetailPage), typeof(RoomDetailPage));
            Routing.RegisterRoute(nameof(RoomEditPage), typeof(RoomEditPage));
            Routing.RegisterRoute(nameof(RoomUpdatePage), typeof(RoomUpdatePage));
            Routing.RegisterRoute(nameof(BookingDetailPage), typeof(BookingDetailPage));
            Routing.RegisterRoute(nameof(CheckOutPage), typeof(CheckOutPage));

            // Build initial menu based on current user (null on startup)
            BuildMenu(_current.User);

            // Subscribe to changes (login/logout) and rebuild menu dynamically
            _current.OnChanged += (u) => MainThread.BeginInvokeOnMainThread(() => BuildMenu(u));
        }

        // Build flyout items and footer (profile/change-password/logout) according to role
        private void BuildMenu(Models.User? user)
        {
            // Clear existing top-level items
            Items.Clear();

            // If no user signed in -> only login visible; clear footer
            if (user == null)
            {
                if (_loginItem != null) Items.Add(_loginItem);
                FlyoutFooter = null;
                return;
            }

            // User is signed in -> remove Login from menu (do not add _loginItem)
            var roleName = user.Role?.RoleName ?? string.Empty;

            // Receptionist + Admin: Bookings + Rooms
            if (roleName == "Receptionist" || roleName == "Admin")
            {
                if (_roomsItem != null) Items.Add(_roomsItem);
                if (_bookingsItem != null) Items.Add(_bookingsItem);
            }

            // Admin: access Users
            if (roleName == "Admin")
            {
                if (_usersItem != null) Items.Add(_usersItem);
            }

            // Optionally add Profile as a top-level item as well
            if (_profileItem != null)
            {
                Items.Add(_profileItem);
            }

            // Build FlyoutFooter with prominent Profile / Change password / Logout buttons
            // Nút đăng xuất
            var logoutButton = new Button
            {
                Text = "Đăng xuất",
                HorizontalOptions = LayoutOptions.Fill,
                BackgroundColor = Colors.DarkRed,
                TextColor = Colors.White,
                //FontAttributes = FontAttributes.Bold,
                CornerRadius = 6,
                HeightRequest = 44
            };
            logoutButton.Clicked += async (_, __) =>
            {
                try { await _auth.LogoutAsync(); } catch { }
                _current.User = null;
                try { await Shell.Current.GoToAsync("//login"); }
                catch { if (_loginItem != null) Shell.Current.CurrentItem = _loginItem; }
                FlyoutIsPresented = false;
            };

            FlyoutFooter = new VerticalStackLayout
            {
                Padding = new Thickness(12, 10),
                Spacing = 8,
                Children =
                {
                    logoutButton
                }
            };
        }
    }
}