using SQLAtlas.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SQLAtlas.Views
{
    public partial class ConfigurationOverview : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();

        public ConfigurationOverview()
        {
            InitializeComponent();
            // Ensure data loads every time the view is navigated to
            this.Loaded += (s, e) => LoadServerDetails();
        }

        private async void LoadServerDetails()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("DEBUG: Start Loading Server Details...");

                // 1. Fetch data
                var details = await Task.Run(() => _metadataService.GetServerInformation());

                if (details == null)
                {
                    System.Diagnostics.Debug.WriteLine("DEBUG: Details came back NULL from Service.");
                    Dispatcher.Invoke(() => AdvisorTitle.Text = "No Data Found");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"DEBUG: Data Found! Server: {details.ServerName}, OS: {details.OSVersion}");

                // 2. Map to local variables
                var idList = new List<KeyValuePair<string, string>> {
            new KeyValuePair<string, string>("Machine", details.ServerName),
            new KeyValuePair<string, string>("Edition", details.Edition),
            new KeyValuePair<string, string>("OS Version", details.OSVersion),
            new KeyValuePair<string, string>("Collation", details.Collation),
            new KeyValuePair<string, string>("Patch Level", details.Level)
        };

                var resList = new List<KeyValuePair<string, string>> {
            new KeyValuePair<string, string>("CPU Cores", details.CpuCount.ToString()),
            new KeyValuePair<string, string>("Physical RAM", $"{details.PhysicalRamGB} GB"),
            new KeyValuePair<string, string>("Hypervisor", details.Hypervisor),
            new KeyValuePair<string, string>("Uptime", details.UptimeDisplay)
        };

                // 3. Force UI Update
                Dispatcher.BeginInvoke(new Action(() => {
                    VersionHeaderTextBlock.Text = details.Version;
                    ServerIdentityList.ItemsSource = idList;
                    EnvironmentStatsList.ItemsSource = resList;

                    // Advisor Update
                    AdvisorStatusIcon.Text = details.PhysicalRamGB < 8 ? "⚠️" : "✅";
                    AdvisorTitle.Text = details.PhysicalRamGB < 8 ? "Resource Limit" : "Healthy";
                    AdvisorDesc.Text = details.PhysicalRamGB < 8
                        ? "RAM is below 8GB. SQL Server may face memory pressure."
                        : "System hardware meets standard enterprise recommendations.";

                    System.Diagnostics.Debug.WriteLine("DEBUG: UI Update Invoiced.");
                }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DEBUG: CRITICAL ERROR: {ex.Message}");
            }
        }

        private void ToolBlock_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                // This assumes your main window is named ExplorerWindow
                var parent = Window.GetWindow(this) as ExplorerWindow;
                parent?.LoadDiagnosticView(btn.Tag.ToString());
            }
        }
    }
}