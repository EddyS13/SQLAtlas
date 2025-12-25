using Microsoft.Data.SqlClient;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Linq;
using System.ComponentModel;
using System.Windows.Data;

namespace SQLAtlas.Views
{
    public partial class PerformanceOverview : UserControl
    {
        private DispatcherTimer? _timer;
        private int _statsTicks = 0;
        private long _lastBatchRequestCount = 0;
        private DateTime _lastSampleTime = DateTime.Now;

        public ObservableCollection<ActivityLog> Activities { get; set; } = new();

        public PerformanceOverview()
        {
            InitializeComponent();
            this.DataContext = this;

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(2);
            _timer.Tick += async (s, e) => await UpdateServerMetrics();
            _timer.Start();

            // Trigger initial load immediately
            Task.Run(async () => await UpdateServerMetrics());
        }

        private async Task UpdateServerMetrics()
        {
            string? connStr = SQLAtlas.CurrentSession.ConnectionString;
            if (string.IsNullOrEmpty(connStr)) return;

            try
            {
                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync();

                    // 1. FAST METRICS (Every 2s)
                    var metricCmd = new SqlCommand(@"
                        SELECT 
                            (SELECT TOP 1 record.value('(./Record/SchedulerMonitorEvent/SystemHealth/ProcessUtilization)[1]', 'int') 
                             FROM (SELECT [timestamp], CONVERT(xml, record) AS [record] FROM sys.dm_os_ring_buffers 
                                   WHERE ring_buffer_type = N'RING_BUFFER_SCHEDULER_MONITOR' AND record LIKE '%<SystemHealth>%') AS x 
                             ORDER BY [timestamp] DESC) as CPU,
                            (SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE is_user_process = 1) as Connections,
                            (SELECT physical_memory_in_use_kb / 1024.0 / 1024.0 FROM sys.dm_os_process_memory) as MemGB,
                            (SELECT cntr_value FROM sys.dm_os_performance_counters 
                             WHERE counter_name = 'Batch Requests/sec' AND object_name LIKE '%SQL Statistics%') as TotalBatchRequests", conn);

                    using (var reader = await metricCmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            Dispatcher.Invoke(() => UpdateTopMetrics(reader));
                        }
                    }

                    // 2. STAGGERED UPDATES (Every 60s / 30 Ticks)
                    if (_statsTicks % 30 == 0 || _statsTicks == 1)
                    {
                        await UpdateDatabaseStats(conn);
                        await RefreshActivityLog(conn);
                    }
                    _statsTicks++;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
        }

        private void UpdateTopMetrics(SqlDataReader reader)
        {
            double cpu = reader["CPU"] != DBNull.Value ? Convert.ToDouble(reader["CPU"]) : 0;
            CpuText.Text = $"{cpu}%";
            CpuBar.Value = cpu;
            ConnText.Text = reader["Connections"]?.ToString() ?? "0";

            if (reader["MemGB"] != DBNull.Value)
            {
                double memGb = Convert.ToDouble(reader["MemGB"]);
                MemText.Text = $"{memGb:N1} GB";
                MemBar.Value = Math.Min(100, (memGb / 32.0) * 100);
            }

            if (reader["TotalBatchRequests"] != DBNull.Value)
            {
                long currentBatch = Convert.ToInt64(reader["TotalBatchRequests"]);
                double elapsed = (DateTime.Now - _lastSampleTime).TotalSeconds;
                if (_lastBatchRequestCount > 0 && elapsed > 0)
                {
                    int qps = (int)((currentBatch - _lastBatchRequestCount) / elapsed);
                    QueryText.Text = Math.Max(0, qps).ToString();
                }
                _lastBatchRequestCount = currentBatch;
                _lastSampleTime = DateTime.Now;
            }
        }

        private async Task UpdateDatabaseStats(SqlConnection conn)
        {
            var cmd = new SqlCommand(@"
                SELECT 
                    (SELECT COUNT(*) FROM sys.objects WHERE type = 'U') as TableCount,
                    (SELECT COUNT(*) FROM sys.objects WHERE type IN ('P', 'PC')) as ProcCount,
                    (SELECT SUM(size * 8 / 1024.0 / 1024.0) FROM sys.master_files WHERE database_id = DB_ID()) as SizeGB", conn);

            using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    Dispatcher.Invoke(() => {
                        TableCountText.Text = string.Format("{0:N0}", reader["TableCount"]);
                        ProcCountText.Text = string.Format("{0:N0}", reader["ProcCount"]);
                        double sizeGb = reader["SizeGB"] != DBNull.Value ? Convert.ToDouble(reader["SizeGB"]) : 0;
                        DbSizeText.Text = sizeGb >= 1024 ? $"{(sizeGb / 1024.0):N2} TB" : $"{sizeGb:N2} GB";
                    });
                }
            }
        }

        private async Task RefreshActivityLog(SqlConnection conn)
        {
            var combinedList = new List<ActivityLog>();
            try
            {
                // Pull System Logs (Capped)
                using (var cmdError = new SqlCommand("EXEC sys.xp_readerrorlog 0, 1, NULL, NULL", conn))
                using (var reader = await cmdError.ExecuteReaderAsync())
                {
                    int count = 0;
                    while (await reader.ReadAsync() && count < 50)
                    {
                        DateTime dt = Convert.ToDateTime(reader["LogDate"]);
                        string msg = reader["Text"]?.ToString() ?? "";
                        combinedList.Add(new ActivityLog
                        {
                            Date = dt.ToString("MMM dd"),
                            Time = dt.ToString("HH:mm:ss"),
                            FullDate = dt,
                            Category = "System",
                            Message = msg,
                            Status = msg.ToLower().Contains("error") ? "WARN" : "INFO"
                        });
                        count++;
                    }
                }

                // Pull Jobs/Sessions (Capped at 50)
                // ... (Insert your existing UNION query here with TOP 50) ...

                var finalData = combinedList.OrderByDescending(x => x.FullDate).Take(100).ToList();
                Dispatcher.Invoke(() => {
                    Activities.Clear();
                    foreach (var item in finalData) Activities.Add(item);
                });
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e) => _timer?.Stop();

        private void CategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ICollectionView view = CollectionViewSource.GetDefaultView(Activities);
            if (view == null) return;
            var selected = CategoryFilter.SelectedItem as ComboBoxItem;
            string filter = selected?.Content?.ToString() ?? "";
            view.Filter = (filter == "All Categories" || string.IsNullOrEmpty(filter)) ? null : (obj) => (obj as ActivityLog)?.Category == filter;
        }
    }

    public class ActivityLog
    {
        public string Time { get; set; } = "";
        public string Date { get; set; } = "";
        public DateTime FullDate { get; set; }
        public string Category { get; set; } = "";
        public string Message { get; set; } = "";
        public string Status { get; set; } = "";
        public SolidColorBrush StatusColor => Status switch
        {
            "WARN" => new SolidColorBrush(Colors.Orange),
            "SUCCESS" => (SolidColorBrush)Application.Current.Resources["CodeForeground"],
            _ => (SolidColorBrush)Application.Current.Resources["AccentColor"]
        };
    }
}