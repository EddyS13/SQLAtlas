// Views/ActivityView.xaml.cs

using System.Windows.Controls;
using System.Windows;
using DatabaseVisualizer.Services;
using System;
using System.Threading.Tasks;
using System.Windows.Input; // Required
using DatabaseVisualizer.Utilities;

namespace DatabaseVisualizer.Views
{
    public partial class ActivityView : UserControl
    {
        // 1. Define the service field
        private readonly MetadataService _metadataService = new MetadataService();

        public ActivityView()
        {
            InitializeComponent();
            this.Loaded += ActivityView_Loaded;
        }

        // 2. Event handler to start refresh when the view is first loaded
        private void ActivityView_Loaded(object sender, RoutedEventArgs e)
        {
            // Auto-refresh on first load
            RefreshActivityButton_Click(null, null);
        }

        // 3. Primary refresh logic (must be async)
        private async void RefreshActivityButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshActivityButton.Content = "FETCHING ACTIVITY DATA...";
            RefreshActivityButton.IsEnabled = false;

            try
            {
                // --- Activity Data (Feature 1) ---
                var activityList = await Task.Run(() => _metadataService.GetDatabaseActivity());
                ActivityDataGrid.ItemsSource = activityList;

                // --- Database Space Info (Feature 3A) ---
                var spaceInfoList = await Task.Run(() => _metadataService.GetDatabaseSpaceInfo());
                DatabaseSpaceDataGrid.ItemsSource = spaceInfoList;

                // --- Plan Cache Size (Feature B) ---
                string cacheSize = await Task.Run(() => _metadataService.GetPlanCacheSize());
                PlanCacheSizeTextBlock.Text = cacheSize;

                RefreshActivityButton.Content = $"Activity Refreshed ({DateTime.Now:T})";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to retrieve activity/space data: {ex.Message}", "Activity Error", MessageBoxButton.OK, MessageBoxImage.Error);
                RefreshActivityButton.Content = "Refresh Failed";
            }
            finally
            {
                RefreshActivityButton.IsEnabled = true;
            }
        }

        private void DataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // This handler directs the mouse scroll event to the DataGrid's internal ScrollViewer.
            if (sender is DataGrid dataGrid)
            {
                var scrollViewer = dataGrid.FindVisualChildren<ScrollViewer>().FirstOrDefault();

                if (scrollViewer != null)
                {
                    if (e.Delta < 0)
                        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + 40);
                    else
                        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - 40);

                    e.Handled = true;
                }
            }
        }
    }
}