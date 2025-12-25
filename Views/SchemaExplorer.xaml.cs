using SQLAtlas.Models;
using SQLAtlas.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SQLAtlas.Views
{
    public partial class SchemaExplorer : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();
        private readonly List<DatabaseObject>? _selectedObjects;

        public SchemaExplorer() : this((DatabaseObject?)null) { }

        public SchemaExplorer(DatabaseObject? selectedObject)
        {
            InitializeComponent();
            _selectedObjects = selectedObject != null ? new List<DatabaseObject> { selectedObject } : new List<DatabaseObject>();
            this.Loaded += SchemaExplorer_Loaded;
        }

        public SchemaExplorer(List<DatabaseObject>? selectedObjects)
        {
            InitializeComponent();
            _selectedObjects = selectedObjects ?? new List<DatabaseObject>();
            this.Loaded += SchemaExplorer_Loaded;
        }

        private async void SchemaExplorer_Loaded(object sender, RoutedEventArgs e)
        {
            if (ObjectContextHeader == null) return;

            if (_selectedObjects == null || _selectedObjects.Count == 0)
            {
                SetTabVisibility(isComparison: false);
                ClearDetails("OBJECT EXPLORER");
                return;
            }

            // CASE 1: COMPARISON MODE (2 Tables)
            if (_selectedObjects.Count == 2)
            {
                DatabaseObject t1 = _selectedObjects[0];
                DatabaseObject t2 = _selectedObjects[1];

                // UI Prep
                SetTabVisibility(isComparison: true);
                ObjectIconHeader.Text = "🔄";
                ObjectContextHeader.Text = "COMPARISON MODE";
                ObjectSubHeader.Text = $"{t1.SchemaName}.{t1.Name} vs {t2.SchemaName}.{t2.Name}";

                // Ensure the Comparison Tab is the one seen
                ComparisonTab.IsSelected = true;

                await LoadTwoTableForeignKeys(t1, t2);
            }
            // CASE 2: SINGLE TABLE/VIEW MODE
            else if (_selectedObjects.Count == 1)
            {
                SetTabVisibility(isComparison: false);
                DatabaseObject target = _selectedObjects.First();
                ObjectContextHeader.Text = target.Name.ToUpper();
                ObjectSubHeader.Text = $"SCHEMA: {target.SchemaName} | TYPE: {target.TypeDescription.ToUpper()}";

                if (ComparisonTab != null) ComparisonTab.Visibility = Visibility.Collapsed;
                ObjectIconHeader.Text = "📋";

                // Important: Clear old data immediately to avoid "ghosting"
                ClearSingleTableGrids();
                await LoadSingleTableDetails(target.SchemaName, target.Name);
            }
        }

        // NEW HELPER: Toggles Tab Visibility
        private void SetTabVisibility(bool isComparison)
        {
            var singleVisibility = isComparison ? Visibility.Collapsed : Visibility.Visible;
            var compareVisibility = isComparison ? Visibility.Visible : Visibility.Collapsed;

            if (DefinitionTab != null) DefinitionTab.Visibility = singleVisibility;
            if (KeysTab != null) KeysTab.Visibility = singleVisibility;
            if (RelationshipsTab != null) RelationshipsTab.Visibility = singleVisibility;
            if (PermissionsTab != null) PermissionsTab.Visibility = singleVisibility;

            if (ComparisonTab != null) ComparisonTab.Visibility = compareVisibility;
        }

        private async Task LoadSingleTableDetails(string schemaName, string tableName)
        {
            try
            {
                // 1. Load Primary Grid First
                var columns = await Task.Run(() => _metadataService.GetColumnDetails(tableName));
                ColumnDetailsGrid.ItemsSource = columns;

                // 2. Load Secondary Data in Background
                _ = Task.Run(async () => {
                    try
                    {
                        var pks = _metadataService.GetPrimaryAndUniqueKeys(schemaName, tableName);
                        Dispatcher.Invoke(() => BindDataWithEmptyCheck(PrimaryKeyGrid, KeysScroll, NoKeysText, pks));

                        var fks = _metadataService.GetForeignKeys(schemaName, tableName);
                        Dispatcher.Invoke(() => BindDataWithEmptyCheck(ForeignKeyGrid, RelationshipsScroll, NoRelationshipsText, fks));

                        var perms = _metadataService.GetObjectPermissions(tableName);
                        Dispatcher.Invoke(() => BindDataWithEmptyCheck(PermissionsDataGrid, null, NoPermissionsText, perms));

                        var idx = _metadataService.GetTableIndexes(schemaName, tableName);
                        Dispatcher.Invoke(() => IndexDetailGrid.ItemsSource = idx);

                        var deps = _metadataService.GetTableUsageDetails(schemaName, tableName);
                        Dispatcher.Invoke(() => TableUsageGrid.ItemsSource = deps);
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
                });
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
        }

        private async Task LoadTwoTableForeignKeys(DatabaseObject table1, DatabaseObject table2)
        {
            // 1. Immediate UI Feedback
            TwoTableForeignKeyGrid.ItemsSource = null;
            TwoTableSelectionTitle.Text = "ANALYZING RELATIONSHIPS...";
            TwoTableSelectionTitle.Foreground = (SolidColorBrush)Application.Current.Resources["MutedFontColor"];

            try
            {
                string t1Full = $"[{table1.SchemaName}].[{table1.Name}]";
                string t2Full = $"[{table2.SchemaName}].[{table2.Name}]";

                // 2. Run the query on a background thread
                var fks = await Task.Run(() => _metadataService.GetForeignKeysBetween(t1Full, t2Full));

                // 3. Update UI
                Dispatcher.Invoke(() => {
                    TwoTableForeignKeyGrid.ItemsSource = fks;

                    if (fks == null || fks.Count == 0)
                    {
                        TwoTableSelectionTitle.Text = "NO DIRECT FOREIGN KEY RELATIONSHIPS FOUND";
                        TwoTableSelectionTitle.Foreground = (SolidColorBrush)Application.Current.Resources["ErrorColor"];
                    }
                    else
                    {
                        TwoTableSelectionTitle.Text = $"FOUND {fks.Count} MAPPING(S)";
                        TwoTableSelectionTitle.Foreground = (SolidColorBrush)Application.Current.Resources["AccentColor"];
                    }
                });
            }
            catch (Exception ex)
            {
                TwoTableSelectionTitle.Text = "ERROR RETRIEVING MAPPINGS";
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        private void ClearSingleTableGrids()
        {
            ColumnDetailsGrid.ItemsSource = null;
            PermissionsDataGrid.ItemsSource = null;
            PrimaryKeyGrid.ItemsSource = null;
            ForeignKeyGrid.ItemsSource = null;
            TableUsageGrid.ItemsSource = null;
            IndexDetailGrid.ItemsSource = null;
        }

        private void ClearDetails(string message)
        {
            ObjectContextHeader.Text = message;
            ClearSingleTableGrids();
        }

        private void BindDataWithEmptyCheck<T>(DataGrid grid, FrameworkElement? scrollContainer, TextBlock? emptyLabel, List<T> data)
        {
            bool hasData = data != null && data.Count > 0;
            grid.ItemsSource = hasData ? data : null;
            grid.Visibility = hasData ? Visibility.Visible : Visibility.Collapsed;
            if (scrollContainer != null) scrollContainer.Visibility = hasData ? Visibility.Visible : Visibility.Collapsed;
            if (emptyLabel != null) emptyLabel.Visibility = hasData ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}