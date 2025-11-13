using System.Windows.Controls;
using DatabaseVisualizer.Models;
using DatabaseVisualizer.Services;
using System.Windows;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace DatabaseVisualizer.Views
{
    public partial class CodeView : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();

        // Field to hold the selected object; marked nullable for safety.
        private readonly DatabaseObject? _selectedObject;

        // --- CONSTRUCTORS ---

        /// <summary>
        /// Parameterless Constructor (For routing safety).
        /// </summary>
        public CodeView() : this((DatabaseObject?)null) { }

        /// <summary>
        /// Constructor for single-object selection (Proc/Func).
        /// Accepts a single nullable object.
        /// </summary>
        public CodeView(DatabaseObject? selectedObject)
        {
            InitializeComponent();

            // Safely assign the object
            _selectedObject = selectedObject;

            this.Loaded += CodeView_Loaded;
        }

        // --- LOAD HANDLER ---

        private async void CodeView_Loaded(object sender, RoutedEventArgs e)
        {
            // Set all grids to Visible/Hidden state based on data

            if (_selectedObject is not null)
            {
                string schemaName = _selectedObject.SchemaName;
                string objectName = _selectedObject.Name;

                // 1. Load Details (Definition, Dates, Parameters)
                var details = await Task.Run(() => _metadataService.GetObjectDetails(schemaName, objectName));

                // 2. Load Dependencies
                var dependencies = await Task.Run(() => _metadataService.GetObjectDependencies(schemaName, objectName));

                // 3. Load Permissions (Integrated Security Check)
                var permissions = await Task.Run(() => _metadataService.GetObjectPermissions(objectName));


                // --- Code Block and Dates (Always displayed if object is selected) ---
                if (details.Definition.Contains("ENCRYPTED"))
                {
                    CodeDefinitionTextBox.Text = $"-- WARNING: Object is ENCRYPTED. Definition is not stored in plain text.\n\n"
                                               + $"-- Last Modified: {details.ModifyDate}\n";
                }
                else
                {
                    CodeDefinitionTextBox.Text = details.Definition;
                }

                CreateDateTextBlock.Text = details.CreateDate.ToString();
                ModifyDateTextBlock.Text = details.ModifyDate.ToString();


                // --- Parameter Logic ---
                bool hasParameters = details.Parameters.Any();
                ParameterGrid.ItemsSource = details.Parameters;
                NoParametersTextBlock.Visibility = hasParameters ? Visibility.Collapsed : Visibility.Visible;
                ParameterGrid.Visibility = hasParameters ? Visibility.Visible : Visibility.Collapsed;

                // --- Dependency Logic ---
                bool hasDependencies = dependencies.Any();
                DependencyGrid.ItemsSource = dependencies;
                NoDependenciesTextBlock.Visibility = hasDependencies ? Visibility.Collapsed : Visibility.Visible;
                DependencyGrid.Visibility = hasDependencies ? Visibility.Visible : Visibility.Collapsed;

                // --- Permissions Logic ---
                bool hasPermissions = permissions.Any();
                PermissionsDataGrid.ItemsSource = permissions;
                NoPermissionsTextBlock.Visibility = hasPermissions ? Visibility.Collapsed : Visibility.Visible;
                PermissionsDataGrid.Visibility = hasPermissions ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                // Default state if loaded without selection (e.g., failed routing)
                CodeDefinitionTextBox.Text = "Select a Stored Procedure or Function from the object list to view its code and dependencies.";

                // Hide grids and show messages
                NoParametersTextBlock.Visibility = Visibility.Visible;
                ParameterGrid.Visibility = Visibility.Collapsed;
                NoDependenciesTextBlock.Visibility = Visibility.Visible;
                DependencyGrid.Visibility = Visibility.Collapsed;
                NoPermissionsTextBlock.Visibility = Visibility.Visible;
                PermissionsDataGrid.Visibility = Visibility.Collapsed;
            }
        }
    }
}