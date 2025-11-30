using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLAtlas.Models
{
    public class AvailabilityReplicaStatus
    {
        public string InstanceName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty; // Primary or Secondary
        public string SynchronizationHealth { get; set; } = string.Empty; // HEALTHY, WARNING, CRITICAL
        public string SynchronizationState { get; set; } = string.Empty; // SYNCHRONIZED, SYNCHRONIZING
        public long LogSendQueueKB { get; set; } // Measures latency
        public long RedoQueueKB { get; set; } // Measures secondary lag
    }
}
