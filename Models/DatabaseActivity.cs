using System;

namespace DatabaseVisualizer.Models
{
    public class DatabaseActivity
    {
        public short SessionID { get; set; }
        public string LoginName { get; set; } = string.Empty;
        public string HostName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime LoginTime { get; set; }
    }
}
