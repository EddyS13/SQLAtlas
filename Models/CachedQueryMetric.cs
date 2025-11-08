using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseVisualizer.Models
{
    public class CachedQueryMetric
    {
        public long ExecutionCount { get; set; }
        public string TotalCPUTimeMS { get; set; }
        public string TotalLogicalReads { get; set; }
        public string AvgCPUTimeMS { get; set; }
        public string QueryStatement { get; set; }
    }
}
