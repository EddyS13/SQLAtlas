using DatabaseVisualizer.Models;
using DatabaseVisualizer.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DatabaseVisualizer.Views
{
    /// <summary>
    /// Interaction logic for UserRoleView.xaml
    /// </summary>
    public partial class UserRoleView : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();

        // Field to hold the selected object(s); marked nullable since the view can load without data.
        private readonly List<DatabaseObject>? _selectedObjects;

        /// <summary>
        /// Parameterless Constructor (Called from Tools Menu).
        /// Routes to the parameterized constructor with a null object.
        /// </summary>
        public UserRoleView() : this((DatabaseObject?)null) { }

        /// <summary>
        /// Constructor for single-object selection (Table/View/Proc).
        /// Accepts a single nullable object.
        /// </summary>
        public UserRoleView(DatabaseObject? selectedObject)
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
        public UserRoleView(List<DatabaseObject>? selectedObjects)
        {
            InitializeComponent();
            _selectedObjects = selectedObjects;
            this.Loaded += SecurityView_Loaded;
        }


        private async void SecurityView_Loaded(object sender, RoutedEventArgs e)
        {
            // Reset status of placeholders
            NoPrincipalsTextBlock.Visibility = Visibility.Collapsed;

            // Only proceed if the view was loaded without a selected object (e.g., from the Tools menu)
            if (_selectedObjects is null || !_selectedObjects.Any())
            {
                try
                {
                    // Fetch data
                    var principals = await Task.Run(() => _metadataService.GetDatabasePrincipals());
                    bool hasPrincipals = principals.Any();

                    if (hasPrincipals)
                    {
                        UsersRolesGrid.ItemsSource = principals;
                        UsersRolesGrid.Visibility = Visibility.Visible;

                        // CRITICAL FIX: Ensure headers are visible when data is present
                        UsersRolesGrid.HeadersVisibility = DataGridHeadersVisibility.Column;
                        NoPrincipalsTextBlock.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        // If empty, hide the grid contents and headers, show message
                        UsersRolesGrid.ItemsSource = null;
                        UsersRolesGrid.Visibility = Visibility.Collapsed;

                        // CRITICAL FIX: Hide the column headers (the "short blue bar")
                        UsersRolesGrid.HeadersVisibility = DataGridHeadersVisibility.None;

                        NoPrincipalsTextBlock.Visibility = Visibility.Visible;
                    }
                }
                catch (Exception ex)
                {
                    // Handle error, display message
                    MessageBox.Show($"Error loading user audit data: {ex.Message}", "Security Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

    }

}