using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLAtlas.Models
{
    public class BlockingProcess
    {
        public int Spid { get; set; }
        public int BlockingSpid { get; set; }
        public long WaitTimeMs { get; set; }
        public int BlockLevel { get; set; }
        public string QueryText { get; set; } = "";
    }
}
