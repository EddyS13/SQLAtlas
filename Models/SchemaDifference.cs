using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLAtlas.Models
{
    public class SchemaDifference
    {
        public string ObjectType { get; set; } = string.Empty; // Table, View, SP
        public string ObjectName { get; set; } = string.Empty;
        public string ColumnName { get; set; } = string.Empty; // <<< CRITICAL MISSING FIELD
        public string DataType { get; set; } = string.Empty;     // <<< CRITICAL MISSING FIELD
        public string DifferenceType { get; set; } = string.Empty; // Missing in Target, Different Definition
        public string SynchronizationScript { get; set; } = string.Empty; // The DDL required to fix it
    }
}
