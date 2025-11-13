using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseVisualizer.Models
{
    public class DatabaseSpaceInfo
    {
        public string FileGroupName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public string CurrentSizeMB { get; set; } = string.Empty;
        public string AvailableFreeSpaceMB { get; set; } = string.Empty;
        public string MaxSize { get; set; } = string.Empty;
    }
}
