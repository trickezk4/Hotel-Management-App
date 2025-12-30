using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HotelApp.Models
{
    // Entity đại diện bảng Rooms trong DB
    public class Room
    {
        public int RoomId { get; set; }                  // Khóa chính
        public string RoomNumber { get; set; } = "";     // Số phòng (unique)
        public int RoomTypeId { get; set; }              // FK tới RoomTypes
        public int Floor { get; set; }                   // Tầng
        public string Status { get; set; } = "Available";// Trạng thái phòng
        public string? Notes { get; set; }               // Ghi chú
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navigation: không tạo cột, dùng để load liên kết
        public RoomType? RoomType { get; set; }
        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
        public ICollection<Stay> Stays { get; set; } = new List<Stay>();
    }
}
