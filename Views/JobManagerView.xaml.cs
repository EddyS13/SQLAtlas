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
    public partial class JobManagerView : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();

        private List<SqlAgentJob> _allJobs = new List<SqlAgentJob>();

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
                _allJobs = await Task.Run(() => _metadataService.GetSqlAgentJobs());

                Dispatcher.Invoke(() => {
                    JobsDataGrid.ItemsSource = _allJobs;
                    RefreshJobsButton.Content = "REFRESH STATUS";

                    // Set search placeholder
                    JobSearchBox.Text = JobSearchBox.Tag.ToString();
                    JobSearchBox.Opacity = 0.5;
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed: {ex.Message}");
                RefreshJobsButton.Content = "Refresh Failed";
            }
            finally { RefreshJobsButton.IsEnabled = true; }
        }

        private void JobSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = JobSearchBox.Text.ToLower();
            string placeholder = JobSearchBox.Tag?.ToString().ToLower() ?? "";

            if (filter == placeholder || _allJobs == null) return;

            if (string.IsNullOrWhiteSpace(filter))
            {
                JobsDataGrid.ItemsSource = _allJobs;
            }
            else
            {
                // Filtering the SqlAgentJob list
                JobsDataGrid.ItemsSource = _allJobs
                    .Where(j => j.JobName.ToLower().Contains(filter))
                    .ToList();
            }
        }

        // Logic for Searchbox Placeholder
        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (JobSearchBox.Text == JobSearchBox.Tag?.ToString())
            {
                JobSearchBox.Text = "";
                JobSearchBox.Opacity = 1.0;
            }
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(JobSearchBox.Text))
            {
                JobSearchBox.Text = JobSearchBox.Tag?.ToString() ?? "";
                JobSearchBox.Opacity = 0.5;
            }
        }
    }
}