// Views/ColumnsView.xaml.cs

using System.Windows.Controls;
using DatabaseVisualizer.Models;
using DatabaseVisualizer.Services;
using System.Windows;
using System.Threading.Tasks;

namespace DatabaseVisualizer.Views
{
    public partial class ColumnsView : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();
        private readonly DatabaseObject _selectedObject;

        private readonly List<DatabaseObject> _selectedObjects;
        private readonly bool _isMultiSelect; // To differentiate logic within the view

        // 1. Parameterless Constructor (Called from Tools Menu)
        public ColumnsView() : this(null) { }

        public ColumnsView(DatabaseObject selectedObject)
        {
            InitializeComponent();
            _selectedObject = selectedObject;
            this.Loaded += ColumnsView_Loaded;
        }

        private async void ColumnsView_Loaded(object sender, RoutedEventArgs e)
        {
            if (_selectedObject != null)
            {
                // Load Column Details (Existing Logic)
                var columnDetails = await Task.Run(() => _metadataService.GetColumnDetails(_selectedObject.Name));
                ColumnDetailsGrid.ItemsSource = columnDetails;

                // NEW: Load Permissions (Issue 7 Integration)
                var permissions = await Task.Run(() => _metadataService.GetObjectPermissions(_selectedObject.Name));
                PermissionsDataGrid.ItemsSource = permissions;
            }
        }
    }
}