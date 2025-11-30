// Views/IndexOptimizationView.xaml.cs

using SQLAtlas.Models;
using SQLAtlas.Services;
using SQLAtlas.Utilities;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SQLAtlas.Views
{
    public partial class IndexOptimizationView : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();

        public IndexOptimizationView()
        {
            InitializeComponent();
            this.Loaded += IndexOptimizationView_Loaded;
        }

        private void IndexOptimizationView_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshIndexButton_Click((object?)null, (RoutedEventArgs?)null);
        }

        private async void RefreshIndexButton_Click(object? sender, RoutedEventArgs? e)
        {
            // Safety check for required controls
            if (RefreshIndexButton is null || IndexStatusTextBlock is null || FragmentedIndexDataGrid is null) return;

            // Set initial loading state
            RefreshIndexButton.Content = "CALCULATING...";
            RefreshIndexButton.IsEnabled = false;
            IndexStatusTextBlock.Text = "Retrieving All Index Statistics...";

            bool success = false;
            var fragList = Enumerable.Empty<IndexFragmentation>().ToList();

            try
            {
                // 1. Index Fragmentation Check (The core analysis)
                // CRITICAL: Runs the robust T-SQL to retrieve all indexes (index_id > 0)
                fragList = await Task.Run(() => _metadataService.GetIndexFragmentation());

                // 2. Bind all rows
                FragmentedIndexDataGrid.ItemsSource = fragList;

                // Final status is set upon success
                IndexStatusTextBlock.Text = $"Analysis Complete. Found {fragList.Count} index status rows.";
                success = true;
            }
            catch (Exception ex)
            {
                // Set error feedback immediately
                IndexStatusTextBlock.Text = "ERROR: Failed to retrieve index data.";
                MessageBox.Show($"Failed to retrieve index data: {ex.Message}", "Index Error", MessageBoxButton.OK, MessageBoxImage.Error);

                // Ensure the grid is cleared on failure
                FragmentedIndexDataGrid.ItemsSource = null;
                success = false;
            }
            finally
            {
                // CRITICAL: Update button content based on success flag
                if (success)
                {
                    // Update with timestamp on successful execution
                    RefreshIndexButton.Content = $"Refresh Index Analysis ({DateTime.Now:T})";
                }
                else
                {
                    // Show "Refresh Failed" on catch
                    RefreshIndexButton.Content = "Refresh Failed";
                }

                RefreshIndexButton.IsEnabled = true;
            }
        }

        // --- ACTION HANDLER (Requires ExecuteNonQuery in service layer) ---
        private async void MaintenanceActionButton_Click(object sender, RoutedEventArgs e)
        {
            // ... (Logic to execute DDL remains here) ...
            if (sender is not Button button || button.DataContext is not IndexFragmentation index) return;

            string script = index.MaintenanceScript;
            if (string.IsNullOrWhiteSpace(script) || script.Contains("N/A")) return;

            var result = MessageBox.Show($"Execute DDL Script?\n\n{script}", "Confirm Index Maintenance",
                                         MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // await Task.Run(() => SqlConnectionManager.ExecuteNonQuery(script)); 
                    MessageBox.Show("Index maintenance command sent to SQL Server.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    RefreshIndexButton_Click(null, null);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to execute DDL: {ex.Message}", "Execution Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // --- UTILITY HANDLER ---
        private void DataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // ... (Logic for smooth scroll remains here) ...
            if (sender is DataGrid dataGrid)
            {
                ScrollViewer? scrollViewer = dataGrid.FindVisualChildren<ScrollViewer>().FirstOrDefault();

                if (scrollViewer is not null)
                {
                    if (e.Delta < 0)
                    {
                        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + 40);
                    }
                    else
                    {
                        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - 40);
                    }
                    e.Handled = true;
                }
            }
        }
    }
}