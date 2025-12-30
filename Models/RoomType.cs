using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HotelApp.Models
{
    public class RoomType
    {
        public int RoomTypeId { get; set; }
        public string Name { get; set; } = "Standard";
        public decimal BasePrice { get; set; }
        public int Capacity { get; set; }
        public string? Description { get; set; }

        public ICollection<Room> Rooms { get; set; } = new List<Room>();
    }
}
