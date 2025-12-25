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
    public partial class ActiveQueriesView : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();

        public ActiveQueriesView()
        {
            InitializeComponent();
            this.Loaded += ActiveQueriesView_Loaded;
        }

        private void ActiveQueriesView_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshPerformanceButton_Click(null, null);
        }

        private async void RefreshPerformanceButton_Click(object? sender, RoutedEventArgs? e)
        {
            RefreshPerformanceButton.Content = "ANALYZING...";
            RefreshPerformanceButton.IsEnabled = false;

            try
            {
                var queryList = await Task.Run(() => _metadataService.GetLongRunningQueries());

                Dispatcher.Invoke(() => {
                    ActiveQueriesDataGrid.ItemsSource = queryList;
                    NoQueriesText.Visibility = (queryList == null || queryList.Count == 0) ? Visibility.Visible : Visibility.Collapsed;
                    RefreshPerformanceButton.Content = "REFRESH";
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Performance Error: {ex.Message}");
                RefreshPerformanceButton.Content = "RETRY";
            }
            finally
            {
                RefreshPerformanceButton.IsEnabled = true;
            }
        }

        private async void KillSession_Click(object sender, RoutedEventArgs e)
        {
            // 1. Identify the session to kill
            var button = (Button)sender;
            // Assuming your model for the grid rows is 'ActiveQuery' or similar
            dynamic row = button.DataContext;
            int spid = row.SessionId;

            var result = MessageBox.Show($"Are you sure you want to kill Session ID {spid}?",
                                        "Terminate Process", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await Task.Run(() => _metadataService.KillSession(spid));
                    RefreshPerformanceButton_Click(null, null); // Refresh after kill
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to kill session: {ex.Message}");
                }
            }
        }
    }
}