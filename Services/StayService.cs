using HotelApp.Data;
using HotelApp.Models;
using Microsoft.EntityFrameworkCore;

namespace HotelApp.Services;

public class StayService
{
    private readonly HotelDbContext _db;
    public StayService(HotelDbContext db) => _db = db;

    // Tạo phiên lưu trú từ booking
    public async Task<int> CreateStayFromBookingAsync(int bookingId)
    {
        var booking = await _db.Bookings.Include(b => b.Customer).FirstOrDefaultAsync(b => b.BookingId == bookingId);
        if (booking is null) throw new InvalidOperationException("Booking không tồn tại.");

        var stay = new Stay
        {
            BookingId = bookingId,
            CustomerId = booking.CustomerId,
            RoomId = booking.RoomId,
            ActualCheckIn = DateTime.Now,
            StayStatus = "CheckedIn"
        };

        _db.Stays.Add(stay);
        await _db.SaveChangesAsync();
        return stay.StayId;
    }

    // Check-out: cập nhật thời gian và trạng thái
    public async Task CheckoutAsync(int stayId)
    {
        var stay = await _db.Stays.FindAsync(stayId);
        if (stay is null) throw new InvalidOperationException("Stay không tồn tại.");
        stay.ActualCheckOut = DateTime.Now;
        stay.StayStatus = "CheckedOut";
        await _db.SaveChangesAsync();
    }

    // Danh sách lưu trú đang hoạt động
    public async Task<List<Stay>> GetActiveStaysAsync()
        => await _db.Stays.Include(s => s.Customer).Include(s => s.Room)
            .Where(s => s.StayStatus == "CheckedIn")
            .OrderBy(s => s.ActualCheckIn).ToListAsync();
}
