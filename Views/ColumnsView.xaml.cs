// Views/ColumnsView.xaml.cs

using System.Windows.Controls;
using DatabaseVisualizer.Models;
using DatabaseVisualizer.Services;
using System.Windows;
using System.Threading.Tasks;
using System.Collections.Generic; // Required for List<DatabaseObject>

namespace DatabaseVisualizer.Views
{
    public partial class ColumnsView : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();

        // Use a List to maintain consistency, even if it usually holds only one object
        private readonly List<DatabaseObject>? _selectedObjects;

        /// <summary>
        /// Constructor for the Tools menu (no object selected).
        /// Calls the parameterized constructor with a null object.
        /// </summary>
        public ColumnsView() : this((DatabaseObject?)null) { }

        /// <summary>
        /// Constructor for single-object selection (Table/View).
        /// Accepts a single nullable object.
        /// </summary>
        public ColumnsView(DatabaseObject? selectedObject)
        {
            InitializeComponent();

            // Check if a single object was passed and convert it to a list for internal consistency
            if (selectedObject != null)
            {
                _selectedObjects = new List<DatabaseObject> { selectedObject };
            }
            else
            {
                _selectedObjects = null;
            }

            this.Loaded += ColumnsView_Loaded;
        }

        /// <summary>
        /// Constructor for multi-select routing (although ColumnsView is typically single-select,
        /// this handles the routing from ExplorerWindow's multi-select logic).
        /// </summary>
        public ColumnsView(List<DatabaseObject>? selectedObjects)
        {
            InitializeComponent();
            _selectedObjects = selectedObjects;
            this.Loaded += ColumnsView_Loaded;
        }

        private async void ColumnsView_Loaded(object sender, RoutedEventArgs e)
        {

            // Only proceed if at least one object was passed (the first element is the target)
            if (_selectedObjects != null && _selectedObjects.Any())
            {
                DatabaseObject targetObject = _selectedObjects.First();
                string objectName = targetObject.Name;

                ObjectContextHeader.Text = $"Schema Details for: {targetObject.SchemaName}.{targetObject.Name} ({targetObject.TypeDescription})";

                // 1. Load Column Details
                var columnDetails = await Task.Run(() => _metadataService.GetColumnDetails(objectName));
                ColumnDetailsGrid.ItemsSource = columnDetails;

                // 2. Load Permissions (Issue 7 integration)
                // Permissions check is done for Tables (U) and Views (V)
                var permissions = await Task.Run(() => _metadataService.GetObjectPermissions(objectName));
                PermissionsDataGrid.ItemsSource = permissions;
            }
            else
            {
                ObjectContextHeader.Text = "Please select a Table or View from the object list.";

                // Display message if loaded without a selected object
                ColumnDetailsGrid.ItemsSource = null;
                PermissionsDataGrid.ItemsSource = null;
                // Note: You would typically update a dedicated TextBlock here, but we use the grid directly.
            }
        }
    }
}