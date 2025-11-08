using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// Models/ColumnDetails.cs

namespace DatabaseVisualizer.Models
{
    public class ColumnDetails
    {
        public string ColumnName { get; set; }
        public string DataType { get; set; }
        public int MaxLength { get; set; }
        public bool IsNullable { get; set; }
    }
}
