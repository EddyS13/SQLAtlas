// Views/SchemaCompareView.xaml.cs

using System.Windows.Controls;
using System.Windows;
using SQLAtlas.Services;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using SQLAtlas.Models;
using System.Windows.Input; // Required for PasswordBox.Password access

namespace SQLAtlas.Views
{
    public partial class SchemaCompareView : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();

        public SchemaCompareView()
        {
            InitializeComponent();
            this.Loaded += SchemaCompareView_Loaded;
        }

        private async void SchemaCompareView_Loaded(object sender, RoutedEventArgs e)
        {
            AuthType_Changed(null, null);
        }

        private void AuthType_Changed(object? sender, RoutedEventArgs? e)
        {
            CredentialPanel.Visibility = (WindowsAuthCheckBox.IsChecked == true)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private async void CompareButton_Click(object sender, RoutedEventArgs e)
        {
            // CRITICAL FIX: Get the actual current connection string from SqlConnectionManager
            string? currentConnectionString = SQLAtlas.Data.SqlConnectionManager.GetCurrentConnectionString();
            
            if (string.IsNullOrWhiteSpace(currentConnectionString))
            {
                MessageBox.Show("Error: Unable to retrieve current database connection string.", "Connection Error");
                return;
            }

            string targetServer = TargetServerTextBox.Text;
            string targetDb = TargetDatabaseTextBox.Text;

            if (string.IsNullOrWhiteSpace(targetServer) || string.IsNullOrWhiteSpace(targetDb))
            {
                MessageBox.Show("Please enter valid target server and database names.", "Input Error");
                return;
            }

            string targetConnectionString;
            if (WindowsAuthCheckBox.IsChecked == true)
            {
                // FIX: Add TrustServerCertificate and Encrypt options
                targetConnectionString = $"Server={targetServer};Database={targetDb};Integrated Security=True;Connection Timeout=300;TrustServerCertificate=True;Encrypt=False;";
            }
            else
            {
                string password = TargetPasswordBox.Password;
                // FIX: Add TrustServerCertificate and Encrypt options
                targetConnectionString = $"Server={targetServer};Database={targetDb};User Id={TargetUserTextBox.Text};Password={password};Connection Timeout=300;TrustServerCertificate=True;Encrypt=False;";
            }

            CompareButton.Content = "Comparing Schemas...";
            CompareButton.IsEnabled = false;

            try
            {
                var sourceSchemaTask = Task.Run(() => _metadataService.GetBaseSchema(currentConnectionString));
                var targetSchemaTask = Task.Run(() => _metadataService.GetBaseSchema(targetConnectionString));

                await Task.WhenAll(sourceSchemaTask, targetSchemaTask);

                var sourceSchema = sourceSchemaTask.Result;
                var targetSchema = targetSchemaTask.Result;

                // Check for errors in the results
                if (sourceSchema.Any(s => s.DifferenceType?.StartsWith("Error:") == true))
                {
                    MessageBox.Show($"Source database error: {sourceSchema.First(s => s.DifferenceType?.StartsWith("Error:") == true).DifferenceType}", "Schema Retrieval Error");
                    return;
                }

                if (targetSchema.Any(s => s.DifferenceType?.StartsWith("Error:") == true))
                {
                    MessageBox.Show($"Target database error: {targetSchema.First(s => s.DifferenceType?.StartsWith("Error:") == true).DifferenceType}", "Schema Retrieval Error");
                    return;
                }

                // Perform comparison
                var differences = CompareSchemas(sourceSchema, targetSchema);

                // Bind results
                DifferencesDataGrid.ItemsSource = differences;
                CompareButton.Content = $"Comparison Complete. Found {differences.Count} differences.";
            }
            catch (Exception ex)
            {
                CompareButton.Content = "Comparison Failed";
                MessageBox.Show($"Comparison Failed: {ex.Message}", "Comparison Error");
            }
            finally
            {
                CompareButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// Compares two schema lists to find missing tables and columns.
        /// </summary>
        private List<SchemaDifference> CompareSchemas(List<SchemaDifference> source, List<SchemaDifference> target)
        {
            var differences = new List<SchemaDifference>();

            // --- Step 1: Extract Unique Table Names ---
            var sourceTableNames = source
                .Select(s => s.ObjectName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var targetTableNames = target
                .Select(t => t.ObjectName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // --- Step 2: Detect Missing Tables ---
            // Tables in Source but NOT in Target
            foreach (var tableName in sourceTableNames.Except(targetTableNames, StringComparer.OrdinalIgnoreCase))
            {
                differences.Add(new SchemaDifference
                {
                    ObjectName = tableName,
                    DifferenceType = "TABLE Missing in Target",
                    SynchronizationScript = $"CREATE TABLE {tableName} (-- DDL required --)"
                });
            }

            // Tables in Target but NOT in Source (the new table you added)
            foreach (var tableName in targetTableNames.Except(sourceTableNames, StringComparer.OrdinalIgnoreCase))
            {
                differences.Add(new SchemaDifference
                {
                    ObjectName = tableName,
                    DifferenceType = "TABLE Missing in Source",
                    SynchronizationScript = $"DROP TABLE {tableName}"
                });
            }

            // --- Step 3: Detect Missing Columns (only for tables that exist in BOTH) ---
            var commonTableNames = sourceTableNames.Intersect(targetTableNames, StringComparer.OrdinalIgnoreCase).ToList();

            foreach (var tableName in commonTableNames)
            {
                // Get all columns for this table in source and target
                var sourceColumns = source
                    .Where(s => s.ObjectName.Equals(tableName, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(s.ColumnName))
                    .Select(s => $"{s.ColumnName}.{s.DataType}")
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var targetColumns = target
                    .Where(t => t.ObjectName.Equals(tableName, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(t.ColumnName))
                    .Select(t => $"{t.ColumnName}.{t.DataType}")
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Columns in Source but NOT in Target
                var missingInTarget = sourceColumns.Except(targetColumns, StringComparer.OrdinalIgnoreCase);
                foreach (var columnDef in missingInTarget)
                {
                    var col = source.FirstOrDefault(s => 
                        s.ObjectName.Equals(tableName, StringComparison.OrdinalIgnoreCase) &&
                        $"{s.ColumnName}.{s.DataType}".Equals(columnDef, StringComparison.OrdinalIgnoreCase));

                    if (col != null)
                    {
                        differences.Add(new SchemaDifference
                        {
                            ObjectName = tableName,
                            ColumnName = col.ColumnName,
                            DataType = col.DataType,
                            DifferenceType = "Column Missing in Target",
                            SynchronizationScript = $"ALTER TABLE {tableName} ADD {col.ColumnName} {col.DataType}"
                        });
                    }
                }

                // Columns in Target but NOT in Source
                var missingInSource = targetColumns.Except(sourceColumns, StringComparer.OrdinalIgnoreCase);
                foreach (var columnDef in missingInSource)
                {
                    var col = target.FirstOrDefault(t => 
                        t.ObjectName.Equals(tableName, StringComparison.OrdinalIgnoreCase) &&
                        $"{t.ColumnName}.{t.DataType}".Equals(columnDef, StringComparison.OrdinalIgnoreCase));

                    if (col != null)
                    {
                        differences.Add(new SchemaDifference
                        {
                            ObjectName = tableName,
                            ColumnName = col.ColumnName,
                            DataType = col.DataType,
                            DifferenceType = "Column Missing in Source",
                            SynchronizationScript = $"ALTER TABLE {tableName} DROP COLUMN {col.ColumnName}"
                        });
                    }
                }
            }

            return differences;
        }
    }
}