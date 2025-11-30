using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLAtlas.Models
{
    public class DatabasePrincipal
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // SQL_USER, WINDOWS_GROUP, etc.
        public string RoleMembership { get; set; } = string.Empty; // e.g., db_datareader, db_owner
    }
}
