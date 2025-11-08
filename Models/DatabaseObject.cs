// Models/DatabaseObject.cs

using System.Collections.ObjectModel;

namespace DatabaseVisualizer.Models
{
    public class DatabaseObject
    {
        public string Name { get; set; }
        public string Type { get; set; } // SQL Server type code (U, V, P, etc.)
        public string TypeDescription { get; set; } // Descriptive text (USER_TABLE, VIEW, etc.)
        public string SchemaName { get; set; }

        // This collection allows the object to be a parent node in the TreeView
        // We'll use this later for columns and relationships.
        public ObservableCollection<DatabaseObject> Children { get; set; } = new ObservableCollection<DatabaseObject>();

        // Optional: Icon or specific display properties could be added here later
    }
}