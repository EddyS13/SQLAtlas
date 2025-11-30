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
    /// Interaction logic for ServerInfoView.xaml
    /// </summary>
    public partial class ServerInfoView : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();

        public ServerInfoView()
        {
            InitializeComponent();
            this.Loaded += View_Loaded;
        }

        private void View_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshServerInfoButton_Click(null, null);
        }

        private async void RefreshServerInfoButton_Click(object? sender, RoutedEventArgs? e)
        {
            RefreshServerInfoButton.Content = "FETCHING INFO...";
            RefreshServerInfoButton.IsEnabled = false;

            try
            {
                var details = await Task.Run(() => _metadataService.GetServerInformation());
                ServerInfoDataGrid.ItemsSource = details;

                RefreshServerInfoButton.Content = $"Info Refreshed ({DateTime.Now:T})";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to retrieve server information: {ex.Message}", "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
                RefreshServerInfoButton.Content = "Refresh Server Info";
            }
            finally
            {
                RefreshServerInfoButton.IsEnabled = true;
            }
        }
    }
}
