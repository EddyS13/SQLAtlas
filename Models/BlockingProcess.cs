using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseVisualizer.Models
{
    public class BlockingProcess
    {
        public short SessionId { get; set; }
        public short BlockingSessionId { get; set; }
        public int WaitTimeMS { get; set; }
        public string WaitType { get; set; }
        public string BlockedCommand { get; set; }
    }
}
