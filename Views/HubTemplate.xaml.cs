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
    /// Interaction logic for HubTemplate.xaml
    /// </summary>
    public partial class HubTemplate : UserControl
    {
        public HubTemplate(string title, string subTitle, Dictionary<string, string> stats, List<ToolShortcut> tools)
        {
            InitializeComponent();

            HubTitle.Text = title;
            HubSub.Text = subTitle;

            // Bind the live counts (Tables, Admins, etc)
            HubStatGrid.ItemsSource = stats;

            // Bind the tools (User Manager, Backup Wizard, etc)
            ToolShortcuts.ItemsSource = tools;

            // Fix: Toggle visibility of the tools header
            if (tools == null || tools.Count == 0)
            {
                AvailableToolsHeader.Visibility = Visibility.Collapsed;
                ToolShortcuts.Visibility = Visibility.Collapsed;
            }
            else
            {
                AvailableToolsHeader.Visibility = Visibility.Visible;
                ToolShortcuts.Visibility = Visibility.Visible;
            }
        }

        private void ToolBlock_Click(object sender, RoutedEventArgs e)
        {
            // 1. Identify the button and its Tag
            if (sender is Button btn && btn.Tag != null)
            {
                string toolName = btn.Tag.ToString();

                // 2. Find the Main Window (The Boss)
                var parentWindow = Window.GetWindow(this) as ExplorerWindow;

                if (parentWindow != null)
                {
                    // 3. Command the Main Window to change the view
                    // NOTE: LoadDiagnosticView MUST be 'public' in ExplorerWindow.xaml.cs
                    parentWindow.LoadDiagnosticView(toolName);
                }
            }
        }
    }

    // Simple class to hold tool info
    public class ToolShortcut
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
    }
}
