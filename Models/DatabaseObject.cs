// Models/DatabaseObject.cs

using System.Collections.ObjectModel;

namespace DatabaseVisualizer.Models
{
    public class DatabaseObject
    {
        public string Name { get; set; } = string.Empty; // <<< Fix CS8618
        public string Type { get; set; } = string.Empty;
        public string TypeDescription { get; set; } = string.Empty;
        public string SchemaName { get; set; } = string.Empty;
        public ObservableCollection<DatabaseObject> Children { get; set; } = new ObservableCollection<DatabaseObject>();
    }
}