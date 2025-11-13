using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseVisualizer.Models
{
    public class IndexFragmentation
    {
        public string SchemaTableName { get; set; } = string.Empty; // Schema.TableName
        public string IndexName { get; set; } = string.Empty;
        public double FragmentationPercent { get; set; }
        public long PageCount { get; set; }
        public string MaintenanceAction { get; set; } = string.Empty; // REBUILD or REORGANIZE
        public string MaintenanceScript { get; set; } = string.Empty; // The DDL command (for clickable action)
    }
}
