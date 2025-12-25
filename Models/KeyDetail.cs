using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLAtlas.Models
{
    public class KeyDetail
    {
        public string ConstraintName { get; set; } = string.Empty;
        public string KeyType { get; set; } = string.Empty; // e.g., PRIMARY KEY, FOREIGN KEY, UNIQUE
        public string Columns { get; set; } = string.Empty; // Column(s) in the current table
        public string ReferencesTable { get; set; } = string.Empty; // Only for FKs
    }
}
