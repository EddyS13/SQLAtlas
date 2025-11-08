using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseVisualizer.Models
{
    // Represents all tables related to a single selected table
    public class RelationshipMap
    {
        public string RelationshipType { get; set; } // e.g., "References" or "Referenced By"
        public string ConnectedTable { get; set; }
    }
}
