using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLAtlas.Models
{
    public class RoleMembership
    {
        public string MemberName { get; set; } = "";
        public string MemberType { get; set; } = "";
        public string RoleName { get; set; } = "";
    }
}
