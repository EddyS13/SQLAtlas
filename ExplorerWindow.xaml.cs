// ExplorerWindow.xaml.cs
using SQLAtlas.Data;
using SQLAtlas.Models;
using SQLAtlas.Services;
using SQLAtlas.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Reflection;

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
            VersionDateTextBlock.Text = $"Build: {DateTime.Now:yyyy-MM-dd}";

            //VersionNumberTextBlock.Text = "v0.8.5";
            //VersionDateTextBlock.Text = $"{DateTime.Now:yyyy-MM-dd}";

            CurrentDatabaseStatusBlock.Text = $"DB: {databaseName ?? "N/A"} on {serverName ?? "N/A"}";

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
                SidebarListBox.SelectedItem = null;

                string header = selectedTab.Header.ToString() ?? string.Empty;
                LoadSidebarContent(header);
            }
        }

        /// <summary>
        /// Loads the appropriate content into the sidebar ListBox based on the Ribbon category.
        /// </summary>
        private void LoadSidebarContent(string category)
        {
            SidebarListBox.SelectedItem = null; // Clear any previous selection

            // --- ROUTE OBJECTS VS TOOLS ---
            if (category == "Schema Explorer")
            {
                // Display the grouped schema list
                SidebarHeaderTextBlock.Text = "SELECT SCHEMA OBJECT";
                SidebarListBox.ItemsSource = _schemaObjectCollectionView;
                SidebarSearchTextBox.Visibility = Visibility.Visible;
            }
            else
            {
                // Display the fixed list of diagnostic tools
                SidebarHeaderTextBlock.Text = $"SELECT TOOL ({category.ToUpper()})";
                List<string> toolItems = GetToolsListForCategory(category);
                
                // CRITICAL FIX: Assign directly without grouping
                SidebarListBox.ItemsSource = toolItems;
                SidebarSearchTextBox.Visibility = Visibility.Collapsed;
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
            if (SidebarListBox.SelectedItem is DatabaseObject dbObject)
            {
                // Routing for selected database objects (Tables/Views/Procs)
                LoadMetadataView(new List<DatabaseObject> { dbObject }, isMultiSelect: false);
            }
            else if (SidebarListBox.SelectedItem is string selectedTool)
            {
                // Routing for selected diagnostic tool
                LoadDiagnosticView(selectedTool);
            }
            // If neither, do nothing - user hasn't selected anything yet
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
                //"Index Optimization Advisor" => new IndexView(), -- THIS NEEDS TO BE DELETED
                _ => null,
            };

            // Fallback ensures ContentHost always receives a valid object
            MainContentHost.Content = newView ?? new UserControl { Content = new TextBlock { Text = $"View for '{viewName}' not found." } };
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