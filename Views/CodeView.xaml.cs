using SQLAtlas.Models;
using SQLAtlas.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SQLAtlas.Views
{
    public partial class CodeView : UserControl
    {
        private readonly MetadataService _metadataService = new MetadataService();
        private readonly DatabaseObject _target;

        public CodeView(DatabaseObject target)
        {
            InitializeComponent();
            _target = target;
            this.Loaded += CodeView_Loaded;
        }

        private async void CodeView_Loaded(object sender, RoutedEventArgs e)
        {
            ObjectIconHeader.Text = _target.Type == "P" ? "⚡" : "ƒ";
            ObjectContextHeader.Text = _target.Name.ToUpper();
            ObjectSubHeader.Text = $"SCHEMA: {_target.SchemaName} | TYPE: {_target.TypeDescription.ToUpper()}";

            try
            {
                // Retrieve procedure details
                var details = await Task.Run(() => _metadataService.GetObjectDetails(_target.SchemaName, _target.Name));

                CodeDefinitionTextBox.Text = details.Definition;
                CreateDateTextBlock.Text = details.CreateDate == DateTime.MinValue ? "N/A" : details.CreateDate.ToString("yyyy-MM-dd HH:mm");
                ModifyDateTextBlock.Text = details.ModifyDate == DateTime.MinValue ? "N/A" : details.ModifyDate.ToString("yyyy-MM-dd HH:mm");

                // Populate Tabs
                BindDataWithEmptyCheck(ParameterGrid, NoParametersTextBlock, details.Parameters);

                var deps = await Task.Run(() => _metadataService.GetObjectDependencies(_target.SchemaName, _target.Name));
                BindDataWithEmptyCheck(DependencyGrid, NoDependenciesTextBlock, deps);

                var perms = await Task.Run(() => _metadataService.GetObjectPermissions(_target.Name));
                BindDataWithEmptyCheck(PermissionsDataGrid, NoPermissionsTextBlock, perms);
            }
            catch (Exception ex)
            {
                CodeDefinitionTextBox.Text = $"-- Error loading metadata: {ex.Message}";
            }
        }

        private void BindDataWithEmptyCheck<T>(DataGrid grid, TextBlock emptyLabel, List<T> data)
        {
            if (data == null || data.Count == 0)
            {
                grid.Visibility = Visibility.Collapsed;
                emptyLabel.Visibility = Visibility.Visible;
            }
            else
            {
                grid.ItemsSource = data;
                grid.Visibility = Visibility.Visible;
                emptyLabel.Visibility = Visibility.Collapsed;
            }
        }

        private async void CopyCodeButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(CodeDefinitionTextBox.Text))
            {
                Clipboard.SetText(CodeDefinitionTextBox.Text);
                var btn = (Button)sender;
                btn.Content = "COPIED!";
                btn.Foreground = (SolidColorBrush)Application.Current.Resources["AccentColor"];

                await Task.Delay(2000);

                btn.Content = "COPY";
                btn.Foreground = (SolidColorBrush)Application.Current.Resources["MutedFontColor"];
            }
        }
    }
}