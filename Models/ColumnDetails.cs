using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// Models/ColumnDetails.cs

namespace SQLAtlas.Models
{
    public class ColumnDetails
    {
        public string ColumnName { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public int MaxLength { get; set; }
        public bool IsNullable { get; set; }
    }
}
