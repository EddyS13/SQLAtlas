using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseVisualizer.Models
{
    public class DatabaseSpaceInfo
    {
        public string FileGroupName { get; set; }
        public string FileName { get; set; }
        public string FileType { get; set; } // DATA or LOG
        public string CurrentSizeMB { get; set; }
        public string AvailableFreeSpaceMB { get; set; }
        public string MaxSize { get; set; } // Max growth limit
    }
}
