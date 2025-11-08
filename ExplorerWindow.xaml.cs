// ExplorerWindow.xaml.cs

using DatabaseVisualizer.Data;
using DatabaseVisualizer.Models;
using DatabaseVisualizer.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace DatabaseVisualizer
{
    public partial class ExplorerWindow : Window
    {
        private readonly MetadataService _metadataService = new MetadataService();

        // CRITICAL UPDATE: Constructor accepts server/database names
        public ExplorerWindow(Dictionary<string, List<DatabaseObject>> groupedObjects, string serverName, string databaseName)
        {
            InitializeComponent();
            LoadObjectListBox(groupedObjects);

            // Set the Status Bar text (Phase 1 UX)
            ConnectionInfoTextBlock.Text = $"Server: {serverName} | DB: {databaseName}";

            // Initialize both dynamic tabs
            RefreshActivityButton_Click(null, null);
            RefreshPerformanceButton_Click(null, null);

            // Initialize all dynamic tabs
            RefreshActivityButton_Click(null, null);
            RefreshPerformanceButton_Click(null, null);
            RefreshIndexButton_Click(null, null); // <<< ADD THIS LINE
        }

        // ExplorerWindow.xaml.cs (Add this field)
        private ICollectionView _objectCollectionView;

        /// <summary>
        /// Populates the ObjectListBox.
        /// </summary>
        private void LoadObjectListBox(Dictionary<string, List<DatabaseObject>> groupedObjects)
        {
            var flatList = groupedObjects.SelectMany(g => g.Value).ToList();

            ObjectListBox.ItemsSource = flatList;

            // Get the collection view and store it
            _objectCollectionView = CollectionViewSource.GetDefaultView(ObjectListBox.ItemsSource);

            // Apply grouping (existing logic)
            if (_objectCollectionView != null && _objectCollectionView.CanGroup == true)
            {
                _objectCollectionView.GroupDescriptions.Clear();
                _objectCollectionView.GroupDescriptions.Add(new PropertyGroupDescription("TypeDescription"));
            }

            /* PREVIOUS CODE
            // Apply grouping to the ListBox based on TypeDescription
            ICollectionView view = CollectionViewSource.GetDefaultView(ObjectListBox.ItemsSource);
            if (view != null && view.CanGroup == true)
            {
                view.GroupDescriptions.Clear();
                view.GroupDescriptions.Add(new PropertyGroupDescription("TypeDescription"));
            }
            */
        }

        // ExplorerWindow.xaml.cs (Add the new TextChanged handler)
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_objectCollectionView == null) return;

            string filterText = SearchTextBox.Text.ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(filterText))
            {
                _objectCollectionView.Filter = null; // Show all items
            }
            else
            {
                // Set the filter delegate
                _objectCollectionView.Filter = (item) =>
                {
                    if (item is DatabaseObject dbObject)
                    {
                        // Filter by name or description
                        return dbObject.Name.ToLowerInvariant().Contains(filterText) ||
                               dbObject.TypeDescription.ToLowerInvariant().Contains(filterText);
                    }
                    return false;
                };
            }
        }

        /// <summary>
        /// Event handler for selection changes in the ObjectListBox.
        /// </summary>
        private void ObjectListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // --- Reset all details on the right pane ---
                ColumnDetailsGrid.ItemsSource = null;
                ForeignKeyGrid.ItemsSource = null;
                TableRelationshipGrid.ItemsSource = null;
                TwoTableSelectionHint.Visibility = Visibility.Visible;
                ParameterGrid.ItemsSource = null;
                DependencyGrid.ItemsSource = null;
                CodeDefinitionTextBox.Text = string.Empty;

                var selectedObjects = ObjectListBox.SelectedItems.Cast<object>().OfType<DatabaseObject>().ToList();
                var selectedTables = selectedObjects.Where(o => o.Type.ToUpperInvariant() == "U").ToList();

                // 1. --- Multi-Select (Case 1: FK Check) ---
                if (selectedTables.Count >= 2)
                {
                    try
                    {
                        ForeignKeyGrid.ItemsSource = _metadataService.GetForeignKeysBetween(
                            selectedTables[0].Name,
                            selectedTables[1].Name);

                        TwoTableSelectionHint.Visibility = Visibility.Collapsed;
                        DetailsTabControl.SelectedItem = RelationshipsTab;
                        TableRelationshipGrid.ItemsSource = null;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Foreign Key Lookup Error: {ex.Message}", "Data Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    return;
                }

                // 2. --- Single Object Selected (Case 2) ---
                if (selectedObjects.Count == 1)
                {
                    var dbObject = selectedObjects.First();
                    string objectType = dbObject.Type.ToUpperInvariant();

                    // Set contextual header
                    DetailsHeaderTextBlock.Text = $"Details for: {dbObject.Name} ({dbObject.TypeDescription})";

                    // 1. Check if object is a Table (U), View (V), or Stored Procedure (P)
                    bool isPermissionCheckable = (objectType == "U" || objectType == "V" || objectType == "P");

                    if (isPermissionCheckable)
                    {
                        var permissions = _metadataService.GetObjectPermissions(dbObject.Name);
                        PermissionsDataGrid.ItemsSource = permissions;
                    }

                    // A. TABLES (U)
                    if (objectType == "U")
                    {
                        SetObjectTabVisibility(showCodeTab: false); // Hide code tab
                        SetMetadataTabVisibility(showMetadata: true); // Show Columns & Relationships

                        ColumnDetailsGrid.ItemsSource = _metadataService.GetColumnDetails(dbObject.Name);
                        TableRelationshipGrid.ItemsSource = _metadataService.GetTableRelationships(dbObject.Name);

                        DetailsTabControl.SelectedItem = RelationshipsTab;
                        return;
                    }

                    // B. VIEWS (V)
                    if (objectType == "V")
                    {
                        SetObjectTabVisibility(showCodeTab: false); // Hide code tab
                        SetMetadataTabVisibility(showMetadata: true); // Show Columns & Relationships

                        ColumnDetailsGrid.ItemsSource = _metadataService.GetColumnDetails(dbObject.Name);
                        DetailsTabControl.SelectedItem = DetailsTabControl.Items[0]; // Columns Tab
                        return;
                    }

                    // C. PROCEDURES / FUNCTIONS (P, FN, IF, TF) - Code Inspection
                    if (objectType == "P" || objectType.Contains("F"))
                    {
                        DetailsTabControl.SelectedItem = SecurityTab; // <<< Switch to Security Tab
                        SetObjectTabVisibility(showCodeTab: true); // Show code tab
                        SetMetadataTabVisibility(showMetadata: false); // HIDE Columns & Relationships

                        string schemaName = dbObject.SchemaName;

                        var details = _metadataService.GetObjectDetails(schemaName, dbObject.Name);
                        var dependencies = _metadataService.GetObjectDependencies(schemaName, dbObject.Name);

                        // Bind results
                        ParameterGrid.ItemsSource = details.Parameters;
                        DependencyGrid.ItemsSource = dependencies;

                        // New Visibility Logic:
                        NoParametersTextBlock.Visibility = details.Parameters.Any() ? Visibility.Collapsed : Visibility.Visible;
                        ParameterGrid.Visibility = details.Parameters.Any() ? Visibility.Visible : Visibility.Collapsed;

                        // Repeat for Dependencies:
                        NoDependenciesTextBlock.Visibility = dependencies.Any() ? Visibility.Collapsed : Visibility.Visible;
                        DependencyGrid.Visibility = dependencies.Any() ? Visibility.Visible : Visibility.Collapsed;

                        // Set Definition text with date info
                        if (details.Definition.Contains("ENCRYPTED"))
                        {
                            CodeDefinitionTextBox.Text = $"-- WARNING: Object is ENCRYPTED. Definition is not stored in plain text.\n\n"
                                                       + $"-- Original source must be retrieved from source control.\n"
                                                       + $"-- Last Modified: {details.ModifyDate}\n";
                        }
                        else
                        {
                            CodeDefinitionTextBox.Text = details.Definition;
                        }

                        // Set Date TextBlocks
                        CreateDateTextBlock.Text = details.CreateDate.ToString();
                        ModifyDateTextBlock.Text = details.ModifyDate.ToString();

                        DetailsTabControl.SelectedItem = CodeTab;
                        return;
                    }

                    // Final Fallback for unhandled types
                    DetailsHeaderTextBlock.Text = $"No details available for: {dbObject.Name}";
                    DetailsTabControl.SelectedItem = ActivityTab;
                    return;
                }

                // Catch-all for deselection or invalid count
                DetailsHeaderTextBlock.Text = "Select an object from the list.";
                DetailsTabControl.SelectedItem = ActivityTab;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"FATAL UNHANDLED ERROR IN SELECTION LOGIC:\n{ex.Message}",
                                "CRITICAL APPLICATION FAILURE", MessageBoxButton.OK, MessageBoxImage.Error);
                DetailsTabControl.SelectedItem = ActivityTab;
            }
        }

        /// <summary>
        /// Fetches and displays the current active database sessions and requests (Activity Tab).
        /// </summary>
        private async void RefreshActivityButton_Click(object sender, RoutedEventArgs e)
        {
            
            RefreshActivityButton.Content = "FETCHING ACTIVITY DATA...";
            RefreshActivityButton.IsEnabled = false;

            try
            {

                // 1. Existing Call: Activity Data (Current Sessions/Requests)
                var activityList = await Task.Run(() => _metadataService.GetDatabaseActivity());
                ActivityDataGrid.ItemsSource = activityList;

                // 2. Existing Call: Database Space Info (Feature 3A - File Size)
                var spaceInfoList = await Task.Run(() => _metadataService.GetDatabaseSpaceInfo());
                DatabaseSpaceDataGrid.ItemsSource = spaceInfoList;

                // 3. New Call: Display Plan Cache Size (Feature B)
                string cacheSize = await Task.Run(() => _metadataService.GetPlanCacheSize());

                // You need to ensure the PlanCacheSizeTextBlock is updated on the UI thread
                PlanCacheSizeTextBlock.Text = cacheSize;

                RefreshActivityButton.Content = $"Activity Refreshed ({DateTime.Now.ToShortTimeString()})";
            }
            catch (Exception ex)
            {
                // ... (Error handling remains the same) ...
                MessageBox.Show($"Failed to retrieve activity/space data: {ex.Message}", "Activity Error", MessageBoxButton.OK, MessageBoxImage.Error);
                RefreshActivityButton.Content = "Refresh Failed";
            }
            finally
            {
                RefreshActivityButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// Fetches and displays the top long-running queries (Performance Tab).
        /// </summary>
        private async void RefreshPerformanceButton_Click(object sender, RoutedEventArgs e)
        {
            
            RefreshPerformanceButton.Content = "ANALYZING PERFORMANCE...";
            RefreshPerformanceButton.IsEnabled = false;

            try
            {
                // 1. Existing Call: Top 5 Active Requests
                var queryList = await Task.Run(() => _metadataService.GetLongRunningQueries());
                PerformanceDataGrid.ItemsSource = queryList;

                // 2. Existing Call: Expensive Cached Queries
                var expensiveQueries = await Task.Run(() => _metadataService.GetTopExpensiveQueries());
                ExpensiveQueriesDataGrid.ItemsSource = expensiveQueries;

                // 3. New Call: Wait Stats (Feature 2A)
                var waitStats = await Task.Run(() => _metadataService.GetTopWaits());
                WaitStatsDataGrid.ItemsSource = waitStats;

                // 4. New Call: Blocking Chain (Feature C)
                var blockingList = await Task.Run(() => _metadataService.GetCurrentBlockingChain());
                BlockingChainDataGrid.ItemsSource = blockingList;

                RefreshPerformanceButton.Content = $"Performance Data Refreshed ({DateTime.Now.ToShortTimeString()})";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to retrieve performance data: {ex.Message}", "Performance Error", MessageBoxButton.OK, MessageBoxImage.Error);
                RefreshPerformanceButton.Content = "Refresh Failed";
            }
            finally
            {
                RefreshPerformanceButton.IsEnabled = true;
            }
        }

        private void SetObjectTabVisibility(bool showCodeTab)
        {
            // Find the TabItems by name
            TabItem codeTab = (TabItem)DetailsTabControl.FindName("CodeTab");

            if (codeTab != null)
            {
                // Only hide/show the Code Inspection tab
                codeTab.Visibility = showCodeTab ? Visibility.Visible : Visibility.Collapsed;
            }

            // NOTE: PerformanceTab and ActivityTab are NOT modified here and remain visible.
        }

        private void SetMetadataTabVisibility(bool showMetadata)
        {
            // Note: We use Visibility.Collapsed so the space is reclaimed.

            TabItem columnsTab = (TabItem)DetailsTabControl.FindName("ColumnsTab"); // Ensure you name this tab in XAML
            TabItem relationshipsTab = (TabItem)DetailsTabControl.FindName("RelationshipsTab");

            if (columnsTab != null)
            {
                columnsTab.Visibility = showMetadata ? Visibility.Visible : Visibility.Collapsed;
            }
            if (relationshipsTab != null)
            {
                relationshipsTab.Visibility = showMetadata ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // ExplorerWindow.xaml.cs (Add the PreviewMouseWheel handler)
        private void ObjectListScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // This handles the event that the mouse wheel scrolls over the ListBox content.
            // It redirects the event to the parent ScrollViewer.
            if (sender is ScrollViewer scrollViewer)
            {
                // Check if the scroll wheel is moving up or down
                if (e.Delta < 0)
                {
                    scrollViewer.LineDown();
                    scrollViewer.LineDown();
                }
                else
                {
                    scrollViewer.LineUp();
                    scrollViewer.LineUp();
                }
                e.Handled = true; // Mark the event as handled so it doesn't propagate further
            }
        }

        // ExplorerWindow.xaml.cs (Add the event handler)
        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. Clear the connection string
            SqlConnectionManager.Disconnect();

            // 2. Create and show a new MainWindow
            MainWindow newMainWindow = new MainWindow();
            newMainWindow.Show();

            // 3. Close the current Explorer window
            this.Close();
        }

        // ExplorerWindow.xaml.cs (Add these event handlers)

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            // Sets the window state to minimized
            WindowState = WindowState.Minimized;
        }

        private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
        {
            // Toggles between Maximized and Normal window states
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }
            else
            {
                WindowState = WindowState.Maximized;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Closes the current window
            Close();
        }

        private async void RefreshIndexButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshIndexButton.Content = "CALCULATING...";
            RefreshIndexButton.IsEnabled = false;
            IndexStatusTextBlock.Text = "Calculating Index Fragmentation..."; // Set loading message

            try
            {
                // 1. Run the blocking database operation on a background thread
                // This prevents the UI from freezing while the query executes.
                var indexList = await Task.Run(() => _metadataService.GetIndexFragmentation());

                // 2. Binding and Final Feedback (back on the UI thread)
                FragmentedIndexDataGrid.ItemsSource = indexList;

                IndexStatusTextBlock.Text = $"Analysis Complete. Found {indexList.Count} fragmented indexes.";
                RefreshIndexButton.Content = $"Refresh Index Analysis ({DateTime.Now.ToShortTimeString()})";
            }
            catch (Exception ex)
            {
                IndexStatusTextBlock.Text = "ERROR: Failed to retrieve index data.";
                MessageBox.Show($"Failed to retrieve index data: {ex.Message}", "Index Error", MessageBoxButton.OK, MessageBoxImage.Error);
                RefreshIndexButton.Content = "Refresh Index Analysis";
            }
            finally
            {
                RefreshIndexButton.IsEnabled = true;
            }
        }







    }
}