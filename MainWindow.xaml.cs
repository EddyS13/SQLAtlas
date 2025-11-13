using DatabaseVisualizer.Data;
using DatabaseVisualizer.Services;
using System.Windows;
using System.Windows.Input;

namespace DatabaseVisualizer
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Set initial state for Windows Auth checkbox
            WindowsAuthCheckBox_Toggled(null, null);
        }

        private void WindowsAuthCheckBox_Toggled(object? sender, RoutedEventArgs? e)
        {
            // Toggles the enabled state of Username and Password fields
            bool isWindowsAuth = WindowsAuthCheckBox.IsChecked ?? false;
            
            UsernameTextBox.IsEnabled = !isWindowsAuth;
            PasswordBox.IsEnabled = !isWindowsAuth;
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            StatusTextBlock.Text = "Attempting connection...";
            ConnectButton.IsEnabled = false;

            // DEFINE THE CONNECTION VARIABLES from the WPF controls
            string server = ServerNameTextBox.Text;
            string database = DatabaseNameTextBox.Text;
            bool useWindowsAuth = WindowsAuthCheckBox.IsChecked ?? false;

            string user = useWindowsAuth ? string.Empty : UsernameTextBox.Text;
            string password = useWindowsAuth ? string.Empty : PasswordBox.Password;

            bool success = await SqlConnectionManager.TestConnection(
                server, database, user, password, useWindowsAuth);

            if (success)
            {
                StatusTextBlock.Text = "Connection Successful! Loading schema...";
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.Green;

                // --- TRANSITION LOGIC ---
                var metadataService = new MetadataService();
                var groupedObjects = metadataService.GetDatabaseObjects();

                if (groupedObjects != null && groupedObjects.Count > 0)
                {
                    // Pass connection info for Status Bar display
                    var explorerWindow = new ExplorerWindow(
                        groupedObjects,
                        ServerNameTextBox.Text,
                        DatabaseNameTextBox.Text);

                    explorerWindow.Show();
                    this.Close();
                }
                else
                {
                    StatusTextBlock.Text = "Connection successful, but no database objects found (or query failed).";
                    StatusTextBlock.Foreground = System.Windows.Media.Brushes.Orange;
                    ConnectButton.IsEnabled = true;
                }

            }
            else
            {
                StatusTextBlock.Text = "Connection Failed. Check credentials and server name.";
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
                ConnectButton.IsEnabled = true;
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            // Sets the window state to minimized
            WindowState = WindowState.Minimized;
        }

        private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
        {
            // Toggles between Maximized and Normal window states
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }
            else
            {
                WindowState = WindowState.Maximized;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Closes the current window
            Close();
        }

        private void WindowHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Check if the left mouse button was pressed (to avoid accidentally dragging on right-click)
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                // Tells the operating system to start the window move operation
                this.DragMove();
            }
        }
    }
}