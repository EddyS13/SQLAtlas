// Models/DatabaseObject.cs

using System.Collections.ObjectModel;

namespace SQLAtlas.Models
{
    public class DatabaseObject
    {
        public string Name { get; set; } = string.Empty; // <<< Fix CS8618
        public string Type { get; set; } = string.Empty;
        public string TypeDescription { get; set; } = string.Empty;
        public string SchemaName { get; set; } = string.Empty;
        public ObservableCollection<DatabaseObject> Children { get; set; } = new ObservableCollection<DatabaseObject>();

        /// <summary>
        /// Returns the display name for the schema explorer tree view.
        /// </summary>
        public override string ToString()
        {
            return Name;
        }
    }
}