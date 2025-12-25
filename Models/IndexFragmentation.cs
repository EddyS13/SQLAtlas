using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLAtlas.Models
{
    public class IndexFragmentation
    {
        public string SchemaTableName { get; set; } = "";
        public string IndexName { get; set; } = "";
        public double FragmentationPercent { get; set; }
        public long PageCount { get; set; }

        // Logic for recommendations
        public string MaintenanceAction
        {
            get
            {
                if (PageCount < 8) return "HEALTHY";
                if (FragmentationPercent > 30) return "REBUILD";
                if (FragmentationPercent > 5) return "REORGANIZE";
                return "HEALTHY";
            }
        }

        public string MaintenanceScript
        {
            get
            {
                if (MaintenanceAction == "REBUILD")
                    return $"ALTER INDEX [{IndexName}] ON {SchemaTableName} REBUILD WITH (ONLINE = ON);";
                if (MaintenanceAction == "REORGANIZE")
                    return $"ALTER INDEX [{IndexName}] ON {SchemaTableName} REORGANIZE;";
                return "";
            }
        }
    }
}
