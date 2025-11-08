using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseVisualizer.Models
{
    public class LongRunningQuery
    {
        public short SessionId { get; set; }
        public string Status { get; set; }
        public int CommandDurationSeconds { get; set; }
        public string SqlStatement { get; set; }
    }
}
