// Views/PerformanceView.xaml.cs

using DatabaseVisualizer.Models;
using DatabaseVisualizer.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace DatabaseVisualizer.Views
{
    public partial class PerformanceView : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();

        // Parameterless constructor for direct navigation (Tools Menu)
        public PerformanceView()
        {
            InitializeComponent();
            this.Loaded += PerformanceView_Loaded;
        }

        private void PerformanceView_Loaded(object sender, RoutedEventArgs e)
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
                // All service calls run asynchronously to prevent UI freeze

                // 1. Top 5 Active Requests
                var queryList = await Task.Run(() => _metadataService.GetLongRunningQueries());
                PerformanceDataGrid.ItemsSource = queryList;

                // 2. Expensive Cached Queries
                var expensiveQueries = await Task.Run(() => _metadataService.GetTopExpensiveQueries());
                ExpensiveQueriesDataGrid.ItemsSource = expensiveQueries;

                // 3. Wait Stats
                var waitStats = await Task.Run(() => _metadataService.GetTopWaits());
                WaitStatsDataGrid.ItemsSource = waitStats;

                // 4. Blocking Chain
                var blockingList = await Task.Run(() => _metadataService.GetCurrentBlockingChain());
                BlockingChainDataGrid.ItemsSource = blockingList;

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