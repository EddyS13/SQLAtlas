// Views/SecurityView.xaml.cs

using System.Windows.Controls;
using DatabaseVisualizer.Models;
using DatabaseVisualizer.Services;
using System.Windows;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq; // Required for .Any() and .First()

namespace DatabaseVisualizer.Views
{
    public partial class SecurityView : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();

        // Field to hold the selected object(s); marked nullable since the view can load without data.
        private readonly List<DatabaseObject>? _selectedObjects;

        /// <summary>
        /// Parameterless Constructor (Called from Tools Menu).
        /// Routes to the parameterized constructor with a null object.
        /// </summary>
        public SecurityView() : this((DatabaseObject?)null) { }

        /// <summary>
        /// Constructor for single-object selection (Table/View/Proc).
        /// Accepts a single nullable object.
        /// </summary>
        public SecurityView(DatabaseObject? selectedObject)
        {
            InitializeComponent();

            // Convert the single object into a list for consistent internal handling
            if (selectedObject != null)
            {
                _selectedObjects = new List<DatabaseObject> { selectedObject };
            }
            else
            {
                _selectedObjects = null;
            }

            this.Loaded += SecurityView_Loaded;
        }

        /// <summary>
        /// Constructor for multi-select routing (although security is usually single-object).
        /// </summary>
        public SecurityView(List<DatabaseObject>? selectedObjects)
        {
            InitializeComponent();
            _selectedObjects = selectedObjects;
            this.Loaded += SecurityView_Loaded;
        }


        private async void SecurityView_Loaded(object sender, RoutedEventArgs e)
        {
            // Only proceed if at least one object was passed
            if (_selectedObjects != null && _selectedObjects.Any())
            {
                DatabaseObject targetObject = _selectedObjects.First();
                string objectName = targetObject.Name;

                // 1. User/Role Manager Data
                var principals = await Task.Run(() => _metadataService.GetDatabasePrincipals());
                UsersRolesGrid.ItemsSource = principals; // Assuming grid name is UsersRolesGrid

                // 2. Audit Log Viewer Data
                var events = await Task.Run(() => _metadataService.GetRecentSecurityEvents());
                AuditLogGrid.ItemsSource = events; // Assuming grid name is AuditLogGrid
            }

        }

    }
}