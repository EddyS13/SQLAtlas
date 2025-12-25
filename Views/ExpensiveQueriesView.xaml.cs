using SQLAtlas.Models;
using SQLAtlas.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SQLAtlas.Views
{
    public partial class ExpensiveQueriesView : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();

        public ExpensiveQueriesView()
        {
            InitializeComponent();
            this.Loaded += ExpensiveQueriesView_Loaded;
        }

        private void ExpensiveQueriesView_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshPerformanceButton_Click(null, null);
        }

        private async void RefreshPerformanceButton_Click(object? sender, RoutedEventArgs? e)
        {
            RefreshPerformanceButton.Content = "ANALYZING CACHE...";
            RefreshPerformanceButton.IsEnabled = false;

            try
            {
                var expensiveQueries = await Task.Run(() => _metadataService.GetTopExpensiveQueries());

                Dispatcher.Invoke(() => {
                    ExpensiveQueriesDataGrid.ItemsSource = expensiveQueries;
                    NoDataText.Visibility = (expensiveQueries == null || expensiveQueries.Count == 0) ? Visibility.Visible : Visibility.Collapsed;
                    RefreshPerformanceButton.Content = "REFRESH ANALYSIS";
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to retrieve expensive queries: {ex.Message}");
                RefreshPerformanceButton.Content = "RETRY";
            }
            finally
            {
                RefreshPerformanceButton.IsEnabled = true;
            }
        }

        private async void CopyQuery_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var row = button.DataContext as ExpensiveQuery;

            if (row != null && !string.IsNullOrEmpty(row.QueryText))
            {
                Clipboard.SetText(row.QueryText);

                // Visual Feedback
                button.Content = "OK!";
                button.Foreground = (SolidColorBrush)Application.Current.Resources["SuccessColor"];

                await Task.Delay(1500);

                button.Content = "COPY";
                button.Foreground = (SolidColorBrush)Application.Current.Resources["MutedFontColor"];
            }
        }
    }
}