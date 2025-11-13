using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseVisualizer.Models
{
    public class MissingIndex
    {
        public string DatabaseName { get; set; } = string.Empty;
        public string SchemaName { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public string EqualityColumns { get; set; } = string.Empty;
        public string InequalityColumns { get; set; } = string.Empty;
        public string IncludedColumns { get; set; } = string.Empty;
        public double AvgUserImpact { get; set; }
    }
}
