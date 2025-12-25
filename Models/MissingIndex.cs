using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLAtlas.Models
{
    public class MissingIndex
    {
        public string TableName { get; set; } = "";
        public double Impact { get; set; }
        public string EqualityColumns { get; set; } = "";
        public string InequalityColumns { get; set; } = "";
        public string IncludedColumns { get; set; } = "";
        public string CreateScript { get; set; } = "";

        // This property drives the XAML status pills
        public string ImpactScore
        {
            get
            {
                if (Impact >= 70) return "High";
                if (Impact >= 30) return "Medium";
                return "Low";
            }
        }
    }
}
