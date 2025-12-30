using System;
using HotelApp.Data;
using HotelApp.Models;
using Microsoft.EntityFrameworkCore;

namespace HotelApp.Services;

public class BookingService
{
    private readonly HotelDbContext _db;
    private readonly CurrentUserService _currentUser;
    public BookingService(HotelDbContext db, CurrentUserService currentUser) => (_db, _currentUser) = (db, currentUser);

    // Must match Customers.IdNumber NVARCHAR length in the database to avoid truncation.
    private const int MaxIdNumberLength = 30;

    // Kiểm tra phòng có trùng lịch trong khoảng thời gian
    public async Task<bool> IsRoomAvailableAsync(int roomId, DateTime from, DateTime to)
    {
        // Điều kiện trùng: (existing.CheckIn < to) && (existing.CheckOut > from)
        return !await _db.Bookings.AnyAsync(b => b.RoomId == roomId
            && b.BookingStatus != "Cancelled"
            && b.CheckInDate < to
            && b.CheckOutDate > from);
    }

    // Tạo booking mới (robust: ensure customer handled first, avoid unique IdNumber collisions)
    public async Task<int> CreateBookingAsync(Booking booking)
    {
        // Validate input
        if (booking == null) throw new ArgumentNullException(nameof(booking));
        if (booking.CheckOutDate <= booking.CheckInDate) throw new InvalidOperationException("Check-out must be after check-in.");

        // Double-check availability to avoid DB exception/race
        var available = await IsRoomAvailableAsync(booking.RoomId, booking.CheckInDate, booking.CheckOutDate);
        if (!available)
            throw new InvalidOperationException("Room is not available for the selected period.");

        // Handle Customer navigation: if caller provided a new Customer object, try to reuse existing one by IdNumber/Phone/FullName;
        // otherwise insert customer first so EF doesn't try to insert both customer+booking in one Save that may collide on unique IdNumber(NULL).
        if (booking.Customer != null && booking.Customer.CustomerId == 0)
        {
            var newCust = booking.Customer;

            Customer? found = null;

            // Normalize and truncate incoming IdNumber to avoid DB truncation errors during lookup/insert
            string? incomingId = string.IsNullOrWhiteSpace(newCust.IdNumber) ? null : newCust.IdNumber.Trim();
            if (incomingId != null && incomingId.Length > MaxIdNumberLength)
                incomingId = incomingId.Substring(0, MaxIdNumberLength);

            // Try to find by IdNumber (if provided and not whitespace)
            if (!string.IsNullOrWhiteSpace(incomingId))
            {
                found = await _db.Customers.FirstOrDefaultAsync(c => c.IdNumber == incomingId);
            }

            // Try by phone
            if (found == null && !string.IsNullOrWhiteSpace(newCust.Phone))
            {
                var phone = newCust.Phone.Trim();
                found = await _db.Customers.FirstOrDefaultAsync(c => c.Phone == phone);
            }

            // Try by full name as a last resort (may create false positives)
            if (found == null && !string.IsNullOrWhiteSpace(newCust.FullName))
            {
                var name = newCust.FullName.Trim();
                found = await _db.Customers.FirstOrDefaultAsync(c => c.FullName == name);
            }

            if (found != null)
            {
                // reuse existing customer
                booking.CustomerId = found.CustomerId;
                booking.Customer = null;
            }
            else
            {
                // If IdNumber is null/empty, assign a unique placeholder so we don't violate a UNIQUE(IdNumber) constraint
                if (string.IsNullOrWhiteSpace(incomingId))
                {
                    // Generate truncated TMP placeholder that fits the DB column
                    string tmp;
                    tmp = $"TMP-{Guid.NewGuid():N}";
                    if (tmp.Length > MaxIdNumberLength)
                        tmp = tmp.Substring(0, MaxIdNumberLength);
                    newCust.IdNumber = tmp;
                }
                else
                {
                    // Use the normalized/truncated incomingId for storage
                    newCust.IdNumber = incomingId;
                }

                // Set timestamps
                newCust.CreatedAt = DateTime.UtcNow;
                newCust.UpdatedAt = DateTime.UtcNow;

                // Add and save the customer first. Retry a few times if a UNIQUE collision occurs on the truncated TMP value.
                _db.Customers.Add(newCust);

                const int maxAttempts = 5;
                int attempt = 0;
                while (true)
                {
                    try
                    {
                        await _db.SaveChangesAsync();
                        break;
                    }
                    catch (DbUpdateException ex)
                    {
                        attempt++;
                        // If we tried multiple times or the error is not a unique-key collision on IdNumber, rethrow.
                        var inner = ex.InnerException?.Message ?? ex.Message;
                        bool likelyUniqueViolation = inner.IndexOf("duplicate", StringComparison.OrdinalIgnoreCase) >= 0
                                                     || inner.IndexOf("unique", StringComparison.OrdinalIgnoreCase) >= 0
                                                     || inner.IndexOf("IX_", StringComparison.OrdinalIgnoreCase) >= 0
                                                     || inner.IndexOf("UNIQUE", StringComparison.OrdinalIgnoreCase) >= 0
                                                     || inner.IndexOf("Violation of UNIQUE", StringComparison.OrdinalIgnoreCase) >= 0;

                        if (!likelyUniqueViolation || attempt >= maxAttempts)
                        {
                            var msg = ex.InnerException?.Message ?? ex.Message;
                            throw new InvalidOperationException("Failed to create customer for booking. Details: " + msg, ex);
                        }

                        // On likely unique collision (rare), regenerate TMP placeholder and retry.
                        if (string.IsNullOrWhiteSpace(newCust.IdNumber) || newCust.IdNumber.StartsWith("TMP-", StringComparison.OrdinalIgnoreCase))
                        {
                            var tmp = $"TMP-{Guid.NewGuid():N}";
                            if (tmp.Length > MaxIdNumberLength)
                                tmp = tmp.Substring(0, MaxIdNumberLength);
                            newCust.IdNumber = tmp;

                            // Replace the tracked entity in the change tracker so EF will attempt the updated value.
                            var entry = _db.Entry(newCust);
                            if (entry != null)
                                entry.State = EntityState.Added;

                            // loop to retry SaveChangesAsync
                            continue;
                        }

                        // Otherwise rethrow
                        var msg2 = ex.InnerException?.Message ?? ex.Message;
                        throw new InvalidOperationException("Failed to create customer for booking. Details: " + msg2, ex);
                    }
                }

                // Attach customer id to booking and clear navigation to avoid duplicate insert
                booking.CustomerId = newCust.CustomerId;
                booking.Customer = null;
            }
        }

        // Re-check availability right before inserting the booking to reduce race window
        var stillAvailable = await IsRoomAvailableAsync(booking.RoomId, booking.CheckInDate, booking.CheckOutDate);
        if (!stillAvailable)
            throw new InvalidOperationException("Room became unavailable while creating booking.");

        // Now add booking safely
        booking.CreatedAt = DateTime.UtcNow;
        booking.UpdatedAt = DateTime.UtcNow;
        _db.Bookings.Add(booking);

        // Update room status to "Booked" (unless already Occupied)
        var room = await _db.Rooms.FindAsync(booking.RoomId);
        if (room != null)
        {
            if (!string.Equals(room.Status, "Occupied", StringComparison.OrdinalIgnoreCase))
            {
                room.Status = "Booked";
                room.UpdatedAt = DateTime.UtcNow;
            }
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            throw new InvalidOperationException("Failed to create booking. Details: " + msg, ex);
        }

        return booking.BookingId;
    }

    // Lấy danh sách booking - return as NoTracking so callers always get fresh DB values (avoids stale tracked entities)
    public async Task<List<Booking>> GetBookingsAsync()
        => await _db.Bookings
            .AsNoTracking()
            .Include(b => b.Customer)
            .Include(b => b.Room).ThenInclude(r => r.RoomType)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

    // Lấy một booking - no tracking for fresh read
    public async Task<Booking?> GetBookingAsync(int bookingId)
        => await _db.Bookings
            .AsNoTracking()
            .Include(b => b.Customer)
            .Include(b => b.Room).ThenInclude(r => r.RoomType)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId);

    // Get active (non-cancelled) booking for a room (latest)
    public async Task<Booking?> GetActiveBookingForRoomAsync(int roomId)
        => await _db.Bookings
            .AsNoTracking()
            .Include(b => b.Customer)
            .Include(b => b.Room).ThenInclude(r => r.RoomType)
            .Where(b => b.RoomId == roomId && b.BookingStatus != "Cancelled")
            .OrderByDescending(b => b.CreatedAt)
            .FirstOrDefaultAsync();

    // Get latest booking for a room regardless of status (useful to show cancellation info)
    public async Task<Booking?> GetLatestBookingForRoomAsync(int roomId)
        => await _db.Bookings
            .AsNoTracking()
            .Include(b => b.Customer)
            .Include(b => b.Room).ThenInclude(r => r.RoomType)
            .Where(b => b.RoomId == roomId)
            .OrderByDescending(b => b.CreatedAt)
            .FirstOrDefaultAsync();

    // Cập nhật booking (change dates/room/status/deposit)
    public async Task UpdateBookingAsync(Booking updated)
    {
        var tracked = await _db.Bookings.FindAsync(updated.BookingId);
        if (tracked is null) throw new InvalidOperationException("Booking not found");

        // check availability if room/date changed
        if (tracked.RoomId != updated.RoomId || tracked.CheckInDate != updated.CheckInDate || tracked.CheckOutDate != updated.CheckOutDate)
        {
            var available = await IsRoomAvailableAsync(updated.RoomId, updated.CheckInDate, updated.CheckOutDate);
            if (!available)
                throw new InvalidOperationException("Room is not available for the selected period.");
        }

        tracked.RoomId = updated.RoomId;
        tracked.CheckInDate = updated.CheckInDate;
        tracked.CheckOutDate = updated.CheckOutDate;
        tracked.BookingStatus = updated.BookingStatus;
        tracked.DepositAmount = updated.DepositAmount;
        tracked.UpdatedAt = DateTime.UtcNow;

        // Optionally update simple customer info if navigation set
        if (updated.Customer != null && updated.Customer.CustomerId == 0)
        {
            // new embedded customer — add it and set customer id
            _db.Customers.Add(updated.Customer);
            await _db.SaveChangesAsync();
            tracked.CustomerId = updated.Customer.CustomerId;
        }

        await _db.SaveChangesAsync();
    }

    // Hủy đặt phòng
    public async Task CancelBookingAsync(int bookingId)
    {
        var tracked = await _db.Bookings.FindAsync(bookingId);
        if (tracked is null) return;

        // Prevent cancelling a booking that is already CheckedIn
        if (string.Equals(tracked.BookingStatus, "CheckedIn", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Cannot cancel a booking that has already been CheckedIn.");

        tracked.BookingStatus = "Cancelled";
        tracked.UpdatedAt = DateTime.UtcNow;

        // store cancellation metadata when available
        // prefer FullName, fall back to Username, then "System"
        var user = _currentUser?.User;
        if (user != null)
        {
            var by = string.IsNullOrWhiteSpace(user.FullName) ? user.Username : user.FullName;
            tracked.CancelledBy = string.IsNullOrWhiteSpace(by) ? "System" : by;
        }
        else
        {
            tracked.CancelledBy = "System";
        }

        // store UTC time so DB preserves universal time
        tracked.CancelledAt = DateTime.UtcNow;

        // If room is not occupied, try to return it to Available — but only if there are no other active bookings
        var room = await _db.Rooms.FindAsync(tracked.RoomId);
        if (room != null && !string.Equals(room.Status, "Occupied", StringComparison.OrdinalIgnoreCase))
        {
            var hasOtherActiveBookings = await _db.Bookings.AnyAsync(b => b.RoomId == room.RoomId && b.BookingId != bookingId && b.BookingStatus != "Cancelled");
            if (!hasOtherActiveBookings)
            {
                room.Status = "Available";
                room.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();
    }

    // Check-in từ booking: tạo Stay, cập nhật trạng thái phòng
    public async Task<int> CheckInBookingAsync(int bookingId)
    {
        var booking = await _db.Bookings
            .Include(b => b.Customer)
            .Include(b => b.Room)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId);

        if (booking is null) throw new InvalidOperationException("Booking not found");
        if (booking.BookingStatus == "Cancelled") throw new InvalidOperationException("Cannot check-in a cancelled booking.");

        // Create stay (DB Stays table does not have CreatedAt/UpdatedAt)
        var stay = new Stay
        {
            BookingId = booking.BookingId,
            CustomerId = booking.CustomerId,
            RoomId = booking.RoomId,
            ActualCheckIn = DateTime.UtcNow,
            StayStatus = "CheckedIn"
        };

        _db.Stays.Add(stay);

        // Update room status to Occupied
        var room = await _db.Rooms.FindAsync(booking.RoomId);
        if (room != null)
        {
            room.Status = "Occupied";
            room.UpdatedAt = DateTime.UtcNow;
        }

        // Update booking status
        booking.BookingStatus = "CheckedIn";
        booking.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return stay.StayId;
    }

    // Tính tổng tiền thuê dự kiến (dựa trên RoomType.BasePrice * nights)
    public async Task<decimal> CalculateEstimatedCostAsync(int roomId, DateTime from, DateTime to)
    {
        var room = await _db.Rooms.Include(r => r.RoomType).FirstOrDefaultAsync(r => r.RoomId == roomId);
        if (room is null) throw new InvalidOperationException("Room not found");
        var basePrice = room.RoomType?.BasePrice ?? 0m;

        var nights = (to.Date - from.Date).TotalDays;
        if (nights < 1) nights = 1;
        var total = basePrice * (decimal)nights;
        return Math.Round(total, 2);
    }

    // Return the active Stay for a booking (StayStatus == "CheckedIn")
    public async Task<Stay?> GetActiveStayForBookingAsync(int bookingId)
    {
        return await _db.Stays
            .Include(s => s.Room)
            .Include(s => s.Booking)
            .FirstOrDefaultAsync(s => s.BookingId == bookingId && s.StayStatus == "CheckedIn");
    }

    // Perform check-out for a booking: set ActualCheckOut, mark stay as CheckedOut, update room & booking
    // Returns stayId (created at check-in), or throws if no active stay found
    public async Task<int> CheckOutBookingAsync(int bookingId, string? notes = null)
    {
        // Find active stay for this booking
        var stay = await _db.Stays
            .Include(s => s.Booking)
            .Include(s => s.Room)
            .FirstOrDefaultAsync(s => s.BookingId == bookingId && s.StayStatus == "CheckedIn");

        if (stay is null)
            throw new InvalidOperationException("Không tìm thấy dữ liêu check-in.");

        // Use the booking's planned CheckOutDate (if available) to compute nights consistently with preview.
        // Fallback to current time only if booking.CheckOutDate is not set.
        DateTime resolvedCheckOut;
        if (stay.Booking != null && stay.Booking.CheckOutDate != default)
            resolvedCheckOut = stay.Booking.CheckOutDate;
        else
            resolvedCheckOut = DateTime.UtcNow;

        // Set actual checkout and status
        stay.ActualCheckOut = resolvedCheckOut;
        stay.StayStatus = "CheckedOut";
        if (!string.IsNullOrWhiteSpace(notes))
        {
            stay.Notes = (stay.Notes ?? "") + "\n" + notes;
        }

        // Update booking status to CheckedOut
        if (stay.Booking != null)
        {
            stay.Booking.BookingStatus = "CheckedOut";
            stay.Booking.UpdatedAt = DateTime.UtcNow;
        }

        // Update room status to Available
        var room = await _db.Rooms.FindAsync(stay.RoomId);
        if (room != null)
        {
            // After a successful check-out we make the room available.
            room.Status = "Available";
            room.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return stay.StayId;
    }

    // Get Stay for a booking regardless of stay status (CheckedIn / CheckedOut)
    public async Task<Stay?> GetStayForBookingAsync(int bookingId)
    {
        return await _db.Stays
            .Include(s => s.Room).ThenInclude(r => r.RoomType)
            .Include(s => s.ServiceUsages).ThenInclude(su => su.Service)
            .Include(s => s.Booking)
            .FirstOrDefaultAsync(s => s.BookingId == bookingId);
    }
}