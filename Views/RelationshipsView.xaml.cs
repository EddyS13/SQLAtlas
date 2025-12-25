// Views/RelationshipsView.xaml.cs

using System.Windows.Controls;
using SQLAtlas.Models;
using SQLAtlas.Services;
using System.Windows;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq; // Required for .Any() and .First()

namespace SQLAtlas.Views
{
    public partial class RelationshipsView : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();

        // Field to hold the selected object(s). It is nullable.
        private readonly List<DatabaseObject>? _selectedObjects;
        private readonly bool _isMultiSelect; // Flag to differentiate between single map and FK comparison

        /// <summary>
        /// Parameterless Constructor (For routing safety).
        /// </summary>
        public RelationshipsView() : this((DatabaseObject?)null, isMultiSelect: false) { }

        /// <summary>
        /// Constructor for single-object selection (Table/View).
        /// </summary>
        public RelationshipsView(DatabaseObject? selectedObject, bool isMultiSelect)
        {
            InitializeComponent();

            this.Loaded += RelationshipsView_Loaded;

            // Check for null and convert single object to a list
            if (selectedObject is not null)
            {
                _selectedObjects = new List<DatabaseObject> { selectedObject };
            }
            else
            {
                _selectedObjects = null;
            }
            _isMultiSelect = isMultiSelect;
        }

        /// <summary>
        /// Constructor for multi-select routing (Accepts a list of tables for comparison).
        /// </summary>
        public RelationshipsView(List<DatabaseObject>? selectedObjects, bool isMultiSelect)
        {
            InitializeComponent();
            _selectedObjects = selectedObjects;
            _isMultiSelect = isMultiSelect;
            this.Loaded += RelationshipsView_Loaded;
        }

        private async void RelationshipsView_Loaded(object sender, RoutedEventArgs e)
        {
            // Reset state
            TableRelationshipGrid.ItemsSource = null;
            ForeignKeyGrid.ItemsSource = null;

            if (_selectedObjects is not null && _selectedObjects.Any())
            {
                // === CRITICAL FIX: Implement logic based on the flag ===
                if (_isMultiSelect && _selectedObjects.Count >= 2)
                {
                    // 1. Multi-Select: Fetch Foreign Keys between the first two tables
                    string table1 = _selectedObjects[0].Name;
                    string table2 = _selectedObjects[1].Name;

                    var foreignKeys = await Task.Run(() => _metadataService.GetForeignKeysBetween(table1, table2));
                    ForeignKeyGrid.ItemsSource = foreignKeys;

                    TwoTableSelectionHint.Text = $"Showing direct FKs between {table1} and {table2}.";
                }
                else // Single-select logic
                {
                    // 2. Single-Select: Fetch Relationship Map for the first (and only) table
                    string table = _selectedObjects.First().Name;

                    var relationshipMap = await Task.Run(() => _metadataService.GetTableRelationships(table));
                    TableRelationshipGrid.ItemsSource = relationshipMap;

                    TwoTableSelectionHint.Text = "Select two tables to compare Foreign Keys.";
                }
            }
            else
            {
                // Display message if loaded without a selected object
                TwoTableSelectionHint.Text = "Select a table to view its full dependency map or select two tables for FK comparison.";
            }
        }

    }
}