using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLAtlas.Models
{
    public class ServerSetting
    {
        public string Name { get; set; } = string.Empty;
        public string CurrentValue { get; set; } = string.Empty; // The current value in memory (run_value)
        public string ConfigValue { get; set; } = string.Empty;  // The value saved to disk (config_value)
        public string Description { get; set; } = string.Empty;
        public string ConfigurationScript { get; set; } = string.Empty; // The SET/RECONFIGURE script
    }
}
