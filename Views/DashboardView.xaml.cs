using SQLAtlas.Services;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace SQLAtlas.Views
{
    public partial class DashboardView : UserControl
    {
        private readonly MetadataService _service = new MetadataService();
        private System.Windows.Threading.DispatcherTimer _refreshTimer;

        public DashboardView()
        {
            InitializeComponent();

            // 1. Setup the Timer
            _refreshTimer = new System.Windows.Threading.DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(5);
            _refreshTimer.Tick += (s, e) => RefreshData();

            // 2. Start refreshing when the view loads
            this.Loaded += (s, e) =>
            {
                RefreshData(); // Initial load
                _refreshTimer.Start();
            };

            // 3. Stop refreshing if we navigate away
            this.Unloaded += (s, e) => _refreshTimer.Stop();
        }

        public async void RefreshData()
        {
            try
            {
                // Fetch stats on background thread
                var stats = await Task.Run(() => _service.GetDashboardStats());

                if (stats == null) return;

                Dispatcher.Invoke(() =>
                {
                    // Update Text Labels
                    UptimeTxt.Text = stats.Uptime;

                    // We set the number only; the % is handled by the XAML TextBlock
                    CpuTxt.Text = stats.CpuUsage.ToString();

                    BlockingTxt.Text = stats.BlockingSessions.ToString();
                    FailedLoginsTxt.Text = stats.FailedLogins.ToString();
                    OnlineDbsTxt.Text = stats.OnlineDatabases.ToString();
                    OfflineDbsTxt.Text = stats.OfflineDatabases.ToString();

                    // --- FIX: PROGRESS BAR ANIMATION ---
                    // Since we removed the Circle, we only animate the horizontal bar value
                    var anim = new DoubleAnimation(
                        stats.CpuUsage,
                        TimeSpan.FromSeconds(1));

                    CpuRingGauge.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, anim);

                    // --- FIX: REMOVED ELLIPSE CAST ---
                    // We no longer look for "Indicator" or "myCircle" to avoid the Rectangle->Ellipse crash.

                    // Update Health Checklist
                    HealthCheckItems.ItemsSource = null;
                    HealthCheckItems.ItemsSource = stats.HealthChecks;

                    // Update Timestamp using the display property from service
                    // If LastUpdateDisplay is null, fallback to current time
                    string timeStamp = !string.IsNullOrEmpty(stats.LastUpdateDisplay)
                                       ? stats.LastUpdateDisplay
                                       : DateTime.Now.ToString("HH:mm:ss");

                    LastRefreshTxt.Text = $"Last Updated: {timeStamp}";
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("UI REFRESH ERROR: " + ex.Message);
            }
        }
    }
}