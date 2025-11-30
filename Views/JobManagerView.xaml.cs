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
using SQLAtlas.Services;

namespace SQLAtlas.Views
{
    public partial class JobManagerView : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();

        public JobManagerView()
        {
            InitializeComponent();
            this.Loaded += View_Loaded;
        }

        private void View_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshJobsButton_Click(null, null);
        }

        private async void RefreshJobsButton_Click(object? sender, RoutedEventArgs? e)
        {
            if (RefreshJobsButton is null || JobsDataGrid is null) return;

            RefreshJobsButton.Content = "FETCHING JOBS...";
            RefreshJobsButton.IsEnabled = false;

            try
            {
                var jobs = await Task.Run(() => _metadataService.GetSqlAgentJobs());
                JobsDataGrid.ItemsSource = jobs;

                RefreshJobsButton.Content = $"Job Status Refreshed ({DateTime.Now:T})";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to retrieve job status: {ex.Message}", "Agent Error", MessageBoxButton.OK, MessageBoxImage.Error);
                RefreshJobsButton.Content = "Refresh Failed";
            }
            finally
            {
                RefreshJobsButton.IsEnabled = true;
            }
        }
    }
}