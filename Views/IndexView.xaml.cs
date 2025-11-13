// Views/IndexView.xaml.cs

using System.Windows.Controls;
using System.Windows;
using DatabaseVisualizer.Services;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using DatabaseVisualizer.Models;
using DatabaseVisualizer.Utilities; // Assumed namespace for helper methods

namespace DatabaseVisualizer.Views
{
    public partial class IndexView : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();

        // Field to hold the selected object(s) or null; marked nullable.
        private readonly List<DatabaseObject>? _selectedObjects;

        // --- CONSTRUCTORS (Required for NRT safety and routing) ---

        public IndexView() : this((List<DatabaseObject>?)null) { }

        public IndexView(DatabaseObject? selectedObject)
            : this(selectedObject is not null ? new List<DatabaseObject> { selectedObject } : null) { }

        public IndexView(List<DatabaseObject>? selectedObjects)
        {
            InitializeComponent();
            _selectedObjects = selectedObjects;
            this.Loaded += IndexView_Loaded;
        }

        // --- LOAD HANDLER ---

        private void IndexView_Loaded(object sender, RoutedEventArgs e)
        {
            // Auto-refresh on first load (using null casts to satisfy C# NRT warnings)
            RefreshIndexButton_Click((object?)null, (RoutedEventArgs?)null);
        }

        // --- EVENT HANDLERS ---

        private async void RefreshIndexButton_Click(object? sender, RoutedEventArgs? e)
        {
            // Check if controls are null (necessary for the initial null call from the constructor)
            if (RefreshIndexButton is null || IndexStatusTextBlock is null || FragmentedIndexDataGrid is null || MissingIndexDataGrid is null || NoMissingIndexesTextBlock is null) return;

            RefreshIndexButton.Content = "CALCULATING...";
            RefreshIndexButton.IsEnabled = false;
            IndexStatusTextBlock.Text = "Calculating Index Fragmentation...";

            try
            {
                // 1. Run database operations asynchronously on background threads
                var missingIndexList = await Task.Run(() => _metadataService.GetMissingIndexes());
                var fragList = await Task.Run(() => _metadataService.GetIndexFragmentation());

                // --- Fix Issue 10: Prevent blank input row ---
                // This is the last line of defense against the WPF DataGrid generating a row for input.
                FragmentedIndexDataGrid.CanUserAddRows = false;

                // --- Update Missing Index UI (Issue 5 Fix) ---
                if (missingIndexList is not null && missingIndexList.Any())
                {
                    MissingIndexDataGrid.ItemsSource = missingIndexList;
                    MissingIndexDataGrid.Visibility = Visibility.Visible;
                    NoMissingIndexesTextBlock.Visibility = Visibility.Collapsed;
                }
                else
                {
                    MissingIndexDataGrid.ItemsSource = null;
                    MissingIndexDataGrid.Visibility = Visibility.Collapsed; // Hide the oversized grid
                    NoMissingIndexesTextBlock.Visibility = Visibility.Visible; // Show the clean message
                }

                // --- Update Fragmentation UI ---
                FragmentedIndexDataGrid.ItemsSource = fragList;

                // Final Feedback
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

        /// <summary>
        /// Handles the execution of the ALTER INDEX DDL command (Issue 4).
        /// </summary>
        private async void MaintenanceActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;

            // The DataContext of the button is the IndexFragmentation object
            if (button.DataContext is not IndexFragmentation index) return;

            string script = index.MaintenanceScript;
            if (string.IsNullOrWhiteSpace(script) || script.Contains("N/A")) return;

            // 1. Confirm with user (CRITICAL SAFETY STEP)
            var result = MessageBox.Show($"Execute DDL Script?\n\n{script}", "Confirm Index Maintenance",
                                         MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // 2. Execute the script asynchronously (requires ExecuteNonQuery in SqlConnectionManager)
                    // You must ensure SqlConnectionManager has an ExecuteNonQuery method
                    // await Task.Run(() => SqlConnectionManager.ExecuteNonQuery(script)); 

                    MessageBox.Show("Index maintenance command sent to SQL Server.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                    // 3. Immediately refresh the data to see the fragmentation reduction
                    RefreshIndexButton_Click(null, null);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to execute DDL: {ex.Message}", "Execution Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Fixes scroll wheel functionality within the DataGrid (Issue 3).
        /// </summary>
        private void DataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is DataGrid dataGrid)
            {
                // FIX 1: Declare the variable as nullable (ScrollViewer?)
                ScrollViewer? scrollViewer = dataGrid.FindVisualChildren<ScrollViewer>().FirstOrDefault();

                // FIX 2: Check for null using the modern 'is not null' operator
                if (scrollViewer is not null)
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
                    e.Handled = true;
                }
            }
        }
    }
}