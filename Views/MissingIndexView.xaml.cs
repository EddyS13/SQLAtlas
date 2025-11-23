// Views/MissingIndexView.xaml.cs

using DatabaseVisualizer.Services;
using DatabaseVisualizer.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DatabaseVisualizer.Views
{
    public partial class MissingIndexView : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();

        // --- CONSTRUCTORS ---

        /// <summary>
        /// Parameterless Constructor (For routing safety, called from Tools Menu).
        /// </summary>
        public MissingIndexView()
        {
            InitializeComponent();
            this.Loaded += MissingIndexView_Loaded;
        }

        // Note: Constructors for DatabaseObject are omitted as this view is loaded only from the Tools menu.

        // --- LOAD HANDLER ---

        private void MissingIndexView_Loaded(object sender, RoutedEventArgs e)
        {
            // Auto-refresh on first load (using null casts to satisfy C# NRT warnings)
            RefreshIndexButton_Click((object?)null, (RoutedEventArgs?)null);
        }

        // --- EVENT HANDLERS ---

        private async void RefreshIndexButton_Click(object? sender, RoutedEventArgs? e)
        {
            // Check for null controls before proceeding (safety measure)
            if (RefreshIndexButton is null || IndexStatusTextBlock is null || MissingIndexDataGrid is null || NoMissingIndexesTextBlock is null) return;

            RefreshIndexButton.Content = "CALCULATING...";
            RefreshIndexButton.IsEnabled = false;
            IndexStatusTextBlock.Text = "Retrieving Missing Index Recommendations...";

            try
            {
                // 1. Run database operation asynchronously
                var missingIndexList = await Task.Run(() => _metadataService.GetMissingIndexes());

                // --- Update Missing Index UI (Issue 5 Fix) ---
                if (missingIndexList is not null && missingIndexList.Any())
                {
                    MissingIndexDataGrid.ItemsSource = missingIndexList;

                    // Show the data grid and hide the message
                    MissingIndexDataGrid.Visibility = Visibility.Visible;
                    NoMissingIndexesTextBlock.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // Hide the data grid content and show the message
                    MissingIndexDataGrid.ItemsSource = null;
                    MissingIndexDataGrid.Visibility = Visibility.Collapsed;
                    NoMissingIndexesTextBlock.Visibility = Visibility.Visible; // Show the clean message
                }

                // Final Feedback
                IndexStatusTextBlock.Text = $"Analysis Complete. Found {missingIndexList.Count} recommendations.";
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

        // --- SCROLL HANDLER (Required for all DataGrids) ---

        private void DataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is DataGrid dataGrid)
            {
                // Find the internal ScrollViewer within the DataGrid's visual tree
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