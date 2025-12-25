using System;

namespace SQLAtlas.Models
{
    public class ServerInfoDetail
    {
        // Identification
        public string ServerName { get; set; } = "Unknown";
        public string Version { get; set; } = "N/A";
        public string Edition { get; set; } = "N/A";
        public string Level { get; set; } = "N/A"; // Service Pack / CU level

        // Hardware Specs
        public int CpuCount { get; set; }
        public int PhysicalRamGB { get; set; }

        // Time Tracking
        public DateTime StartTime { get; set; }

        // Helper property for the UI: Converts StartTime into "Up for X days..."
        public string UptimeDisplay
        {
            get
            {
                TimeSpan uptime = DateTime.Now - StartTime;

                if (uptime.TotalDays >= 1)
                    return $"Up for {uptime.Days}d, {uptime.Hours}h, {uptime.Minutes}m";

                return $"Up for {uptime.Hours}h, {uptime.Minutes}m";
            }
        }
    }
}