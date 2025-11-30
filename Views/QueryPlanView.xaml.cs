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
            string query = QueryInputTextBox.Text;

            if (string.IsNullOrWhiteSpace(query))
            {
                MessageBox.Show("Please enter a SQL query to analyze.", "Input Required");
                return;
            }

            // Set initial state
            GetPlanButton.Content = "ANALYZING...";
            GetPlanButton.IsEnabled = false;
            PlanOutputTextBox.Text = "Running query in SHOWPLAN_XML mode. Please wait...";

            try
            {
                // CRITICAL: Run the plan retrieval service method asynchronously
                // The service method handles the SET SHOWPLAN_XML ON/OFF sequence.
                string planXml = await Task.Run(() => _metadataService.GetQueryExecutionPlan(query));

                // Check for successful XML retrieval or an error message
                if (!planXml.StartsWith("ERROR"))
                {
                    // 1. Format the XML for display
                    XDocument doc = XDocument.Parse(planXml);
                    string formattedXml = doc.ToString(); // Auto-indents and adds line breaks

                    // 2. Extract a simple summary (Conceptual)
                    // Find the estimated subtree cost from the root RelOp node
                    XElement? rootNode = doc.Descendants().FirstOrDefault(d => d.Name.LocalName == "RelOp");
                    string estimatedCost = rootNode?.Attribute("EstimateRows")?.Value ?? "N/A";

                    string summary = $"--- Plan Summary ---\n" +
                                     $"Estimated Rows: {estimatedCost}\n" +
                                     $"Plan XML Size: {planXml.Length / 1024:N0} KB\n\n";

                    // 3. Display summary above the raw XML
                    PlanOutputTextBox.Text = summary + formattedXml;
                    PlanOutputTextBox.Foreground = Brushes.White;
                }
                else
                {
                    PlanOutputTextBox.Text = planXml;
                    PlanOutputTextBox.Foreground = Brushes.White; // Assumes dark theme
                }
            }
            catch (Exception ex)
            {
                PlanOutputTextBox.Text = $"Unexpected .NET Error: {ex.Message}";
                PlanOutputTextBox.Foreground = Brushes.Red;
            }
            finally
            {
                GetPlanButton.Content = "⚡ Get Plan";
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