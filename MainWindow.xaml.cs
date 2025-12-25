using SQLAtlas.Data;
using SQLAtlas.Services;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace SQLAtlas;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AuthCheckBox_Toggled(null, null);
    }

    private void AuthCheckBox_Toggled(object sender, RoutedEventArgs e)
    {
        // Check for null because this fires during InitializeComponent()
        if (SqlAuthFields == null || ServerNameTextBox == null) return;

        bool useSqlAuth = AuthCheckBox.IsChecked ?? false;

        if (useSqlAuth)
        {
            SqlAuthFields.Visibility = Visibility.Visible;
            // If they want SQL Auth, we clear the default 'localhost' 
            // to let them type their specific server if needed
            if (ServerNameTextBox.Text == "localhost") ServerNameTextBox.Text = string.Empty;
        }
        else
        {
            SqlAuthFields.Visibility = Visibility.Collapsed;
            ServerNameTextBox.Text = "localhost";
            UsernameTextBox.Text = string.Empty;
            PasswordBox.Password = string.Empty;
        }
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        string server = ServerNameTextBox.Text;
        string database = DatabaseNameTextBox.Text;
        bool useSqlAuth = AuthCheckBox.IsChecked ?? false;

        // Use SQL Auth means Windows Auth is FALSE
        bool success = await SqlConnectionManager.TestConnection(server, database, UsernameTextBox.Text, PasswordBox.Password, !useSqlAuth);

        if (success)
        {
            // Save to Global Session
            CurrentSession.ConnectionString = SqlConnectionManager.GetLastConnectionString();
            CurrentSession.ServerName = server;
            CurrentSession.DatabaseName = database;

            // Load Sidebar Data
            var initialData = await SqlConnectionManager.GetGroupedObjects(CurrentSession.ConnectionString!);

            // Transition with required arguments
            var explorer = new ExplorerWindow(initialData, server, database);
            explorer.Show();
            this.Close();
        }
        else
        {
            StatusTextBlock.Text = "Connection Failed.";
            StatusTextBlock.Foreground = Brushes.Red;
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

    private void MainBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}