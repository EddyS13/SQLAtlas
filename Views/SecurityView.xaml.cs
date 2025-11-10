// Views/SecurityView.xaml.cs

using System.Windows.Controls;
using DatabaseVisualizer.Models;
using DatabaseVisualizer.Services;
using System.Windows;
using System.Threading.Tasks;

namespace DatabaseVisualizer.Views
{
    public partial class SecurityView : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();
        private readonly DatabaseObject _selectedObject;

        private readonly List<DatabaseObject> _selectedObjects;
        private readonly bool _isMultiSelect; // To differentiate logic within the view

        // 1. Parameterless Constructor (Called from Tools Menu)
        public SecurityView() : this(null) { }

        // Note: The main selection logic needs to pass the selected object to the SecurityView
        // when a Table, View, or Proc is selected.
        public SecurityView(DatabaseObject selectedObject = null)
        {
            InitializeComponent();
            _selectedObject = selectedObject;
            this.Loaded += SecurityView_Loaded;
        }

        private async void SecurityView_Loaded(object sender, RoutedEventArgs e)
        {
            // This view can either show all security info or filter by the passed object.
            if (_selectedObject != null)
            {
                var permissions = await Task.Run(() => _metadataService.GetObjectPermissions(_selectedObject.Name));
                PermissionsDataGrid.ItemsSource = permissions;
            }
        }
    }
}