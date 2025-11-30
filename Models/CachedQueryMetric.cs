using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLAtlas.Models
{
    public class CachedQueryMetric
    {
        public long ExecutionCount { get; set; }
        public string TotalCPUTimeMS { get; set; } = string.Empty;
        public string TotalLogicalReads { get; set; } = string.Empty;
        public string AvgCPUTimeMS { get; set; } = string.Empty;
        public string QueryStatement { get; set; } = string.Empty;
    }
}
