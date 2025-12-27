using SQLAtlas.Data;
using SQLAtlas.Models;
using SQLAtlas.Services;
using SQLAtlas.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace SQLAtlas
{
    public partial class ExplorerWindow : Window
    {
        private readonly MetadataService _metadataService = new MetadataService();
        private readonly Dictionary<string, List<DatabaseObject>> _allGroupedObjects = new();
        private ICollectionView? _schemaObjectCollectionView;
        private readonly MetadataService _service = new MetadataService();

        public ExplorerWindow(Dictionary<string, List<DatabaseObject>>? groupedObjects,
                             string? serverName,
                             string? databaseName)
        {
            InitializeComponent();

            // 1. Data Setup & Validation
            _allGroupedObjects = groupedObjects ?? new Dictionary<string, List<DatabaseObject>>();
            if (_allGroupedObjects.Sum(g => g.Value.Count) == 0)
            {
                MessageBox.Show("Warning: No database objects were loaded.", "Data Load Warning");
            }
            LoadSchemaCollection(_allGroupedObjects);

            // 2. Status Bar & Versioning
            Version? appVersion = Assembly.GetExecutingAssembly().GetName().Version;
            VersionNumberTextBlock.Text = $"v{appVersion?.ToString() ?? "N/A"}";
            VersionDateTextBlock.Text = "2025.12.25";
            CurrentDatabaseNameBlock.Text = databaseName ?? "N/A";
            CurrentServerNameBlock.Text = serverName ?? "N/A";

            // 3. Set Default Landing Page (Dashboard)
            RibbonMenu.SelectedIndex = 0;
        }

        private void LoadSchemaCollection(Dictionary<string, List<DatabaseObject>> groupedObjects)
        {
            var flatList = groupedObjects.SelectMany(g => g.Value).ToList();
            _schemaObjectCollectionView = CollectionViewSource.GetDefaultView(flatList);

            if (_schemaObjectCollectionView != null && _schemaObjectCollectionView.CanGroup)
            {
                _schemaObjectCollectionView.GroupDescriptions.Clear();
                _schemaObjectCollectionView.SortDescriptions.Clear();
                _schemaObjectCollectionView.SortDescriptions.Add(new SortDescription("TypeDescription", ListSortDirection.Ascending));
                _schemaObjectCollectionView.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
                _schemaObjectCollectionView.GroupDescriptions.Add(new PropertyGroupDescription("TypeDescription"));
            }
        }

        // --- NAVIGATION ROUTING (Efficiency Update) ---

        private void RibbonMenu_Loaded(object sender, RoutedEventArgs e) => TriggerTabRouting();

        private void TriggerTabRouting()
        {
            if (RibbonMenu.SelectedItem is TabItem selectedTab)
            {
                string tabName = selectedTab.Header?.ToString() ?? "";

                // Toggle Sidebar Visibility: Hidden for Dashboard, Visible for others
                bool isDashboard = tabName == "Dashboard";
                SidebarColumn.Width = isDashboard ? new GridLength(0) : new GridLength(280);
                SidebarBorder.Visibility = isDashboard ? Visibility.Collapsed : Visibility.Visible;

                if (isDashboard)
                {
                    MainContentHost.Content = new DashboardView();
                }
                else
                {
                    LoadSidebarContent(tabName);
                    UpdateModuleOverview(tabName);
                }
            }
        }

        private void UpdateModuleOverview(string tabName)
        {
            MainContentHost.Content = tabName switch
            {
                "Design and Dev" => new DesignDevOverview(),
                "Performance" => new PerformanceOverview(),
                "Schema Explorer" => null,
                _ => new TextBlock { Text = $"{tabName} Overview", Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            };
        }

        private void LoadSidebarContent(string category)
        {
            SidebarListBox.SelectedItem = null;
            if (category == "Schema Explorer")
            {
                SidebarHeaderTextBlock.Text = "SELECT SCHEMA OBJECT";
                SidebarListBox.ItemsSource = _schemaObjectCollectionView;
                SearchBoxContainer.Visibility = Visibility.Visible;
            }
            else
            {
                SidebarHeaderTextBlock.Text = $"SELECT TOOL ({category.ToUpper()})";
                SidebarListBox.ItemsSource = GetToolsListForCategory(category);
                SearchBoxContainer.Visibility = Visibility.Collapsed;
            }
        }

        private List<string> GetToolsListForCategory(string cat) => cat switch
        {
            "Performance" => new() { "Active Running Queries", "Top Expensive Queries", "Wait Statistics", "Blocking Monitor" },
            "Security" => new() { "User/Role/Permission Manager", "Security Audit Log Viewer", "Data Masking Manager" },
            "Maintenance" => new() { "Backup History & Restore", "Job Scheduling/Agent Manager", "Index Optimization Advisor", "Missing Index Recommendations" },
            //"Configuration" => new() { "Server/Database Configuration Editor", "Drive Space and Growth Monitor" },
            "Configuration" => new() { "Server & Database Details", "Server/Database Configuration Editor", "Drive Space and Growth Monitor" },
            "Design and Dev" => new() { "Schema Comparison", "SQL Snippet/Template Library", "Query Execution Plan Visualizer" },
            "High Availability" => new() { "High Availability Status Dashboard" },
            _ => new() { "No tools available." }
        };

        // --- INTERACTION LOGIC ---

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

        public void LoadDiagnosticView(string tool)
        {
            MainContentHost.Content = tool switch
            {
                // Map the tool name from the ListBox to the correct View class
                // Performance Tools
                "Active Running Queries" => new ActiveQueriesView(),
                "Top Expensive Queries" => new ExpensiveQueriesView(),
                "Wait Statistics" => new WaitStatsView(),
                "Blocking Monitor" => new BlockingChainView(),
                // Security Tools
                "User/Role/Permission Manager" => new UserRoleView(),
                "Security Audit Log Viewer" => new AuditLogView(),
                "Data Masking Manager" => new DataMaskingView(),
                //Maintenance Tools
                "Index Optimization Advisor" => new IndexOptimizationView(),
                "Missing Index Recommendations" => new MissingIndexView(),
                "Backup History & Restore" => new BackupRestoreView(),
                "Job Scheduling/Agent Manager" => new JobManagerView(),
                //Configuration Tools
                "Server & Database Details" => new ServerInfoView(),
                "Server/Database Configuration Editor" => new ConfigurationEditorView(),
                "Drive Space and Growth Monitor" => new DriveSpaceView(),
                //Design and Dev Tools
                "Schema Comparison" => new SchemaCompareView(),
                "SQL Snippet/Template Library" => new SnippetLibraryView(),
                "Query Execution Plan Visualizer" => new QueryPlanView(),
                //High Availability Tools
                "High Availability Status Dashboard" => new HighAvailabilityView(),
                // MISC TOOLS              
                "Activity & Storage" => new ActivityView(),
                "Security Permissions" => new SecurityView(),
                "Performance" => new PerformanceView(),
                "Top Query Analysis" => new PerformanceView(),

                _ => new TextBlock { Text = $"View for {tool} is in development.", Foreground = Brushes.White }
            };
        }

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
                         type == "STORED PROCEDURES" || type == "SCALAR FUNCTIONS" || type == "TABLE FUNCTIONS")
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

        // --- HELPER METHODS (Visual Tree & Search) ---

        private void SidebarSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_schemaObjectCollectionView == null) return;
            string txt = SidebarSearchTextBox.Text.ToLower();
            _schemaObjectCollectionView.Filter = string.IsNullOrWhiteSpace(txt) ? null : (obj) =>
            {
                var dbObj = obj as DatabaseObject;
                return dbObj != null && (dbObj.Name.ToLower().Contains(txt) || dbObj.TypeDescription.ToLower().Contains(txt));
            };
        }

        private void SidebarListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) return;
            var item = FindAncestor<ListBoxItem>((Visual)e.OriginalSource);
            if (item != null) item.IsSelected = true;
        }

        private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null && !(current is T)) current = VisualTreeHelper.GetParent(current);
            return current as T;
        }

        // --- WINDOW CHROME ---

        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            SqlConnectionManager.Disconnect();
            new MainWindow().Show();
            this.Close();
        }

        private void RibbonMenu_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.OriginalSource is TabControl)
            {
                if (RibbonMenu.SelectedItem is TabItem selectedTab)
                {
                    string category = selectedTab.Header.ToString().Trim();

                    // 1. Toggle Sidebar Visibility (Your existing logic)
                    bool isDashboard = category == "Dashboard";
                    SidebarColumn.Width = isDashboard ? new GridLength(0) : new GridLength(280);
                    SidebarBorder.Visibility = isDashboard ? Visibility.Collapsed : Visibility.Visible;

                    // 2. Swap the Main Content
                    NavigateToCategory(category);

                    // 3. Update the Sidebar (Using YOUR existing method names)
                    LoadSidebarContent(category);
                }
            }
        }

        private void NavigateToCategory(string category)
        {
            // 1. Handle Specialized Pages
            if (category == "Dashboard") { MainContentHost.Content = new DashboardView(); return; }
            if (category == "Performance") { MainContentHost.Content = new PerformanceOverview(); return; }
            if (category == "Configuration") { MainContentHost.Content = new ConfigurationOverview(); return; }
            if (category == "Design and Dev") { MainContentHost.Content = new DesignDevOverview(); return; }

            // 2. Setup HubTemplate variables for Universal Hubs
            var liveStats = _service.GetHubOverviewStats();
            var hubStats = new Dictionary<string, string>();
            List<ToolShortcut> tools = new List<ToolShortcut>();
            string title = "";
            string sub = "";

            switch (category)
            {
                case "Security":
                    title = "SECURITY CENTER";
                    sub = "Manage database access, roles, and auditing.";
                    hubStats.Add("ADMINS", liveStats.GetValueOrDefault("Admins", "0"));
                    tools.Add(new ToolShortcut { Name = "User/Role/Permission Manager", Description = "View database principals, role memberships, and explicit permissions.", Icon = "👥" });
                    tools.Add(new ToolShortcut { Name = "Security Audit Log Viewer", Description = "Review the SQL Error Log filtered for security-related events and failed logins.", Icon = "🛡️" });
                    tools.Add(new ToolShortcut { Name = "Data Masking Manager", Description = "Identify potentially sensitive data that should be masked or encrypted.", Icon = "🔍" });
                    break;

                case "Maintenance":
                    title = "MAINTENANCE HUB";
                    sub = "Keep your databases healthy.";
                    hubStats.Add("LAST BACKUP", liveStats.GetValueOrDefault("LastBackup", "N/A"));
                    tools.Add(new ToolShortcut { Name = "Backup History & Restore", Description = "Monitor backup success rates and generate restore scripts from history.", Icon = "💾" });
                    tools.Add(new ToolShortcut { Name = "Job Scheduling/Agent Manager", Description = "View job outcomes, next scheduled runs, and step details.", Icon = "📅" });
                    tools.Add(new ToolShortcut { Name = "Index Optimization Advisor", Description = "Identify heavily fragmented indexes that require reorganize or rebuild operations.", Icon = "⚡" });
                    tools.Add(new ToolShortcut { Name = "Missing Index Recommendations", Description = "Review optimizer recommendations for new indexes to boost query performance.", Icon = "🧩" });
                    break;

                case "Schema Explorer":
                    var schemaStats = _service.GetHubOverviewStats();
                    title = "SCHEMA EXPLORER";
                    sub = "Analyze and manage database structures.";
                    hubStats.Add("TABLES", liveStats.GetValueOrDefault("Tables", "0"));
                    hubStats.Add("VIEWS", liveStats.GetValueOrDefault("Views", "0"));
                    hubStats.Add("STORED PROCS", liveStats.GetValueOrDefault("Stored Procs", "0"));
                    tools = new List<ToolShortcut>();
                    break;

                case "High Availability":
                    title = "HIGH AVAILABILITY";
                    sub = "Monitor AlwaysOn and Mirroring.";
                    hubStats.Add("REPLICAS", liveStats.GetValueOrDefault("Replicas", "0"));
                    tools.Add(new ToolShortcut { Name = "High Availability Status Dashboard", Description = "Monitor health.", Icon = "🌐" });
                    break;
            }

            // 3. Load the HubTemplate if a title was set
            if (!string.IsNullOrEmpty(title))
            {
                MainContentHost.Content = new HubTemplate(title, sub, hubStats, tools);
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e) => WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
        private void WindowHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
    }
}