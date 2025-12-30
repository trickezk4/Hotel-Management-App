using HotelApp.Data;
using HotelApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace HotelApp.Services;

// RoomService chịu trách nhiệm đọc/ghi dữ liệu liên quan tới phòng
public class RoomService
{
    private readonly IDbContextFactory<HotelDbContext> _dbFactory;
    public RoomService(IDbContextFactory<HotelDbContext> dbFactory) => _dbFactory = dbFactory;

    // Lấy danh sách phòng, có thể lọc theo type/status/text
    public async Task<List<Room>> GetRoomsAsync(int? roomTypeId = null, string? status = null, string? searchText = null)
    {
        await using var db = _dbFactory.CreateDbContext();
        // Khởi tạo query EF
        var q = db.Rooms.Include(r => r.RoomType).AsQueryable();

        // Lọc theo loại phòng nếu có
        if (roomTypeId.HasValue)
            q = q.Where(r => r.RoomTypeId == roomTypeId.Value);

        // Lọc theo trạng thái nếu có
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(r => r.Status == status);

        // Tìm kiếm theo số phòng nếu có
        if (!string.IsNullOrWhiteSpace(searchText))
            q = q.Where(r => r.RoomNumber.Contains(searchText));

        // AsNoTracking: chỉ đọc, tối ưu hiệu năng
        return await q.AsNoTracking().OrderBy(r => r.RoomNumber).ToListAsync();
    }

    public async Task<Room?> GetRoomAsync(int roomId)
    {
        await using var db = _dbFactory.CreateDbContext();
        // Include RoomType so bindings to Room.RoomType.* work
        return await db.Rooms
            .Include(r => r.RoomType)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RoomId == roomId);
    }

    // Lấy danh sách loại phòng (phục vụ UI)
    public async Task<List<RoomType>> GetRoomTypesAsync()
    {
        await using var db = _dbFactory.CreateDbContext();
        return await db.RoomTypes.AsNoTracking().OrderBy(rt => rt.Name).ToListAsync();
    }

    // Thêm phòng mới
    public async Task AddRoomAsync(Room room)
    {
        await using var db = _dbFactory.CreateDbContext();
        db.Rooms.Add(room);
        await db.SaveChangesAsync();
    }

    // Cập nhật phòng
    public async Task UpdateRoomAsync(Room room)
    {
        await using var db = _dbFactory.CreateDbContext();
        // Load the entity that is tracked by this DbContext and update its fields.
        var tracked = await db.Rooms.FindAsync(room.RoomId);
        if (tracked is null)
        {
            // If not found in this context, throw or choose to attach — here we throw to make the flow explicit.
            throw new InvalidOperationException($"Room with id {room.RoomId} not found in the current context.");
        }

        tracked.RoomNumber = room.RoomNumber;
        tracked.RoomTypeId = room.RoomTypeId;
        tracked.Floor = room.Floor;
        tracked.Status = room.Status;
        tracked.Notes = room.Notes;
        tracked.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
    }

    // Xóa phòng
    public async Task DeleteRoomAsync(int roomId)
    {
        await using var db = _dbFactory.CreateDbContext();
        var room = await db.Rooms.FindAsync(roomId);
        if (room is null) return;
        db.Rooms.Remove(room);
        await db.SaveChangesAsync();
    }

    //Kiểm tra số phòng theo tầng có tồn tại hay không
    public async Task<bool> ExistsRoomAsync(string roomNumber)
    {
        await using var db = _dbFactory.CreateDbContext();
        return await db.Rooms.AsNoTracking().AnyAsync(r => r.RoomNumber == roomNumber);
    }
}
