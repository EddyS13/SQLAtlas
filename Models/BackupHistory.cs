using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLAtlas.Models
{
    public class BackupHistory
    {
        public string DatabaseName { get; set; } = "";
        public string BackupType { get; set; } = "";

        // Error Fix: Added BackupDate
        public DateTime BackupDate { get; set; }

        // Error Fix: Added UserName
        public string UserName { get; set; } = "";

        // Error Fix: Matches the decimal coming from SQL query
        public decimal SizeMB { get; set; }

        public string DeviceName { get; set; } = "";

        // UI Logic
        public bool IsStale => BackupDate < DateTime.Now.AddHours(-24);
    }
}
