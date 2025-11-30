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
            string notes = "--- Configuration Optimization Notes ---\n\n" +
                           "1. max server memory (MB):\n   - Problem: Set too high/low.\n   - Goal: Reserve 10-20% of physical RAM for OS.\n\n" +
                           "2. max degree of parallelism:\n   - Problem: 0 (unlimited) or high.\n   - Goal: Set to 8 or less to prevent CPU saturation.\n\n" +
                           "3. cost threshold for parallelism:\n   - Problem: 5 (default).\n   - Goal: Raise to 25-70 to improve CPU efficiency by stopping cheap parallel queries.\n\n" +
                           "4. fillfactor:\n   - Problem: 100 (default).\n   - Goal: Set lower (e.g., 90) to reduce page splits and improve update performance.";

            MessageBox.Show(notes, "Configuration Optimization Notes", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ShowDatabaseGuidanceButton_Click(object sender, RoutedEventArgs e)
        {
            string notes = "--- Database Configuration Guidance ---\n\n" +
                           "1. Recovery Model:\n   - Check: Is it FULL for production databases?\n   - Warning: SIMPLE recovery model risks losing data since the last full/differential backup.\n\n" +
                           "2. Auto Close:\n   - Best Practice: Should be set to OFF.\n   - Warning: Turning this ON severely impacts performance due to frequent database startup/shutdown cycles.\n\n" +
                           "3. Auto Shrink:\n   - Best Practice: Should be set to OFF.\n   - Warning: Frequent shrinking causes index fragmentation and performance degradation.";

            MessageBox.Show(notes, "Database Configuration Guidance", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
