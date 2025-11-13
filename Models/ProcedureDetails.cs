using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseVisualizer.Models
{
    public class ProcedureDetails
    {
        public string Definition { get; set; } = string.Empty;
        public List<ProcedureParameter> Parameters { get; set; } = new List<ProcedureParameter>();

        // New properties
        public DateTime CreateDate { get; set; }
        public DateTime ModifyDate { get; set; }
    }
}
