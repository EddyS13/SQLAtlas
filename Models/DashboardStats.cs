using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLAtlas.Models
{
    public class DashboardStats
    {
        public string Uptime { get; set; } = "N/A";
        public int CpuUsage { get; set; }
        public int BlockingSessions { get; set; }
        public int FailedLogins { get; set; }
        public int OnlineDatabases { get; set; }
        public int OfflineDatabases { get; set; }
        public string LastUpdateDisplay { get; set; } = "";

        // This is the missing piece causing your error:
        public List<HealthCheckItem> HealthChecks { get; set; } = new List<HealthCheckItem>();
    }

    public class HealthCheckItem
    {
        public string Message { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusColor { get; set; } = "#FFFFFF"; // Default White
    }
}