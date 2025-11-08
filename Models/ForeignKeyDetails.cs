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
        public string ConstraintName { get; set; }
        public string ParentTable { get; set; }
        public string ParentColumn { get; set; }
        public string ReferencedTable { get; set; }
        public string ReferencedColumn { get; set; }
    }
}
