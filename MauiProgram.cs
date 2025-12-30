// MauiProgram.cs – đăng ký DbContext EF Core + Services + ViewModels + Dependency Injection
using HotelApp;
using HotelApp.Data;
using HotelApp.Services;
using HotelApp.ViewModels;
using HotelApp.Views;
using Microsoft.EntityFrameworkCore;
//using static Android.Icu.Text.IDNA;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder()
            .UseMauiApp<App>();

        // Đăng ký DbContext: EF Core sẽ dùng chuỗi kết nối này
        builder.Services.AddDbContextFactory<HotelDbContext>(options =>
            options.UseSqlServer(
                "Server=(localdb)\\Dev;Database=HotelManagement;Trusted_Connection=True;TrustServerCertificate=True;"));
        // Đăng ký services (truy cập dữ liệu)
        builder.Services.AddScoped<RoomService>();
        builder.Services.AddScoped<BookingService>();
        builder.Services.AddScoped<InvoiceService>();
        builder.Services.AddScoped<StayService>();
        // auth service
        builder.Services.AddSingleton<CurrentUserService>();
        builder.Services.AddScoped<AuthService>();

        // Đăng ký ViewModels
        builder.Services.AddTransient<RoomListViewModel>();
        builder.Services.AddTransient<RoomEditViewModel>();
        builder.Services.AddTransient<RoomDetailViewModel>();
        builder.Services.AddTransient<BookingViewModel>();
        builder.Services.AddTransient<InvoiceViewModel>();
        builder.Services.AddTransient<RoomUpdateViewModel>();

        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<UserManagementViewModel>();

        builder.Services.AddTransient<BookingDetailViewModel>();
       
        builder.Services.AddTransient<CheckOutViewModel>();

        // Đăng ký Pages (MAUI sẽ inject VM vào page constructor)
        builder.Services.AddTransient<RoomListPage>();
        builder.Services.AddTransient<RoomEditPage>();
        builder.Services.AddTransient<RoomDetailPage>();
        builder.Services.AddTransient<BookingPage>();
        builder.Services.AddTransient<InvoicePage>();
        builder.Services.AddTransient<RoomUpdatePage>();
        
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<UserManagementPage>();

        builder.Services.AddTransient<BookingManagementPage>();

        // Register booking detail page + viewmodel
        builder.Services.AddTransient<BookingDetailPage>();
        
        // Register CheckOutPage
        builder.Services.AddTransient<CheckOutPage>();

        return builder.Build();
    }
}
