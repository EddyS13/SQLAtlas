using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLAtlas.Models
{
    public class ExpensiveQuery
    {
        public long ExecutionCount { get; set; }
        public long AvgCpuTime { get; set; }
        public long AvgLogicalReads { get; set; }
        public long AvgIo { get; set; }
        public DateTime LastExecutionTime { get; set; }
        public string QueryText { get; set; } = "";

        // This property removes the annoying leading spaces/tabs from the SQL engine
        public string CleanQueryText
        {
            get
            {
                if (string.IsNullOrWhiteSpace(QueryText)) return "";
                // Remove leading newlines/spaces and replace multiple spaces with one
                return QueryText.TrimStart().Replace("\r", "").Replace("\n", " ").Trim();
            }
        }
    }
}