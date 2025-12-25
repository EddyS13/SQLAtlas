using System;
using System.Windows.Media;

namespace SQLAtlas.Models
{
    public class SqlAgentJob
    {
        public string JobName { get; set; } = "";
        public bool IsEnabled { get; set; }
        public string LastRunOutcome { get; set; } = "";
        public DateTime? LastRunDate { get; set; }
        public DateTime? NextRunDate { get; set; }
        public string LastRunDuration { get; set; } = "";

        // Keep the text helper
        public string IsEnabledDisplay => IsEnabled ? "YES" : "NO";
    }
}