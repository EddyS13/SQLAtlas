using SQLAtlas.Services;
using System;
using System.Collections.Generic;
using System.IO;
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
using System.Xml;
using System.Xml.Linq;

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
            // 1. Capture the UI values on the MAIN thread first
            string queryText = QueryInputTextBox.Text;

            if (string.IsNullOrWhiteSpace(queryText))
            {
                PlanOutputTextBox.Text = "Please enter a query.";
                return;
            }

            GetPlanButton.IsEnabled = false;
            PlanOutputTextBox.Text = "Generating execution plan...";

            try
            {
                // 2. Pass the 'queryText' string variable into the Task.
                // Now the background thread doesn't have to touch the UI.
                string planXml = await Task.Run(() => _metadataService.GetQueryExecutionPlan(queryText));

                if (planXml.StartsWith("ERROR"))
                {
                    PlanOutputTextBox.Text = planXml;
                    PlanOutputTextBox.Foreground = Brushes.Red;
                }
                else
                {
                    PlanOutputTextBox.Text = PrettifyXml(planXml);
                    PlanOutputTextBox.Foreground = new SolidColorBrush(Color.FromRgb(156, 220, 254));
                }
            }
            catch (Exception ex)
            {
                PlanOutputTextBox.Text = $"Unexpected Error: {ex.Message}";
                PlanOutputTextBox.Foreground = Brushes.Red;
            }
            finally
            {
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

        private string PrettifyXml(string xml)
        {
            try
            {
                var stringBuilder = new StringBuilder();
                var element = XElement.Parse(xml);
                var settings = new XmlWriterSettings
                {
                    OmitXmlDeclaration = true,
                    Indent = true,
                    NewLineOnAttributes = true // Makes the XML much easier to read
                };

                using (var xmlWriter = XmlWriter.Create(stringBuilder, settings))
                {
                    element.Save(xmlWriter);
                }
                return stringBuilder.ToString();
            }
            catch
            {
                return xml; // Return raw if parsing fails
            }
        }
    }
}