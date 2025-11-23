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

        // Field to hold all schema data (promoted from constructor)
        private readonly Dictionary<string, List<DatabaseObject>> _allGroupedObjects = new();
        // Field for filtering the schema list
        private ICollectionView? _schemaObjectCollectionView;

        // --- CONSTRUCTOR ---

        public ExplorerWindow(Dictionary<string, List<DatabaseObject>>? groupedObjects,
                             string? serverName,
                             string? databaseName)
        {
            InitializeComponent();

            // 1. Initial Data Setup (Safe assignment of schema data)
            _allGroupedObjects = groupedObjects ?? new Dictionary<string, List<DatabaseObject>>();
            LoadSchemaCollection(_allGroupedObjects); // Initialize the collection view

            // 2. Set Status Bar and Version Info
            VersionNumberTextBlock.Text = "v0.8.0";
            VersionDateTextBlock.Text = $"{DateTime.Now:yyyy-MM-dd}";
            CurrentDatabaseTextBlock.Text = $"DB: {databaseName ?? "N/A"} ({serverName ?? "N/A"})";

            // 3. Set Initial UI State: Select the first ribbon tab to trigger content load
            if (RibbonMenu.Items.Count > 0)
            {
                RibbonMenu.SelectedIndex = 0;
            }
        }

        // --- DATA INITIALIZATION & FILTERING ---

        /// <summary>
        /// Initializes the master list of database objects and the CollectionView for filtering.
        /// </summary>
        private void LoadSchemaCollection(Dictionary<string, List<DatabaseObject>> groupedObjects)
        {
            var flatList = groupedObjects.SelectMany(g => g.Value).ToList();

            ICollectionView view = CollectionViewSource.GetDefaultView(flatList);
            _schemaObjectCollectionView = view;

            // Apply grouping for visual hierarchy in the sidebar
            if (_schemaObjectCollectionView is not null && _schemaObjectCollectionView.CanGroup == true)
            {
                _schemaObjectCollectionView.GroupDescriptions.Clear();
                _schemaObjectCollectionView.GroupDescriptions.Add(new PropertyGroupDescription("TypeDescription"));
            }
        }

        // --- RIBBON MENU ROUTING ---

        /// <summary>
        /// Handles the change of the top Ribbon Menu context to update the sidebar.
        /// </summary>
        private void RibbonMenu_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl && RibbonMenu.SelectedItem is TabItem selectedTab)
            {
                // 1. CRITICAL FIX: Clear the main content host before switching
                MainContentHost.Content = null;

                // 2. Clear any lingering selection in the sidebar list
                SchemaListBox.SelectedItem = null;

                string header = selectedTab.Header.ToString() ?? string.Empty;
                LoadSidebarContent(header);
            }
        }

        /// <summary>
        /// Loads the appropriate content into the sidebar ListBox based on the Ribbon category.
        /// </summary>
        private void LoadSidebarContent(string category)
        {

            // --- ROUTE CONTENT ---
            if (category == "Schema Explorer")
            {
                SidebarHeaderTextBlock.Text = "SELECT SCHEMA OBJECT";

                // FIX: Show Schema List, Hide Tools List
                SchemaScrollViewer.Visibility = Visibility.Visible;
                ToolsScrollViewer.Visibility = Visibility.Collapsed;

                // Assign the grouped view to the Schema ListBox
                SchemaListBox.ItemsSource = _schemaObjectCollectionView;
                SidebarSearchTextBox.Visibility = Visibility.Visible;
                SearchIconTextBlock.Visibility = Visibility.Visible; // <<< SHOW ICON
            }
            else
            {
                // FIX: Show Tools List, Hide Schema List
                SidebarHeaderTextBlock.Text = $"TOOLS FOR {category.ToUpper()}";
                SchemaScrollViewer.Visibility = Visibility.Collapsed;
                ToolsScrollViewer.Visibility = Visibility.Visible;

                List<string> toolItems = GetToolsListForCategory(category);
                // Assign the simple list of strings to the dedicated Tools ListBox
                ToolsListBox.ItemsSource = toolItems;
                SidebarSearchTextBox.Visibility = Visibility.Collapsed;
                SearchIconTextBlock.Visibility = Visibility.Collapsed; // <<< HIDE ICON
            }
        }

        /// <summary>
        /// Initializes the master list of database objects and the CollectionView for filtering.
        /// </summary>
        private void LoadObjectListBox(Dictionary<string, List<DatabaseObject>> groupedObjects)
        {
            var flatList = groupedObjects.SelectMany(g => g.Value).ToList();

            // Get the collection view and store it for filtering
            ICollectionView view = CollectionViewSource.GetDefaultView(flatList);
            _schemaObjectCollectionView = view;

            // Apply grouping
            if (_schemaObjectCollectionView is not null && _schemaObjectCollectionView.CanGroup == true)
            {
                _schemaObjectCollectionView.GroupDescriptions.Clear();
                _schemaObjectCollectionView.GroupDescriptions.Add(new PropertyGroupDescription("TypeDescription"));
            }
            // We do NOT assign ItemSource here; the RibbonMenu_SelectionChanged handler does that.
        }

        /// <summary>
        /// Returns the hardcoded list of tools for a given category.
        /// </summary>
        private List<string> GetToolsListForCategory(string category)
        {
            // Use a Dictionary or a robust switch expression for clear routing
            return category switch
            {
                // Category 1: Schema Explorer (Handled by different logic, but included here for completeness)
                "Schema Explorer" => new List<string>(),

                // Category 2: Performance (Each item loads a specific, distinct View)
                "Performance" => new List<string>
                    {
                        "Active Running Queries",
                        "Top Expensive Queries",
                        "Wait Statistics",
                        "Blocking Monitor"
                    },

                // Category 3: Security & User Management Tools
                "Security" => new List<string>
                    {
                        "User/Role/Permission Manager",
                        "Security Audit Log Viewer",
                        "Data Masking Manager"
                    },

                // Category 4: Maintenance (Includes Index Features)
                "Maintenance" => new List<string>
                    {
                        "Backup History & Restore",
                        "Job Scheduling/Agent Manager",
                        "Index Optimization Advisor",   
                        "Missing Index Recommendations"
                    },

                // Category 5: Configuration & Utility Tools
                "Configuration" => new List<string>
                    {
                        "Server/Database Configuration Editor"
                    },

                // Category 6: Design & Development Tools
                "Design & Dev" => new List<string>
                    {
                        "Schema Comparison and Synchronization",
                        "SQL Snippet/Template Library",
                        "Query Execution Plan Visualizer"
                    },

                // Category 7: High Availability Tools
                "High Availability" => new List<string>
                    {
                        "High Availability Status Dashboard"
                    },

                "General Info" => new List<string>
                    {
                        "Server & Database Details" 
                    },

            // Final Fallback
            _ => new List<string> { "No tools defined for this category." },
            };
        }

        // --- DYNAMIC CONTENT HOST ROUTING ---

        private void SchemaListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Reset tool selection to prevent conflicts
            ToolsListBox.SelectedItem = null;

            // 1. Get Selected Objects (Defensive Casting)
            // CRITICAL FIX: Reference SchemaListBox instead of the ambiguous name
            var selectedObjects = SchemaListBox.SelectedItems.Cast<object>().OfType<DatabaseObject>().ToList();
            var selectedTables = selectedObjects.Where(o => o.Type.ToUpperInvariant() == "U").ToList();

            // 2. --- Multi-Select Logic (FK Check) ---
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

        private void ToolsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Clear schema selection to prevent conflicts
            SchemaListBox.SelectedItem = null;

            if (ToolsListBox.SelectedItem is string selectedTool)
            {
                // Route the selected tool string to the diagnostic view loader
                LoadDiagnosticView(selectedTool);
            }
        }

        /// <summary>
        /// Loads the appropriate View based on the selected diagnostic tool string.
        /// </summary>
        private void LoadDiagnosticView(string viewName)
        {
            UserControl? newView = viewName switch
            {
                // Map the tool name from the ListBox to the correct View class
                "Activity & Storage" => new ActivityView(), // Use ActivityView as the default state if needed
                "Performance" => new PerformanceView(),
                "Top Query Analysis" => new PerformanceView(), // PerformanceView handles query analysis
                //"Index Optimization Advisor" => new IndexView(),
                "Security Permissions" => new SecurityView(),
                "Active Running Queries" => new ActiveQueriesView(),
                "Top Expensive Queries" => new ExpensiveQueriesView(),
                "Wait Statistics" => new WaitStatsView(),
                "Blocking Monitor" => new BlockingChainView(),
                "User/Role/Permission Manager" => new UserRoleView(),
                "Security Audit Log Viewer" => new AuditLogView(),
                "Data Masking Manager" => new Views.DataMaskingView(),
                "Server & Database Details" => new ServerInfoView(),
                "Backup History & Restore" => new Views.BackupRestoreView(),
                "Job Scheduling/Agent Manager" => new Views.JobManagerView(),
                "Index Optimization Advisor" => new Views.IndexOptimizationView(),
                "Missing Index Recommendations" => new Views.MissingIndexView(),
                // Add cases for Backup, Config, etc.
                _ => null,
            };

            // Fallback ensures ContentHost always receives a valid object
            MainContentHost.Content = newView ?? new UserControl { Content = new TextBlock { Text = $"Select a tool from the {viewName} category." } };
        }

        /// <summary>
        /// Loads the appropriate Metadata View based on the selected DatabaseObject type.
        /// </summary>
        private void LoadMetadataView(List<DatabaseObject> selectedObjects, bool isMultiSelect)
        {
            UserControl? newView = null;

            if (isMultiSelect)
            {
                // MULTI-SELECT: Load Relationship View for FK comparison
                newView = new RelationshipsView(selectedObjects, isMultiSelect: true);
            }
            else if (selectedObjects.Count == 1)
            {
                DatabaseObject dbObject = selectedObjects.First();
                string objectType = dbObject.Type.ToUpperInvariant();

                if (objectType == "U" || objectType == "V")
                {
                    newView = new ColumnsView(dbObject);
                }
                else if (objectType == "P" || objectType.Contains("F"))
                {
                    newView = new CodeView(dbObject);
                }
            }

            MainContentHost.Content = newView ?? new UserControl { Content = new TextBlock { Text = "Select an object." } };
        }

        // --- SEARCH HANDLER ---

        private void SidebarSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_schemaObjectCollectionView is null) return;

            string filterText = SidebarSearchTextBox.Text.ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(filterText))
            {
                _schemaObjectCollectionView.Filter = null;
            }
            else
            {
                _schemaObjectCollectionView.Filter = (item) =>
                {
                    if (item is DatabaseObject dbObject)
                    {
                        return dbObject.Name.ToLowerInvariant().Contains(filterText) ||
                               dbObject.TypeDescription.ToLowerInvariant().Contains(filterText);
                    }
                    return false;
                };
            }
        }

        // === SYSTEM CONTROL HANDLERS ===

        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            SqlConnectionManager.Disconnect();
            MainWindow newMainWindow = new MainWindow();
            newMainWindow.Show();
            this.Close();
        }

        // Handlers for Custom Window Chrome
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

        private void WindowHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }
    }
}