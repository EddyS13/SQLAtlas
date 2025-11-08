using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseVisualizer.Models
{
    public class IndexFragmentation
    {
        public string TableName { get; set; }
        public string IndexName { get; set; }
        public double FragmentationPercent { get; set; }
        public long PageCount { get; set; }
        public string MaintenanceAction { get; set; } // Rebuild or Reorganize
    }
}
