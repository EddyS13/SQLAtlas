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
    /// 

    

    public partial class AuditLogView : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();

        // Field to hold the selected object(s); marked nullable since the view can load without data.
        private readonly List<DatabaseObject>? _selectedObjects;

        private List<AuditLogEvent> _allEvents = new List<AuditLogEvent>();

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
            try
            {
                _allEvents = await Task.Run(() => _metadataService.GetRecentSecurityEvents());

                Dispatcher.Invoke(() => {
                    AuditLogGrid.ItemsSource = _allEvents;
                    bool hasEvents = _allEvents.Any();
                    AuditLogGrid.Visibility = hasEvents ? Visibility.Visible : Visibility.Collapsed;
                    NoLogsContainer.Visibility = hasEvents ? Visibility.Collapsed : Visibility.Visible;

                    AuditSearchBox.Text = AuditSearchBox.Tag.ToString();
                    AuditSearchBox.Opacity = 0.5;
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private void AuditSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = AuditSearchBox.Text.ToLower();
            if (filter == AuditSearchBox.Tag.ToString().ToLower() || _allEvents == null) return;

            if (string.IsNullOrWhiteSpace(filter))
            {
                AuditLogGrid.ItemsSource = _allEvents;
            }
            else
            {
                AuditLogGrid.ItemsSource = _allEvents
                    .Where(x => x.Message.ToLower().Contains(filter) || x.ProcessInfo.ToLower().Contains(filter))
                    .ToList();
            }
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (AuditSearchBox.Text == AuditSearchBox.Tag?.ToString())
            {
                AuditSearchBox.Text = "";
                AuditSearchBox.Opacity = 1.0;
            }
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(AuditSearchBox.Text))
            {
                AuditSearchBox.Text = AuditSearchBox.Tag?.ToString() ?? "";
                AuditSearchBox.Opacity = 0.5;
            }
        }

    }
}
