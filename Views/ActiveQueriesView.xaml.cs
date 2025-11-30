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
    /// Interaction logic for ActiveQueriesView.xaml
    /// </summary>
    public partial class ActiveQueriesView : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();

        // Parameterless constructor for direct navigation (Tools Menu)
        public ActiveQueriesView()
        {
            InitializeComponent();
            this.Loaded += ActiveQueriesView_Loaded;
        }
        private void ActiveQueriesView_Loaded(object sender, RoutedEventArgs e)
        {
            // Auto-refresh on first load (using null casts to satisfy C# NRT warnings)
            RefreshPerformanceButton_Click((object?)null, (RoutedEventArgs?)null);
        }
        private async void RefreshPerformanceButton_Click(object? sender, RoutedEventArgs? e)
        {
            RefreshPerformanceButton.Content = "ANALYZING PERFORMANCE...";
            RefreshPerformanceButton.IsEnabled = false;

            try
            {
                var queryList = await Task.Run(() => _metadataService.GetLongRunningQueries());
                ActiveQueriesDataGrid.ItemsSource = queryList;

                RefreshPerformanceButton.Content = $"Performance Data Refreshed ({DateTime.Now:T})";
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
    }
}
