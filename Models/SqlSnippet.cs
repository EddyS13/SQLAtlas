using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLAtlas.Models
{
    public class SqlSnippet
    {
        public string Title { get; set; } = string.Empty; // e.g., "Find Blocking Sessions"
        public string Category { get; set; } = string.Empty; // e.g., "Monitoring", "Security"
        public string Description { get; set; } = string.Empty; // Detailed explanation of what the script does
        public string Code { get; set; } = string.Empty; // The actual T-SQL code
    }
}
