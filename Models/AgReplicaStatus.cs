using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLAtlas.Models
{
    public class AgReplicaStatus
    {
        public string AGName { get; set; } = "";
        public string ReplicaServerName { get; set; } = "";
        public string Role { get; set; } = "";
        public string FailoverMode { get; set; } = "";
        public string SynchronizationState { get; set; } = "";
        public string OperationalState { get; set; } = "";
    }
}
