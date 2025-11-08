using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseVisualizer.Models
{
    public class ObjectPermission
    {
        public string PrincipalName { get; set; } // User or Role name
        public string PermissionType { get; set; } // SELECT, EXECUTE, CONTROL, etc.
        public string PermissionState { get; set; } // GRANT, DENY, or REVOKE
    }
}
