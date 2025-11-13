using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseVisualizer.Models
{
    public class Dependency
    {
        public string Type { get; set; } = string.Empty;
        public string ObjectName { get; set; } = string.Empty;
    }
}
