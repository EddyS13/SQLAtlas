using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLAtlas.Models
{
    public class ExecutionPlan
    {
        public string QueryText { get; set; } = string.Empty; // The original query
        public string PlanXML { get; set; } = "Execute a query to see the plan..."; // The raw plan XML
        public string Summary { get; set; } = "N/A"; // Extracted bottleneck summary
    }
}
