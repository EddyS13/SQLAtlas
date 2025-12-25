using SQLAtlas.Models;
using SQLAtlas.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SQLAtlas.Views
{
    /// <summary>
    /// Interaction logic for BlockingChainView.xaml
    /// </summary>
    public partial class BlockingChainView : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();

        // Parameterless constructor for direct navigation (Tools Menu)
        public BlockingChainView()
        {
            InitializeComponent();
            this.Loaded += BlockingChainView_Loaded;
        }
        private void BlockingChainView_Loaded(object sender, RoutedEventArgs e)
        {
            // Auto-refresh on first load (using null casts to satisfy C# NRT warnings)
            RefreshPerformanceButton_Click((object?)null, (RoutedEventArgs?)null);
        }
        private async void RefreshPerformanceButton_Click(object? sender, RoutedEventArgs? e)
        {
            RefreshPerformanceButton.Content = "ANALYZING...";
            RefreshPerformanceButton.IsEnabled = false;

            try
            {
                var blockingList = await Task.Run(() => _metadataService.GetCurrentBlockingChain());

                Dispatcher.Invoke(() => {
                    BlockingChainDataGrid.ItemsSource = blockingList;

                    if (blockingList == null || blockingList.Count == 0)
                    {
                        // Show the "No Blocking" message, hide the Grid
                        BlockingChainDataGrid.Visibility = Visibility.Collapsed;
                        NoBlockingContainer.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        // Show the Grid, hide the message
                        BlockingChainDataGrid.Visibility = Visibility.Visible;
                        NoBlockingContainer.Visibility = Visibility.Collapsed;
                    }

                    RefreshPerformanceButton.Content = "REFRESH MONITOR";
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Blocking Analysis Error: {ex.Message}");
                RefreshPerformanceButton.Content = "RETRY";
            }
            finally
            {
                RefreshPerformanceButton.IsEnabled = true;
            }
        }

        private async void KillSession_Click(object sender, RoutedEventArgs e)
        {
            var btn = (Button)sender;
            var session = btn.DataContext as BlockingProcess;
            if (session == null) return;

            var result = MessageBox.Show($"Kill SPID {session.Spid}?\n\nIf this is the Head Blocker (Level 0), it will release the entire chain.",
                                         "Terminate Session", MessageBoxButton.YesNo, MessageBoxImage.Stop);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await Task.Run(() => _metadataService.KillSession(session.Spid));
                    RefreshPerformanceButton_Click(null, null);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }
    }
}
