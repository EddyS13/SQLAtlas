// ExplorerWindow.xaml.cs
using SQLAtlas.Data;
using SQLAtlas.Models;
using SQLAtlas.Services;
using SQLAtlas.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace SQLAtlas
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

            // 1. Initial Data Setup
            _allGroupedObjects = groupedObjects ?? new Dictionary<string, List<DatabaseObject>>();
            
            int objectCount = _allGroupedObjects.Sum(g => g.Value.Count);
            if (objectCount == 0)
            {
                MessageBox.Show("Warning: No database objects were loaded. The sidebar will be empty.", "Data Load Warning");
            }
            
            LoadSchemaCollection(_allGroupedObjects);

            // 2. Set Status Bar and Version Info
            Version? appVersion = Assembly.GetExecutingAssembly().GetName().Version;
            VersionNumberTextBlock.Text = $"v{appVersion?.ToString() ?? "N/A"}";
            VersionDateTextBlock.Text = "2025.12.25";

            CurrentDatabaseNameBlock.Text = databaseName ?? "N/A";
            CurrentServerNameBlock.Text = serverName ?? "N/A";

            // 3. Set Initial UI State
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
            _schemaObjectCollectionView = CollectionViewSource.GetDefaultView(flatList);

            if (_schemaObjectCollectionView != null && _schemaObjectCollectionView.CanGroup)
            {
                _schemaObjectCollectionView.GroupDescriptions.Clear();
                _schemaObjectCollectionView.SortDescriptions.Clear();

                // 1. Sort the folders (Groups) alphabetically
                _schemaObjectCollectionView.SortDescriptions.Add(new SortDescription("TypeDescription", ListSortDirection.Ascending));

                // 2. Sort the items INSIDE the folders alphabetically by name
                _schemaObjectCollectionView.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));

                _schemaObjectCollectionView.GroupDescriptions.Add(new PropertyGroupDescription("TypeDescription"));
            }
        }

        // --- RIBBON MENU ROUTING ---

        /// <summary>
        /// Handles the change of the top Ribbon Menu context to update the sidebar.
        /// </summary>
        private void RibbonMenu_Loaded(object sender, RoutedEventArgs e)
        {
            // Force the first tab to be selected and the logic to run
            RibbonMenu.SelectedIndex = 0;
            var firstTab = RibbonMenu.SelectedItem as TabItem;
            if (firstTab != null)
            {
                string? headerText = firstTab.Header?.ToString();
                if (!string.IsNullOrEmpty(headerText))
                {
                    LoadSidebarContent(headerText);
                }
            }
        }

        private void RibbonMenu_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Ensure this ONLY runs for the TabControl, not the ListBox inside it
            if (e.OriginalSource is TabControl)
            {
                if (RibbonMenu.SelectedItem is TabItem selectedTab)
                {
                    string? tabName = selectedTab.Header?.ToString();
                    if (!string.IsNullOrEmpty(tabName))
                    {
                        LoadSidebarContent(tabName);
                        UpdateMainView(tabName);
                    }
                }
            }
        }

        private void UpdateMainView(string tabName)
        {
            switch (tabName)
            {
                case "Performance":
                    MainContentHost.Content = new Views.PerformanceOverview();
                    break;
                case "Schema Explorer":
                    MainContentHost.Content = null;
                    break;
                default:
                    MainContentHost.Content = new TextBlock { Text = tabName + " View", Foreground = Brushes.White };
                    break;
            }
        }

        /// <summary>
        /// Loads the appropriate content into the sidebar ListBox based on the Ribbon category.
        /// </summary>
        private void LoadSidebarContent(string category)
        {
            if (string.IsNullOrEmpty(category))
                return;

            SidebarListBox.SelectedItem = null; // Clear any previous selection

            // --- ROUTE OBJECTS VS TOOLS ---
            if (category == "Schema Explorer")
            {
                // Display the grouped schema list
                SidebarHeaderTextBlock.Text = "SELECT SCHEMA OBJECT";
                SidebarListBox.ItemsSource = _schemaObjectCollectionView;
                SearchBoxContainer.Visibility = Visibility.Visible;
            }
            else
            {
                // Display the fixed list of diagnostic tools
                SidebarHeaderTextBlock.Text = $"SELECT TOOL ({category.ToUpper()})";
                List<string> toolItems = GetToolsListForCategory(category);
                
                // CRITICAL FIX: Assign directly without grouping
                SidebarListBox.ItemsSource = toolItems;
                SearchBoxContainer.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Returns the hardcoded list of tools for a given category.
        /// </summary>
        private List<string> GetToolsListForCategory(string category)
        {
            return category switch
            {
                "Performance" => new List<string> 
                { 
                    "Active Running Queries",
                    "Top Expensive Queries",
                    "Wait Statistics",
                    "Blocking Monitor"
                },
                "Security" => new List<string> 
                { 
                    "User/Role/Permission Manager",
                    "Security Audit Log Viewer",
                    "Data Masking Manager"
                },
                "Maintenance" => new List<string> 
                { 
                    "Backup History & Restore",
                    "Job Scheduling/Agent Manager",
                    "Index Optimization Advisor",
                    "Missing Index Recommendations"
                },
                "Configuration" => new List<string> 
                {
                    "Server & Database Details",
                    "Server/Database Configuration Editor",
                    "Drive Space and Growth Monitor"
                },
                "Design and Dev" => new List<string> 
                { 
                    "Schema Comparison & Synchronization",
                    "SQL Snippet/Template Library",
                    "Query Execution Plan Visualizer"
                },
                "High Availability" => new List<string> 
                { 
                    "High Availability Status Dashboard",
                },
                _ => new List<string> { "No tools available." },
            };
        }

        // --- DYNAMIC CONTENT HOST ROUTING ---

        /// <summary>
        /// Routes the selected sidebar item to the main Content Host.
        /// </summary>
        private void SidebarListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 1. Get ALL selected items from the ListBox
            var selectedItems = SidebarListBox.SelectedItems.Cast<object>().ToList();

            if (selectedItems.Count > 0)
            {
                // 2. Check if we are dealing with DatabaseObjects (Tables/Procs)
                if (selectedItems.All(x => x is DatabaseObject))
                {
                    var dbObjects = selectedItems.Cast<DatabaseObject>().ToList();

                    // Pass the whole list. isMultiSelect is true if count > 1
                    LoadMetadataView(dbObjects, isMultiSelect: dbObjects.Count > 1);
                }
                // 3. Otherwise, handle Tool selection (Diagnostics)
                else if (SidebarListBox.SelectedItem is string selectedTool)
                {
                    LoadDiagnosticView(selectedTool);
                }
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
                // Performance Tools
                "Active Running Queries" => new ActiveQueriesView(),
                "Top Expensive Queries" => new ExpensiveQueriesView(),
                "Wait Statistics" => new WaitStatsView(),
                "Blocking Monitor" => new BlockingChainView(),
                // Security Tools
                "User/Role/Permission Manager" => new Views.UserRoleView(),
                "Security Audit Log Viewer" => new Views.AuditLogView(),
                "Data Masking Manager" => new Views.DataMaskingView(),
                //Maintenance Tools
                "Index Optimization Advisor" => new Views.IndexOptimizationView(),
                "Missing Index Recommendations" => new Views.MissingIndexView(),
                "Backup History & Restore" => new Views.BackupRestoreView(),
                "Job Scheduling/Agent Manager" => new Views.JobManagerView(),
                //Configuration Tools
                "Server & Database Details" => new ServerInfoView(),
                "Server/Database Configuration Editor" => new ConfigurationEditorView(),
                "Drive Space and Growth Monitor" => new Views.DriveSpaceView(),
                //Design and Dev Tools
                "Schema Comparison & Synchronization" => new Views.SchemaCompareView(),
                "SQL Snippet/Template Library" => new Views.SnippetLibraryView(),
                "Query Execution Plan Visualizer" => new Views.QueryPlanView(),
                //High Availability Tools
                "High Availability Status Dashboard" => new Views.HighAvailabilityView(),
                // MISC TOOLS              
                "Activity & Storage" => new ActivityView(),
                "Security Permissions" => new SecurityView(),
                "Performance" => new PerformanceView(),
                "Top Query Analysis" => new PerformanceView(),
                _ => null,
            };

            // Fallback ensures ContentHost always receives a valid object
            MainContentHost.Content = newView ?? new UserControl 
            { 
                Content = new TextBlock 
                { 
                    Text = $"View for '{viewName}' not found.",
                    Foreground = Brushes.White
                } 
            };
        }

        /// <summary>
        /// Loads the appropriate Metadata View based on the selected DatabaseObject type.
        /// </summary>
        private void LoadMetadataView(List<DatabaseObject> selectedObjects, bool isMultiSelect)
        {
            UserControl? newView = null;

            // CASE: MULTI-SELECT (Comparison Mode)
            if (selectedObjects.Count == 2)
            {
                // If both are tables/views, open SchemaExplorer in Comparison Mode
                newView = new SchemaExplorer(selectedObjects);
            }
            // CASE: SINGLE SELECT
            else if (selectedObjects.Count == 1)
            {
                DatabaseObject dbObject = selectedObjects.First();
                string type = dbObject.Type.ToUpperInvariant();

                if (type == "U" || type == "V" || type == "TABLES" || type == "VIEWS")
                {
                    newView = new SchemaExplorer(dbObject);
                }
                else if (type == "P" || type == "FN" || type == "TF" || type == "IF" ||
                         type == "STORED PROCEDURES" || type == "SCALAR FUNCTIONS")
                {
                    newView = new CodeView(dbObject);
                }
            }

            // Assign the view or show the error state
            MainContentHost.Content = newView ?? new UserControl
            {
                Content = new TextBlock
                {
                    Text = selectedObjects.Count > 2
                        ? "Please select only 2 tables for comparison."
                        : $"Metadata view not found for: {selectedObjects.FirstOrDefault()?.TypeDescription}",
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
        }

        private void SidebarListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // If Ctrl or Shift is held, let the default WPF multi-select logic handle it
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                return;
            }

            // Otherwise, find the item and select it normally
            Visual visual = (Visual)e.OriginalSource;
            ListBoxItem? item = FindAncestor<ListBoxItem>(visual);

            if (item != null)
            {
                // Only force selection if we aren't trying to multi-select
                item.IsSelected = true;
            }
        }

        // Helper to climb the visual tree
        private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            do
            {
                if (current is T ancestor) return ancestor;
                current = VisualTreeHelper.GetParent(current);
            } while (current != null);
            return null;
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