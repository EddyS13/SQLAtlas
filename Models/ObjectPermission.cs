using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseVisualizer.Models
{
    public class ObjectPermission
    {
        public string PrincipalName { get; set; } = string.Empty;
        public string PermissionType { get; set; } = string.Empty;
        public string PermissionState { get; set; } = string.Empty;
    }
}
