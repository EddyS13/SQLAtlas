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
    /// Interaction logic for BackupRestoreView.xaml
    /// </summary>
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
            RefreshBackupButton_Click(null, null);
        }

        private async void RefreshBackupButton_Click(object? sender, RoutedEventArgs? e)
        {
            if (RefreshBackupButton is null || BackupHistoryGrid is null) return;

            RefreshBackupButton.Content = "FETCHING HISTORY...";
            RefreshBackupButton.IsEnabled = false;

            try
            {
                var history = await Task.Run(() => _metadataService.GetBackupHistory());
                BackupHistoryGrid.ItemsSource = history;

                RefreshBackupButton.Content = $"History Refreshed ({DateTime.Now:T})";
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
            if (BackupHistoryGrid.SelectedItem is BackupHistory selectedBackup)
            {
                // Escape square brackets and single quotes for SQL injection prevention
                string escapedDatabaseName = EscapeSqlIdentifier(selectedBackup.DatabaseName);
                string escapedDeviceName = EscapeSqlString(selectedBackup.DeviceName);
                
                string restoreScript = $@"-- **WARNING: Running this command will overwrite the existing database!**
                    -- Execute this script in SSMS connected to the master database.

                    RESTORE DATABASE {escapedDatabaseName}
                    FROM DISK = {escapedDeviceName}
                    WITH 
                        FILE = 1, 
                        NOUNLOAD, 
                        REPLACE, 
                        STATS = 10;
                            ";

                RestoreScriptTextBox.Text = restoreScript;
            }
            else
            {
                RestoreScriptTextBox.Text = string.Empty;
            }
        }

        /// <summary>
        /// Safely escapes SQL identifiers (like database names) by wrapping in square brackets
        /// and escaping any internal brackets to prevent SQL injection.
        /// </summary>
        private static string EscapeSqlIdentifier(string? identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return "[UnknownDatabase]";

            // Escape closing brackets by doubling them
            string escaped = identifier.Replace("]", "]]");
            return $"[{escaped}]";
        }

        /// <summary>
        /// Safely escapes SQL string literals by wrapping in single quotes
        /// and escaping internal quotes to prevent SQL injection.
        /// </summary>
        private static string EscapeSqlString(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "N''";

            // Escape single quotes by doubling them
            string escaped = value.Replace("'", "''");
            return $"N'{escaped}'";
        }
    }

}
