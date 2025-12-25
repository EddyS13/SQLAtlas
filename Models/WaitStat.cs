using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLAtlas.Models
{
    public class WaitStat
    {
        public string WaitType { get; set; } = "";
        public double WaitTimeS { get; set; }
        public double Percentage { get; set; }
        public double AvgWaitMs { get; set; }
        public string Description { get; set; } = "";
    }
}
