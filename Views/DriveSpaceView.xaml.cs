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
    /// Interaction logic for DriveSpaceView.xaml
    /// </summary>
    public partial class DriveSpaceView : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();

        public DriveSpaceView()
        {
            InitializeComponent();
            this.Loaded += View_Loaded;
        }

        private void View_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshDriveButton_Click(null, null);
        }

        private async void RefreshDriveButton_Click(object? sender, RoutedEventArgs? e)
        {
            if (RefreshDriveButton is null || DriveSpaceGrid is null) return;

            RefreshDriveButton.Content = "FETCHING DRIVE STATUS...";
            RefreshDriveButton.IsEnabled = false;

            try
            {
                var spaceInfo = await Task.Run(() => _metadataService.GetDriveSpaceReport());
                DriveSpaceGrid.ItemsSource = spaceInfo;

                RefreshDriveButton.Content = $"Status Refreshed ({DateTime.Now:T})";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to retrieve drive status: {ex.Message}", "System Error");
                RefreshDriveButton.Content = "Refresh Failed";
            }
            finally
            {
                RefreshDriveButton.IsEnabled = true;
            }
        }
    }
}