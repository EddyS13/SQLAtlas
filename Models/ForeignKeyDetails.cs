using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// Models/ForeignKeyDetails.cs

namespace DatabaseVisualizer.Models
{
    // Represents a single foreign key constraint link between two columns/tables
    public class ForeignKeyDetails
    {
        public string ConstraintName { get; set; } = string.Empty;
        public string ParentTable { get; set; } = string.Empty;
        public string ParentColumn { get; set; } = string.Empty;
        public string ReferencedTable { get; set; } = string.Empty;
        public string ReferencedColumn { get; set; } = string.Empty;
    }
}
