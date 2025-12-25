using SQLAtlas.Models;
using SQLAtlas.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SQLAtlas.Views
{
    public partial class BackupRestoreView : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();

        public BackupRestoreView()
        {
            InitializeComponent();
            this.Loaded += View_Loaded;
        }

        private void View_Loaded(object sender, RoutedEventArgs e)
        {
            // Auto-load data on startup
            RefreshBackupButton_Click(null, null);
        }

        private async void RefreshBackupButton_Click(object? sender, RoutedEventArgs? e)
        {
            if (RefreshBackupButton is null || BackupHistoryGrid is null) return;

            RefreshBackupButton.Content = "FETCHING MSDB LOGS...";
            RefreshBackupButton.IsEnabled = false;

            try
            {
                // Fetch the last 30 days of history from our service
                var history = await Task.Run(() => _metadataService.GetBackupHistory());

                Dispatcher.Invoke(() => {
                    BackupHistoryGrid.ItemsSource = history;
                    RefreshBackupButton.Content = $"History Refreshed ({DateTime.Now:T})";

                    // Reset script box on refresh
                    RestoreScriptTextBox.Text = "-- Select a backup row to generate recovery T-SQL";
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load backup history: {ex.Message}", "Maintenance Error");
                RefreshBackupButton.Content = "Refresh Failed";
            }
            finally
            {
                RefreshBackupButton.IsEnabled = true;
            }
        }

        private void BackupHistoryGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Cast the selected item to your BackupHistory model
            if (BackupHistoryGrid.SelectedItem is BackupHistory selectedBackup)
            {
                // Use your safety escaping logic
                string escapedDatabaseName = EscapeSqlIdentifier(selectedBackup.DatabaseName);
                string escapedDeviceName = EscapeSqlString(selectedBackup.DeviceName);

                // Build a professional recovery script
                string restoreScript =
                    $"-- ************************************************************\n" +
                    $"-- WARNING: THIS SCRIPT WILL OVERWRITE THE EXISTING DATABASE\n" +
                    $"-- GENERATED AT: {DateTime.Now}\n" +
                    $"-- ************************************************************\n\n" +
                    $"USE [master];\n" +
                    $"GO\n\n" +
                    $"-- Kill existing connections\n" +
                    $"ALTER DATABASE {escapedDatabaseName} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;\n\n" +
                    $"-- Perform Restore\n" +
                    $"RESTORE DATABASE {escapedDatabaseName}\n" +
                    $"FROM DISK = {escapedDeviceName}\n" +
                    $"WITH \n" +
                    $"    REPLACE, \n" +
                    $"    STATS = 5;\n\n" +
                    $"-- Restore multi-user access\n" +
                    $"ALTER DATABASE {escapedDatabaseName} SET MULTI_USER;\n" +
                    $"GO";

                RestoreScriptTextBox.Text = restoreScript;
            }
            else
            {
                RestoreScriptTextBox.Text = "-- Select a backup row to generate recovery T-SQL";
            }
        }

        #region SQL Safety Helpers (Your Existing Logic)

        private static string EscapeSqlIdentifier(string? identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return "[UnknownDatabase]";

            string escaped = identifier.Replace("]", "]]");
            return $"[{escaped}]";
        }

        private static string EscapeSqlString(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "N''";

            string escaped = value.Replace("'", "''");
            return $"N'{escaped}'";
        }

        #endregion

        private async void CopyScriptButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(RestoreScriptTextBox.Text) ||
                RestoreScriptTextBox.Text.StartsWith("-- Select")) return;

            try
            {
                Clipboard.SetText(RestoreScriptTextBox.Text);

                // Visual Feedback
                string originalContent = CopyScriptButton.Content.ToString() ?? "COPY TO CLIPBOARD";
                CopyScriptButton.Content = "✓ COPIED!";
                CopyScriptButton.Foreground = (Brush)FindResource("SuccessColor");

                await Task.Delay(2000);

                CopyScriptButton.Content = originalContent;
                CopyScriptButton.Foreground = (Brush)FindResource("FontColor");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to copy to clipboard: " + ex.Message);
            }
        }
    }
}