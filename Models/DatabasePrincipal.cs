using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLAtlas.Models
{
    public class DatabasePrincipal
    {
        public string Name { get; set; } = "";
        public string TypeDescription { get; set; } = "";
        public DateTime CreateDate { get; set; }
        public string DefaultSchema { get; set; } = "";
        public string AuthenticationType { get; set; } = "";
    }
}
