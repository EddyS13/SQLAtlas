using System;

namespace SQLAtlas.Models
{
    public class ServerInfoDetail
    {
        public string ServerName { get; set; } = "";
        public string Version { get; set; } = "";
        public string Edition { get; set; } = "";
        public string Level { get; set; } = "";
        public int CpuCount { get; set; }
        public double PhysicalRamGB { get; set; }
        public string Collation { get; set; } = "";
        public string OSVersion { get; set; } = "";
        public string Hypervisor { get; set; } = "";

        // 1. Add StartTime so your logic has data to work with
        public DateTime StartTime { get; set; }

        // 2. Your custom logic (This replaces the old {get; set;} version)
        public string UptimeDisplay
        {
            get
            {
                if (StartTime == DateTime.MinValue) return "N/A";
                TimeSpan uptime = DateTime.Now - StartTime;

                if (uptime.TotalDays >= 1)
                    return $"Up for {uptime.Days}d, {uptime.Hours}h, {uptime.Minutes}m";

                return $"Up for {uptime.Hours}h, {uptime.Minutes}m";
            }
        }
    }
}