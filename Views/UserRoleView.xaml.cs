using SQLAtlas.Models;
using SQLAtlas.Services;
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

namespace SQLAtlas.Views
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

        private List<DatabasePrincipal> _allPrincipals = new List<DatabasePrincipal>();
        private List<RoleMembership> _allMemberships = new List<RoleMembership>();

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
            try
            {
                _allPrincipals = await Task.Run(() => _metadataService.GetDatabasePrincipals());
                _allMemberships = await Task.Run(() => _metadataService.GetRoleMemberships());

                Dispatcher.Invoke(() => {
                    UsersRolesGrid.ItemsSource = _allPrincipals;
                    MembershipsGrid.ItemsSource = _allMemberships;
                    NoPrincipalsContainer.Visibility = (_allPrincipals.Any()) ? Visibility.Collapsed : Visibility.Visible;

                    // Set initial placeholder text
                    SecuritySearchBox.Text = SecuritySearchBox.Tag.ToString();
                    SecuritySearchBox.Opacity = 0.5;
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading security data: {ex.Message}");
            }
        }

        private void SecuritySearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = SecuritySearchBox.Text.ToLower();

            // Skip filtering if it's just the placeholder text
            if (filter == SecuritySearchBox.Tag.ToString().ToLower()) return;

            if (string.IsNullOrWhiteSpace(filter))
            {
                UsersRolesGrid.ItemsSource = _allPrincipals;
                MembershipsGrid.ItemsSource = _allMemberships;
            }
            else
            {
                // Filter Principals by Name or Type
                UsersRolesGrid.ItemsSource = _allPrincipals
                    .Where(p => p.Name.ToLower().Contains(filter) || p.TypeDescription.ToLower().Contains(filter))
                    .ToList();

                // Filter Memberships by Role or Member Name
                MembershipsGrid.ItemsSource = _allMemberships
                    .Where(m => m.RoleName.ToLower().Contains(filter) || m.MemberName.ToLower().Contains(filter))
                    .ToList();
            }
        }

        // Simple Placeholder Logic
        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SecuritySearchBox.Text == SecuritySearchBox.Tag.ToString())
            {
                SecuritySearchBox.Text = "";
                SecuritySearchBox.Opacity = 1.0;
            }
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SecuritySearchBox.Text))
            {
                SecuritySearchBox.Text = SecuritySearchBox.Tag.ToString();
                SecuritySearchBox.Opacity = 0.5;
            }
        }

    }

}