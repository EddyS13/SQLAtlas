// Views/MissingIndexView.xaml.cs

using SQLAtlas.Models;
using SQLAtlas.Services;
using SQLAtlas.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SQLAtlas.Views
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
                int count = missingIndexList?.Count ?? 0;

                IndexStatusTextBlock.Text = $"Analysis Complete. Found {count} recommendations.";
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

        private void ViewScriptButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is MissingIndex index)
            {
                // Create a custom styled popup window
                Window scriptWindow = new Window
                {
                    Title = "Index Recovery Script",
                    Width = 600,
                    Height = 400,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Background = (Brush)FindResource("DeepBackground"),
                    WindowStyle = WindowStyle.ToolWindow
                };

                Grid grid = new Grid { Margin = new Thickness(20) };
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                TextBlock header = new TextBlock
                {
                    Text = "T-SQL GENERATED SCRIPT",
                    Foreground = (Brush)FindResource("AccentColor"),
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 10)
                };

                TextBox scriptBox = new TextBox
                {
                    Text = index.CreateScript,
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = new FontFamily("Consolas"),
                    Background = (Brush)FindResource("CardBackground"),
                    Foreground = (Brush)FindResource("CodeForeground"),
                    Padding = new Thickness(10),
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                };

                Button copyBtn = new Button
                {
                    Content = "COPY SCRIPT TO CLIPBOARD",
                    Margin = new Thickness(0, 15, 0, 0),
                    Padding = new Thickness(0, 10, 0, 10),
                    Background = (Brush)FindResource("AccentColor"),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold
                };

                // Copy Logic
                copyBtn.Click += (s, ev) => {
                    Clipboard.SetText(scriptBox.Text);
                    copyBtn.Content = "✓ COPIED TO CLIPBOARD!";
                    copyBtn.Background = (Brush)FindResource("SuccessColor");
                    copyBtn.FontWeight = FontWeights.Bold;
                };

                grid.Children.Add(header);
                grid.Children.Add(scriptBox);
                grid.Children.Add(copyBtn);
                Grid.SetRow(header, 0);
                Grid.SetRow(scriptBox, 1);
                Grid.SetRow(copyBtn, 2);

                scriptWindow.Content = grid;
                scriptWindow.ShowDialog();
            }
        }
    }
}