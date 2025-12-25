using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLAtlas.Models
{
    public class IndexDetail
    {
        public string IndexName { get; set; } = string.Empty;
        public string IndexType { get; set; } = string.Empty; // CLUSTERED, NONCLUSTERED, XML, etc.
        public string KeyColumns { get; set; } = string.Empty;
        public string IncludedColumns { get; set; } = "N/A";

        public double FragmentationPercent { get; set; } = 0.0; // The quick health metric
        public long PageCount { get; set; } = 0; // The size metric (for relevance check)
    }
}
