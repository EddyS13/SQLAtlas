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
    public partial class DesignDevOverview : System.Windows.Controls.UserControl
    {
        public DesignDevOverview()
        {
            InitializeComponent();
        }


        private void ToolBlock_Click(object sender, RoutedEventArgs e)
        {
            // 1. Identify which button was clicked
            if (sender is Button clickedButton && clickedButton.Tag != null)
            {
                string targetTool = clickedButton.Tag.ToString();

                // 2. Find the main window (ExplorerWindow) in the application's memory
                var parentWindow = Window.GetWindow(this) as ExplorerWindow;

                if (parentWindow != null)
                {
                    // 3. Trigger the existing routing logic you already built
                    parentWindow.LoadDiagnosticView(targetTool);
                }
            }
        }

    }
}
