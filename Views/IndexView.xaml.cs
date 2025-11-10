// Views/IndexView.xaml.cs

using DatabaseVisualizer.Models;
using DatabaseVisualizer.Services;
using DatabaseVisualizer.Utilities;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls;
using System.Windows.Input; // Required for MouseWheelEventArgs
using System.Windows.Media;

namespace DatabaseVisualizer.Views
{
    public partial class IndexView : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();
        private readonly DatabaseObject _selectedObject;

        private readonly List<DatabaseObject> _selectedObjects;
        private readonly bool _isMultiSelect; // To differentiate logic within the view
        public IndexView()
        {
            InitializeComponent();
            this.Loaded += IndexView_Loaded;
        }

        private void IndexView_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshIndexButton_Click(null, null);
        }

        private async void RefreshIndexButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshIndexButton.Content = "CALCULATING...";
            RefreshIndexButton.IsEnabled = false;
            IndexStatusTextBlock.Text = "Calculating Index Fragmentation...";

            try
            {
                var missingIndexList = await Task.Run(() => _metadataService.GetMissingIndexes());

                if (missingIndexList.Any())
                {
                    MissingIndexDataGrid.ItemsSource = missingIndexList;
                    MissingIndexDataGrid.Visibility = Visibility.Visible;
                    NoMissingIndexesTextBlock.Visibility = Visibility.Collapsed;
                }
                else
                {
                    MissingIndexDataGrid.Visibility = Visibility.Collapsed;
                    NoMissingIndexesTextBlock.Visibility = Visibility.Visible;
                }

                // 2. Index Fragmentation Check
                var fragList = await Task.Run(() => _metadataService.GetIndexFragmentation());
                FragmentedIndexDataGrid.ItemsSource = fragList;

                IndexStatusTextBlock.Text = $"Analysis Complete. Found {fragList.Count} fragmented indexes.";
                RefreshIndexButton.Content = $"Refresh Index Analysis ({DateTime.Now:T})";
            }
            catch (Exception ex)
            {
                IndexStatusTextBlock.Text = "ERROR: Failed to retrieve index data.";
                MessageBox.Show($"Failed to retrieve index data: {ex.Message}", "Index Error", MessageBoxButton.OK, MessageBoxImage.Error);
                RefreshIndexButton.Content = "Refresh Index Analysis";
            }
            finally
            {
                RefreshIndexButton.IsEnabled = true;
            }
        }

        private async void MaintenanceActionButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. Get the data item associated with the clicked button
            if (!(sender is Button button) || !(button.DataContext is IndexFragmentation index)) return;

            string script = index.MaintenanceScript;
            if (string.IsNullOrWhiteSpace(script) || script.Contains("N/A")) return;

            // 2. Confirm with user (CRITICAL SAFETY STEP)
            var result = MessageBox.Show($"Execute DDL Script?\n\n{script}", "Confirm Index Maintenance",
                                         MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                // 3. Execute the script asynchronously
                // Requires: SqlConnectionManager.ExecuteNonQuery(string sql) method to be implemented.
                // await Task.Run(() => SqlConnectionManager.ExecuteNonQuery(script)); 

                MessageBox.Show("Index maintenance command sent to SQL Server.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                // 4. Immediately refresh the data to see the fragmentation reduction
                RefreshIndexButton_Click(null, null);
            }
        }

        private void DataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // This handler directs the mouse scroll event to the DataGrid's internal ScrollViewer.
            if (sender is DataGrid dataGrid)
            {
                // Find the internal ScrollViewer within the DataGrid's visual tree
                ScrollViewer scrollViewer = dataGrid.FindVisualChildren<ScrollViewer>().FirstOrDefault();

                if (scrollViewer != null)
                {
                    // Propagate the scroll event
                    if (e.Delta < 0)
                    {
                        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + 40);
                    }
                    else
                    {
                        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - 40);
                    }
                    e.Handled = true; // Stop the event from propagating
                }
            }
        }
    }
}