using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseVisualizer.Models
{
    public class MissingIndex
    {
        public string DatabaseName { get; set; }
        public string SchemaName { get; set; }
        public string TableName { get; set; }
        public string EqualityColumns { get; set; } // Columns used for exact match (WHERE column = value)
        public string InequalityColumns { get; set; } // Columns used for ranges (WHERE column > value)
        public string IncludedColumns { get; set; } // Columns needed by the SELECT list
        public double AvgUserImpact { get; set; } // Estimated average percentage improvement 
    }
}
