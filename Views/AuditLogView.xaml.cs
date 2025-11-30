using SQLAtlas.Models;
using SQLAtlas.Services;
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
    /// Interaction logic for AuditLogView.xaml
    /// </summary>
    public partial class AuditLogView : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();

        // Field to hold the selected object(s); marked nullable since the view can load without data.
        private readonly List<DatabaseObject>? _selectedObjects;

        /// <summary>
        /// Parameterless Constructor (Called from Tools Menu).
        /// Routes to the parameterized constructor with a null object.
        /// </summary>
        public AuditLogView() : this((DatabaseObject?)null) { }

        /// <summary>
        /// Constructor for single-object selection (Table/View/Proc).
        /// Accepts a single nullable object.
        /// </summary>
        public AuditLogView(DatabaseObject? selectedObject)
        {
            InitializeComponent();

            // Convert the single object into a list for consistent internal handling
            if (selectedObject != null)
            {
                _selectedObjects = new List<DatabaseObject> { selectedObject };
            }
            else
            {
                _selectedObjects = null;
            }

            this.Loaded += SecurityView_Loaded;
        }

        /// <summary>
        /// Constructor for multi-select routing (although security is usually single-object).
        /// </summary>
        public AuditLogView(List<DatabaseObject>? selectedObjects)
        {
            InitializeComponent();
            _selectedObjects = selectedObjects;
            this.Loaded += SecurityView_Loaded;
        }


        private async void SecurityView_Loaded(object sender, RoutedEventArgs e)
        {
            // Reset status of placeholders
            NoLogsTextBlock.Visibility = Visibility.Collapsed;

            // The Audit Log view is a general utility, so it always loads its data on startup
            try
            {
                // Fetch data
                var events = await Task.Run(() => _metadataService.GetRecentSecurityEvents());
                bool hasEvents = events.Any();

                if (hasEvents)
                {
                    AuditLogGrid.ItemsSource = events;
                    AuditLogGrid.Visibility = Visibility.Visible;

                    // CRITICAL FIX: Ensure headers are visible when data is present
                    AuditLogGrid.HeadersVisibility = DataGridHeadersVisibility.Column;
                    NoLogsTextBlock.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // If empty, hide the grid contents and headers, show message
                    AuditLogGrid.ItemsSource = null;
                    AuditLogGrid.Visibility = Visibility.Collapsed;

                    // CRITICAL FIX: Hide the column headers (the "short blue bar")
                    AuditLogGrid.HeadersVisibility = DataGridHeadersVisibility.None;

                    NoLogsTextBlock.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                // Handle error, display message
                MessageBox.Show($"Error loading audit log data: {ex.Message}", "Security Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }
}
