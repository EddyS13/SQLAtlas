using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLAtlas.Models
{
    public class MaskingCandidate
    {
        public string SchemaName { get; set; } = "";
        public string TableName { get; set; } = "";
        public string ColumnName { get; set; } = "";
        public string DataType { get; set; } = "";
        public bool IsMasked { get; set; }
        public string MaskingFunction { get; set; } = "";

        public string SchemaTableName => $"{SchemaName}.{TableName}";

        public string SuggestedMask
        {
            get
            {
                string col = ColumnName.ToLower();

                // Specialized mask for SSN patterns
                if (col.Contains("sec_no"))
                    return "partial(0, \"XXX-XX-\", 4)";

                // Standard mask for passwords, credit cards, and phones
                return "default()";
            }
        }
    }
}
