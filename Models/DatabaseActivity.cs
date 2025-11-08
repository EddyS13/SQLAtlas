using System;

namespace DatabaseVisualizer.Models
{
    public class DatabaseActivity
    {
        public short SessionID { get; set; }
        public string LoginName { get; set; }
        public string HostName { get; set; }
        public string Status { get; set; }
        public string Command { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime LoginTime { get; set; }
    }
}
