using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLAtlas.Models
{
    public class DriveSpaceInfo
    {
        public string DriveLetter { get; set; } = "";
        public string VolumeName { get; set; } = "";
        public double FreeSpaceGB { get; set; }
        public double TotalSizeGB { get; set; }
        public string DatabaseFiles { get; set; } = ""; // The list of DB names
        public int DatabaseFileCount {  get; set; }
        public double PercentFull => TotalSizeGB > 0
            ? Math.Round(((TotalSizeGB - FreeSpaceGB) / TotalSizeGB) * 100, 1)
            : 0;
    }
}
