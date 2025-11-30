using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLAtlas.Models
{
    public class BackupHistory
    {
        public string DatabaseName { get; set; } = string.Empty;
        public DateTime BackupDate { get; set; }
        public string BackupType { get; set; } = string.Empty; // Full, Diff, Log
        public string UserName { get; set; } = string.Empty;
        public decimal SizeMB { get; set; }
        public string DeviceName { get; set; } = string.Empty;
    }
}
