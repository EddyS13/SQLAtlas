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
using System.Xml.Linq;
using System.IO;

namespace SQLAtlas.Views
{
    public partial class QueryPlanView : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();

        public QueryPlanView()
        {
            InitializeComponent();
        }

        private async void GetPlanButton_Click(object sender, RoutedEventArgs e)
        {
            // SECURITY FIX: Change from async void to Task-based pattern
            await GetPlanButtonClickAsync();
        }

        private async Task GetPlanButtonClickAsync()
        {
            if (string.IsNullOrWhiteSpace(QueryInputTextBox.Text))
            {
                PlanOutputTextBox.Text = "Please enter a query.";
                return;
            }

            GetPlanButton.IsEnabled = false;
            PlanOutputTextBox.Text = "Generating execution plan...";

            try
            {
                // FIX 5: Use QueryInputTextBox
                string planXml = await Task.Run(() => _metadataService.GetQueryExecutionPlan(QueryInputTextBox.Text));

                // CRITICAL FIX 6 & 7: Use PlanOutputTextBox
                if (planXml.StartsWith("ERROR"))
                {
                    PlanOutputTextBox.Text = planXml;
                    PlanOutputTextBox.Foreground = Brushes.Red;
                }
                else
                {
                    // Assuming you integrate the XML formatting logic here:
                    PlanOutputTextBox.Text = planXml ?? "No execution plan generated.";
                    PlanOutputTextBox.Foreground = Brushes.White;
                }
            }
            catch (Exception ex)
            {
                PlanOutputTextBox.Text = $"Unexpected .NET Error: {ex.Message}";
                PlanOutputTextBox.Foreground = Brushes.Red;
            }
            finally
            {
                // FIX 8: GetPlanButton
                GetPlanButton.IsEnabled = true;
            }
        }

        private void CopyPlanButton_Click(object sender, RoutedEventArgs e)
        {
            string planXml = PlanOutputTextBox.Text;

            if (string.IsNullOrWhiteSpace(planXml) || planXml.StartsWith("ERROR"))
            {
                MessageBox.Show("Cannot copy: No valid execution plan available.", "Copy Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // 1. Copy the formatted XML text to the clipboard
                Clipboard.SetText(planXml);

                // 2. Provide visual feedback (change button text temporarily)
                Button btn = (Button)sender;
                btn.Content = "✅ Copied!";
                btn.IsEnabled = false;

                // Reset button text after a short delay (using a DispatcherTimer)
                System.Windows.Threading.DispatcherTimer timer = new System.Windows.Threading.DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(2);
                timer.Tick += (s, ev) =>
                {
                    btn.Content = "📋 Copy XML";
                    btn.IsEnabled = true;
                    timer.Stop();
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy to clipboard: {ex.Message}", "Clipboard Error");
            }
        }
    }
}