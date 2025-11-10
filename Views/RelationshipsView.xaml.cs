// Views/RelationshipsView.xaml.cs

using System.Windows.Controls;
using DatabaseVisualizer.Models;
using DatabaseVisualizer.Services;
using System.Windows;
using System.Threading.Tasks;

namespace DatabaseVisualizer.Views
{
    public partial class RelationshipsView : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();
        private readonly DatabaseObject _selectedObject;

        private readonly List<DatabaseObject> _selectedObjects;
        private readonly bool _isMultiSelect; // To differentiate logic within the view

        // Modify the constructor to accept a list
        public RelationshipsView(List<DatabaseObject> selectedObjects, bool isMultiSelect)
        {
            InitializeComponent();
            _selectedObjects = selectedObjects; // Store the list
            _isMultiSelect = isMultiSelect;
            this.Loaded += RelationshipsView_Loaded;
        }

        // Add a default constructor for single-object or null calls
        public RelationshipsView(DatabaseObject selectedObject) : this(new List<DatabaseObject> { selectedObject }, false) { }
        public RelationshipsView() : this(null, false) { }

        private async void RelationshipsView_Loaded(object sender, RoutedEventArgs e)
        {
            // Note: _selectedObjects is the List<DatabaseObject> passed by the constructor

            if (_selectedObjects != null && _selectedObjects.Any())
            {
                // === CRITICAL FIX: Implement logic based on the flag ===
                if (_isMultiSelect && _selectedObjects.Count >= 2)
                {
                    string table1 = _selectedObjects[0].Name;
                    string table2 = _selectedObjects[1].Name;

                    // 1. Fetch Foreign Keys between the two tables
                    var foreignKeys = await Task.Run(() => _metadataService.GetForeignKeysBetween(table1, table2));
                    ForeignKeyGrid.ItemsSource = foreignKeys;

                    // 2. Hide single-table map elements
                    TableRelationshipGrid.ItemsSource = null;
                    TwoTableSelectionHint.Text = $"Showing direct FKs between {table1} and {table2}.";

                }
                else // Single-select logic
                {
                    // 1. Load Single Table Mapping (Existing Logic)
                    string table = _selectedObjects.First().Name;
                    var relationshipMap = await Task.Run(() => _metadataService.GetTableRelationships(table));
                    TableRelationshipGrid.ItemsSource = relationshipMap;

                    // 2. Ensure FK Grid is clear
                    ForeignKeyGrid.ItemsSource = null;
                    TwoTableSelectionHint.Text = "Select two tables to compare Foreign Keys.";
                }
            }
        }
    }
}