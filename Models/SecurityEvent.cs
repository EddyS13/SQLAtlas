using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseVisualizer.Models
{
    public class SecurityEvent
    {
        public DateTime EventTime { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string LoginName { get; set; } = string.Empty;
        public string Success { get; set; } = string.Empty;
        public string ClientHost { get; set; } = string.Empty;
    }
}
