using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLAtlas.Models
{
    public class DriveSpaceInfo
    {
        public string DriveLetter { get; set; } = string.Empty;
        public decimal TotalCapacityGB { get; set; }
        public decimal FreeSpaceGB { get; set; }
        public decimal PercentFree { get; set; }
        public string DatabaseFiles { get; set; } = string.Empty; // List of databases residing on this drive
    }
}
