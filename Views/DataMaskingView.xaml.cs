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
    /// Interaction logic for DataMaskingView.xaml
    /// </summary>
    public partial class DataMaskingView : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();

        public DataMaskingView()
        {
            InitializeComponent();
            this.Loaded += View_Loaded;
        }

        private void View_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshMaskingButton_Click(null, null);
        }

        private async void RefreshMaskingButton_Click(object? sender, RoutedEventArgs? e)
        {
            // ... (Button state logic) ...
            //var candidates = await Task.Run(() => _metadataService.GetMaskingCandidates());
            //MaskingCandidatesGrid.ItemsSource = candidates;
            // ... (Final state logic) ...

            RefreshMaskingButton.Content = "AUDITING COLUMNS...";
            RefreshMaskingButton.IsEnabled = false;

            try
            {
                var candidates = await Task.Run(() => _metadataService.GetMaskingCandidates());
                bool hasCandidates = candidates is not null && candidates.Any();

                // CRITICAL FIX: Toggle visibility based on data count
                if (hasCandidates)
                {
                    MaskingCandidatesGrid.ItemsSource = candidates;
                    MaskingCandidatesGrid.Visibility = Visibility.Visible;
                    NoCandidatesTextBlock.Visibility = Visibility.Collapsed;
                }
                else
                {
                    MaskingCandidatesGrid.ItemsSource = null; // Clear old data
                    MaskingCandidatesGrid.Visibility = Visibility.Collapsed; // Hide the empty grid/header
                    NoCandidatesTextBlock.Visibility = Visibility.Visible; // Show the message
                }

                RefreshMaskingButton.Content = $"Audit Complete ({DateTime.Now:T})";
            }
            catch (Exception ex)
            {
                // ... (Error handling remains the same) ...
                MaskingCandidatesGrid.Visibility = Visibility.Collapsed;
                NoCandidatesTextBlock.Visibility = Visibility.Visible;
                NoCandidatesTextBlock.Text = $"ERROR: Failed to run audit. Details: {ex.Message}";
                RefreshMaskingButton.Content = "Refresh Failed";
            }
            finally
            {
                RefreshMaskingButton.IsEnabled = true;
            }
        }

        private async void ApplyMaskingActionButton_Click(object sender, RoutedEventArgs e)
        {
            // Logic to execute DDL script from CommandParameter (using ExecuteNonQuery)
        }
    }
}
