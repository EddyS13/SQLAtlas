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
    /// Interaction logic for HighAvailabilityView.xaml
    /// </summary>
    public partial class HighAvailabilityView : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();

        public HighAvailabilityView()
        {
            InitializeComponent();
            this.Loaded += View_Loaded;
        }

        private void View_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshAGButton_Click(null, null);
        }

        private async void RefreshAGButton_Click(object? sender, RoutedEventArgs? e)
        {
            if (RefreshAGButton is null || AgStatusGrid is null) return;

            RefreshAGButton.Content = "FETCHING STATUS...";
            RefreshAGButton.IsEnabled = false;

            try
            {
                var status = await Task.Run(() => _metadataService.GetAvailabilityGroupStatus());
                bool hasData = status is not null && status.Any(); // Check if list is not null and has items

                if (hasData)
                {
                    AgStatusGrid.ItemsSource = status;
                    AgStatusGrid.Visibility = Visibility.Visible;
                    NoDataTextBlock.Visibility = Visibility.Collapsed; // Hide message
                }
                else
                {
                    AgStatusGrid.ItemsSource = null;
                    AgStatusGrid.Visibility = Visibility.Collapsed; // Hide the empty grid structure
                    NoDataTextBlock.Visibility = Visibility.Visible; // Show message
                }

                RefreshAGButton.Content = $"Status Refreshed ({DateTime.Now:T})";
            }
            catch (Exception ex)
            {
                // ... (existing error handling) ...
                // On error, hide grid and show standard error feedback
                AgStatusGrid.Visibility = Visibility.Collapsed;
                NoDataTextBlock.Visibility = Visibility.Visible;
                NoDataStatusText.Text = $"ERROR: {ex.Message}";
                RefreshAGButton.Content = "Refresh Failed";
            }
            finally
            {
                RefreshAGButton.IsEnabled = true;
            }
        }
    }
}
