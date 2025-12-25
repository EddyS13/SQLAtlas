using SQLAtlas.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SQLAtlas.Views
{
    /// <summary>
    /// Interaction logic for ServerInfoView.xaml
    /// </summary>
    public partial class ServerInfoView : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();

        public ServerInfoView()
        {
            InitializeComponent();
            this.Loaded += View_Loaded;
        }

        private void View_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshServerInfoButton_Click(null, null);
        }

        private async void RefreshServerInfoButton_Click(object? sender, RoutedEventArgs? e)
        {
            RefreshServerInfoButton.Content = "FETCHING...";
            RefreshServerInfoButton.IsEnabled = false;

            try
            {
                // Use your existing service method
                var details = await Task.Run(() => _metadataService.GetServerInformation());

                if (details != null)
                {
                    ServerHeaderInfo.Text = details.ServerName;
                    VersionTxt.Text = details.Version;
                    EditionTxt.Text = details.Edition;
                    LevelTxt.Text = $"Patch Level: {details.Level}";
                    CpuTxt.Text = details.CpuCount.ToString();
                    RamTxt.Text = $"{details.PhysicalRamGB} GB";
                    UptimeTxt.Text = details.UptimeDisplay;

                    // Simple Advisor logic
                    if (details.PhysicalRamGB < 8)
                    {
                        AdvisorTitle.Text = "⚠️ Resource Constraint";
                        AdvisorDesc.Text = "Server RAM is below 8GB. SQL Server may struggle with buffer pool cache management.";
                    }
                    else
                    {
                        AdvisorTitle.Text = "✅ Configuration Healthy";
                        AdvisorDesc.Text = "Hardware specs meet enterprise standards. Proceed to Configuration Editor for tuning.";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
            finally
            {
                RefreshServerInfoButton.Content = "REFRESH INFO";
                RefreshServerInfoButton.IsEnabled = true;
            }
        }
    }
}
