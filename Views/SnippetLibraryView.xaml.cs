using SQLAtlas.Models;
using SQLAtlas.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    public partial class SnippetLibraryView : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();
        private readonly List<SqlSnippet> _allSnippets;

        // Field to hold the ICollectionView for grouping/sorting
        private ICollectionView? _snippetCollectionView;

        public SnippetLibraryView()
        {
            InitializeComponent();

            // Fetch the static list of snippets once
            _allSnippets = _metadataService.GetSnippetLibrary();

            this.Loaded += SnippetLibraryView_Loaded;
        }

        private void SnippetLibraryView_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSnippets();
        }

        private void LoadSnippets()
        {
            // 1. Assign source and get CollectionView
            SnippetListBox.ItemsSource = _allSnippets;
            _snippetCollectionView = CollectionViewSource.GetDefaultView(SnippetListBox.ItemsSource);

            // 2. Apply Grouping by Category
            if (_snippetCollectionView is not null && _snippetCollectionView.CanGroup)
            {
                _snippetCollectionView.GroupDescriptions.Clear();
                _snippetCollectionView.GroupDescriptions.Add(new PropertyGroupDescription("Category"));
            }
        }

        /// <summary>
        /// Updates the right pane with details when a snippet is selected.
        /// </summary>
        private void SnippetListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SnippetListBox.SelectedItem is SqlSnippet selectedSnippet)
            {
                SnippetTitleTextBlock.Text = selectedSnippet.Title;
                SnippetDescriptionTextBlock.Text = $"Category: {selectedSnippet.Category}\n\n{selectedSnippet.Description}";
                CodeDisplayTextBox.Text = selectedSnippet.Code;
                CopyButton.IsEnabled = true;
            }
            else
            {
                SnippetTitleTextBlock.Text = "Select a SQL Snippet";
                SnippetDescriptionTextBlock.Text = "Select a snippet from the left to view details and code.";
                CodeDisplayTextBox.Text = string.Empty;
                CopyButton.IsEnabled = false;
            }
        }

        /// <summary>
        /// Copies the currently displayed T-SQL code to the Windows clipboard.
        /// </summary>
        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(CodeDisplayTextBox.Text))
            {
                Clipboard.SetText(CodeDisplayTextBox.Text);
                CopyButton.Content = "✅ Copied!";

                // Reset button text after a short delay
                System.Windows.Threading.DispatcherTimer timer = new System.Windows.Threading.DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(2);
                timer.Tick += (s, ev) =>
                {
                    CopyButton.Content = "📋 Copy Code to Clipboard";
                    timer.Stop();
                };
                timer.Start();
            }
        }
    }
}