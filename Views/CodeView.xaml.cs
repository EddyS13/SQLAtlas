// Views/CodeView.xaml.cs

using System.Windows.Controls;
using DatabaseVisualizer.Models;
using DatabaseVisualizer.Services;
using System.Windows;
using System.Threading.Tasks;

namespace DatabaseVisualizer.Views
{
    public partial class CodeView : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();
        private readonly DatabaseObject _selectedObject;

        private readonly List<DatabaseObject> _selectedObjects;
        private readonly bool _isMultiSelect; // To differentiate logic within the view

        // 1. Parameterless Constructor (Called from Tools Menu)
        public CodeView() : this(null) { }

        public CodeView(DatabaseObject selectedObject)
        {
            InitializeComponent();
            _selectedObject = selectedObject;
            this.Loaded += CodeView_Loaded;
        }

        private async void CodeView_Loaded(object sender, RoutedEventArgs e)
        {
            if (_selectedObject != null)
            {
                string schemaName = _selectedObject.SchemaName;
                string objectName = _selectedObject.Name;

                // Load Details (Definition, Dates, Parameters)
                var details = await Task.Run(() => _metadataService.GetObjectDetails(schemaName, objectName));
                ParameterGrid.ItemsSource = details.Parameters;

                // Set Definition text with date info and handling for encryption
                if (details.Definition.Contains("ENCRYPTED"))
                {
                    CodeDefinitionTextBox.Text = $"-- WARNING: Object is ENCRYPTED. Definition is not stored in plain text.\n\n"
                                               + $"-- Last Modified: {details.ModifyDate}\n";
                }
                else
                {
                    CodeDefinitionTextBox.Text = details.Definition;
                }

                var permissions = await Task.Run(() => _metadataService.GetObjectPermissions(_selectedObject.Name));
                PermissionsDataGrid.ItemsSource = permissions;

                // Check Parameter List
                bool hasParameters = details.Parameters.Any();
                ParameterGrid.ItemsSource = details.Parameters;

                NoParametersTextBlock.Visibility = hasParameters ? Visibility.Collapsed : Visibility.Visible;
                ParameterGrid.Visibility = hasParameters ? Visibility.Visible : Visibility.Collapsed;

                CreateDateTextBlock.Text = details.CreateDate.ToString();
                ModifyDateTextBlock.Text = details.ModifyDate.ToString();

                // Load Dependencies
                var dependencies = await Task.Run(() => _metadataService.GetObjectDependencies(schemaName, objectName));
                DependencyGrid.ItemsSource = dependencies;
            }
        }
    }
}