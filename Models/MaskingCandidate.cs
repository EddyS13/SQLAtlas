using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseVisualizer.Models
{
    public class MaskingCandidate
    {
        public string SchemaTableName { get; set; } = string.Empty;
        public string ColumnName { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public bool IsMasked { get; set; }
        public string MaskingFunction { get; set; } = string.Empty;
        public string MaskingScript { get; set; } = string.Empty; // DDL script to apply/remove mask
    }
}
