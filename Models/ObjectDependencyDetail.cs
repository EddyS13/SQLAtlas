using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLAtlas.Models
{
    public class ObjectDependencyDetail
    {
        public string ObjectName { get; set; } = string.Empty;
        public string ObjectType { get; set; } = string.Empty;

        /// <summary>
        /// Example: READ Only, MIXED Access, UNKNOWN
        /// </summary>
        public string ReferenceType { get; set; } = string.Empty;

        /// <summary>
        /// Example: READ or N/A
        /// </summary>
        public string ReadStatus { get; set; } = string.Empty;

        /// <summary>
        /// Example: Possible I/U/D or N/A
        /// </summary>
        public string WriteStatus { get; set; } = string.Empty;
    }
}
