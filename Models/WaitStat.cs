using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLAtlas.Models
{
    public class WaitStat
    {
        public string WaitType { get; set; } = string.Empty;
        public long WaitTimeSeconds { get; set; }
        public long WaitingTasksCount { get; set; }
    }
}
