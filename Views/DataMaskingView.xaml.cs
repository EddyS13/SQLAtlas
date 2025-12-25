using SQLAtlas.Data;
using SQLAtlas.Models;
using SQLAtlas.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SQLAtlas.Views
{
    public partial class DataMaskingView : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();

        public DataMaskingView()
        {
            InitializeComponent();
            this.Loaded += View_Loaded;
        }

        private void View_Loaded(object sender, RoutedEventArgs e) => RefreshMaskingButton_Click(null, null);

        private async void RefreshMaskingButton_Click(object? sender, RoutedEventArgs? e)
        {
            RefreshMaskingButton.Content = "AUDITING COLUMNS...";
            RefreshMaskingButton.IsEnabled = false;

            try
            {
                var candidates = await Task.Run(() => _metadataService.GetMaskingCandidates());

                Dispatcher.Invoke(() => {
                    if (candidates != null && candidates.Any())
                    {
                        MaskingCandidatesGrid.ItemsSource = candidates;
                        MaskingCandidatesGrid.Visibility = Visibility.Visible;
                        NoCandidatesContainer.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        MaskingCandidatesGrid.Visibility = Visibility.Collapsed;
                        NoCandidatesContainer.Visibility = Visibility.Visible;
                    }
                    RefreshMaskingButton.Content = "REFRESH AUDIT";
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Audit Error: {ex.Message}");
                RefreshMaskingButton.Content = "RETRY";
            }
            finally { RefreshMaskingButton.IsEnabled = true; }
        }

        private async void ApplyMaskingActionButton_Click(object sender, RoutedEventArgs e)
        {
            var btn = (Button)sender;
            var item = btn.DataContext as MaskingCandidate;
            if (item == null) return;

            string action = item.IsMasked ? "REMOVE MASKING from" : "APPLY MASKING to";
            var result = MessageBox.Show($"Are you sure you want to {action} {item.ColumnName}?", "Confirm Security Change", MessageBoxButton.YesNo);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Generate DDL on the fly
                    string sql = item.IsMasked
                        ? $"ALTER TABLE [{item.SchemaName}].[{item.TableName}] ALTER COLUMN [{item.ColumnName}] DROP MASKED"
                        : $"ALTER TABLE [{item.SchemaName}].[{item.TableName}] ALTER COLUMN [{item.ColumnName}] ADD MASKED WITH (FUNCTION = '{item.SuggestedMask}')";

                    await Task.Run(() => SqlConnectionManager.ExecuteNonQuery(sql));
                    RefreshMaskingButton_Click(null, null);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to update masking: {ex.Message}");
                }
            }
        }
    }
}