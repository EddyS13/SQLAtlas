// Views/ActivityView.xaml.cs

using System.Windows.Controls;
using System.Windows;
using SQLAtlas.Services;
using System;
using System.Threading.Tasks;
using System.Windows.Input; // Required for MouseWheelEventArgs
using System.Linq; // Required for LINQ extensions
using System.Windows.Media; // Required for VisualTreeHelper

namespace SQLAtlas.Views
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
            // Auto-refresh on first load (using null casts to satisfy C# NRT warnings)
            RefreshActivityButton_Click((object?)null, (RoutedEventArgs?)null);
        }

        // 3. Primary refresh logic (must be async)
        private async void RefreshActivityButton_Click(object? sender, RoutedEventArgs? e)
        {
            RefreshActivityButton.Content = "FETCHING ACTIVITY DATA...";
            RefreshActivityButton.IsEnabled = false;

            try
            {
                // --- Activity Data (Run asynchronously to prevent UI freeze) ---
                var activityList = await Task.Run(() => _metadataService.GetDatabaseActivity());
                ActivityDataGrid.ItemsSource = activityList;

                // --- Database Space Info ---
                var spaceInfoList = await Task.Run(() => _metadataService.GetDatabaseSpaceInfo());
                DatabaseSpaceDataGrid.ItemsSource = spaceInfoList;

                // --- Plan Cache Size ---
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

        // 4. Mouse Scroll Handler (Fixes Issue 3: In-Grid Scrolling)
        private void DataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // This handler is applied to all DataGrids in ActivityView.xaml
            if (sender is DataGrid dataGrid)
            {
                // Find the internal ScrollViewer within the DataGrid's visual tree
                // NOTE: This logic assumes a FindVisualChildren helper is accessible or uses standard traversal.
                // For simplicity, we use standard traversal (though less robust than a helper extension):
                DependencyObject parent = VisualTreeHelper.GetParent(dataGrid);
                while (parent != null && !(parent is ScrollViewer))
                {
                    parent = VisualTreeHelper.GetParent(parent);
                }

                if (parent is ScrollViewer scrollViewer)
                {
                    // Propagate the scroll event
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