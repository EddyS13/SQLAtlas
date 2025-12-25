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
    public partial class ConfigurationEditorView : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();

        public ConfigurationEditorView()
        {
            InitializeComponent();
            this.Loaded += ConfigurationEditorView_Loaded;
        }

        private void ConfigurationEditorView_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshConfigButton_Click(null, null);
        }

        private async void RefreshConfigButton_Click(object? sender, RoutedEventArgs? e)
        {
            if (RefreshConfigButton is null || ServerConfigGrid is null) return;

            RefreshConfigButton.Content = "FETCHING CONFIGURATION...";
            RefreshConfigButton.IsEnabled = false;

            try
            {
                var settings = await Task.Run(() => _metadataService.GetConfigurableSettings());
                ServerConfigGrid.ItemsSource = settings;

                var dbSettings = await Task.Run(() => _metadataService.GetDatabaseConfiguration());
                DatabaseConfigGrid.ItemsSource = dbSettings;

                RefreshConfigButton.Content = $"Settings Refreshed ({DateTime.Now:T})";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to retrieve server configuration: {ex.Message}", "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Error);
                RefreshConfigButton.Content = "Refresh Failed";
            }
            finally
            {
                RefreshConfigButton.IsEnabled = true;
            }
        }

        private void ShowConfigNotesButton_Click(object sender, RoutedEventArgs e)
        {
            GuidanceHeader.Text = "SERVER OPTIMIZATION";
            GuidanceContent.Text = "1. MAX SERVER MEMORY\nReserve 10-20% for the OS to prevent paging.\n\n" +
                                   "2. MAXDOP\nSet to 8 or lower for most workloads.\n\n" +
                                   "3. COST THRESHOLD\nIncrease to 50 to avoid tiny queries using all CPUs.\n\n" +
                                   "4. FILL FACTOR\nSet to 90 for high-insert tables.";

            GuidancePanel.Visibility = Visibility.Visible;
        }

        private void ShowDatabaseGuidanceButton_Click(object sender, RoutedEventArgs e)
        {
            GuidanceHeader.Text = "DATABASE BEST PRACTICES";
            GuidanceContent.Text = "1. RECOVERY MODEL\nUse FULL for production, SIMPLE for Dev/Test.\n\n" +
                                   "2. AUTO-CLOSE\nAlways keep OFF to prevent CPU spikes.\n\n" +
                                   "3. AUTO-SHRINK\nAlways keep OFF to prevent fragmentation.";

            GuidancePanel.Visibility = Visibility.Visible;
        }

        private void CloseGuidance_Click(object sender, RoutedEventArgs e)
        {
            GuidancePanel.Visibility = Visibility.Collapsed;
        }
    }
}
