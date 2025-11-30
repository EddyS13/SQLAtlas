using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLAtlas.Models
{
    public class SqlAgentJob
    {
        public string JobName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string NextRunDate { get; set; } = string.Empty;
        public string NextRunTime { get; set; } = string.Empty;
    }
}
