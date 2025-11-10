// ExplorerWindow.xaml.cs

using DatabaseVisualizer.Data;
using DatabaseVisualizer.Models;
using DatabaseVisualizer.Services;
using DatabaseVisualizer.Views;
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
        // CRITICAL FIX: Declare the ICollectionView field for search/filtering
        private ICollectionView _objectCollectionView;

        public ExplorerWindow(Dictionary<string, List<DatabaseObject>> groupedObjects, string serverName, string databaseName)
        {
            InitializeComponent();

            // 1. Initial Setup
            LoadObjectListBox(groupedObjects);
            PopulateToolsMenu();

            // Set Version Info
            VersionNumberTextBlock.Text = "v0.7.2";
            VersionDateTextBlock.Text = $"Build: {DateTime.Now:yyyy-MM-dd}";

            // Set the Status Bar text
            ConnectionInfoTextBlock.Text = $"Server: {serverName} | DB: {databaseName}";

            // Set initial default view
            MainContentHost.Content = new ActivityView();
        }

        // === NAVIGATION SETUP METHODS ===

        private void PopulateToolsMenu()
        {
            var tools = new List<string>
            {
                "Activity & Storage",
                "Performance Monitor",
                "Index Analysis",
            };
            ToolsListBox.ItemsSource = tools;
        }

        /// <summary>
        /// Populates the ObjectListBox and sets up grouping.
        /// </summary>
        private void LoadObjectListBox(Dictionary<string, List<DatabaseObject>> groupedObjects)
        {
            var flatList = groupedObjects.SelectMany(g => g.Value).ToList();

            ObjectListBox.ItemsSource = flatList;

            // Get the collection view and store it for filtering
            _objectCollectionView = CollectionViewSource.GetDefaultView(ObjectListBox.ItemsSource);

            // Apply grouping
            if (_objectCollectionView != null && _objectCollectionView.CanGroup == true)
            {
                _objectCollectionView.GroupDescriptions.Clear();
                _objectCollectionView.GroupDescriptions.Add(new PropertyGroupDescription("TypeDescription"));
            }
        }

        // === SEARCH HANDLER (Issue 8 Logic) ===

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

        // === EVENT HANDLERS (Routing Logic) ===

        /// <summary>
        /// Handles navigation when a diagnostic tool is selected from the sidebar.
        /// </summary>
        private void ToolsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ToolsListBox.SelectedItem is string selectedTool)
            {
                // Ensure no object is selected when a tool is active
                ObjectListBox.SelectedItem = null;
                LoadDiagnosticView(selectedTool);
            }
        }

        /// <summary>
        /// Handles navigation when a database object is selected from the list.
        /// </summary>
        private void ObjectListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Reset tool selection to prevent conflicts
            ToolsListBox.SelectedItem = null;

            // 1. Get Selected Objects (Defensive Casting)
            var selectedObjects = ObjectListBox.SelectedItems.Cast<object>().OfType<DatabaseObject>().ToList();
            var selectedTables = selectedObjects.Where(o => o.Type.ToUpperInvariant() == "U").ToList();

            // 2. --- Multi-Select Logic (FK Check - Passes List) ---
            if (selectedTables.Count >= 2)
            {
                LoadMetadataView(selectedTables, isMultiSelect: true);
                return;
            }

            // 3. --- Single Select or Deselect ---
            if (selectedObjects.Count == 1)
            {
                // Passes the single selected object
                LoadMetadataView(new List<DatabaseObject> { selectedObjects.First() }, isMultiSelect: false);
            }
            else
            {
                // Deselection or invalid mix (cleanup display)
                MainContentHost.Content = new ActivityView();
            }
        }

        // --- View Loading Helpers ---

        private void LoadDiagnosticView(string viewName)
        {
            UserControl newView = viewName switch
            {
                "Activity & Storage" => new ActivityView(),
                "Performance Monitor" => new PerformanceView(),
                "Index Analysis" => new IndexView(),
                _ => null,
            };

            // Fallback ensures ContentHost always receives a valid UserControl
            MainContentHost.Content = newView ?? new UserControl { Content = new TextBlock { Text = $"Tool '{viewName}' not found." } };
        }

        private void LoadMetadataView(List<DatabaseObject> selectedObjects, bool isMultiSelect)
        {
            UserControl newView = null;

            if (isMultiSelect)
            {
                // MULTI-SELECT: Load Relationship View
                newView = new RelationshipsView(selectedObjects, isMultiSelect: true);
            }
            else if (selectedObjects.Count == 1)
            {
                DatabaseObject dbObject = selectedObjects.First();
                string objectType = dbObject.Type.ToUpperInvariant();

                if (objectType == "U" || objectType == "V")
                {
                    // TABLES & VIEWS: Load Columns view (which includes Relationships/Security)
                    newView = new ColumnsView(dbObject);
                }
                else if (objectType == "P" || objectType.Contains("F"))
                {
                    // PROCEDURES & FUNCTIONS: Load Code Inspection view (which includes Security)
                    newView = new CodeView(dbObject);
                }
            }

            // Fallback ensures ContentHost always receives a valid object
            MainContentHost.Content = newView ?? new UserControl { Content = new TextBlock { Text = "Select an object." } };
        }

        // === SYSTEM CONTROL HANDLERS ===

        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            SqlConnectionManager.Disconnect();
            MainWindow newMainWindow = new MainWindow();
            newMainWindow.Show();
            this.Close();
        }

        // --- Custom Window Chrome Handlers ---
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // --- SCROLL WHEEL HANDLER (Issue 3 Fix) ---
        private void ObjectListScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
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
                e.Handled = true;
            }
        }
    }
}