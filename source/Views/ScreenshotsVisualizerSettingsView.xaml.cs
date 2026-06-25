using CommonPlayniteShared.Common;
using CommonPluginsControls.Controls;
using CommonPluginsShared;
using CommonPluginsShared.UI;
using Playnite.SDK;
using ScreenshotsVisualizer.Services;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ScreenshotsVisualizer.Views
{
    public partial class ScreenshotsVisualizerSettingsView : UserControl
    {
        private ScreenshotsVisualizerDatabase PluginDatabase => ScreenshotsVisualizer.PluginDatabase;

        public ScreenshotsVisualizerSettingsView()
        {
            InitializeComponent();
        }

        private void EditSelectedActiveSource_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ScreenshotsVisualizerSettingsViewModel viewModel)
            {
                viewModel.ConfigurationContext.EditSelectedActiveSource();
            }
        }

        private void ActiveSourcesList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataContext is ScreenshotsVisualizerSettingsViewModel viewModel
                && viewModel.ConfigurationContext.HasSelectedActiveSource)
            {
                viewModel.ConfigurationContext.EditSelectedActiveSource();
            }
        }

        private void ButtonClear_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string pathThumbnail = System.IO.Path.Combine(PluginDatabase.Paths.PluginCachePath, "Thumbnails");
                FileSystem.DeleteDirectory(pathThumbnail);
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }
        }

        private void ContextSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!(DataContext is ScreenshotsVisualizerSettingsViewModel viewModel))
            {
                return;
            }

            string searchText = string.Empty;
            if (sender != null)
            {
                var textProperty = sender.GetType().GetProperty("Text");
                searchText = textProperty?.GetValue(sender)?.ToString() ?? string.Empty;
            }

            if (!string.Equals(viewModel.ConfigurationContext.ContextSearchText, searchText, StringComparison.Ordinal))
            {
                viewModel.ConfigurationContext.ContextSearchText = searchText;
            }
        }

        private void SelectFolderVariableButton_Click(object sender, RoutedEventArgs e)
        {
            if (TryPickVariable(SelectVariableMode.Path, out string variableToken))
            {
                InsertVariableToken(PART_FolderToSave, variableToken);
            }
        }

        private void SelectFilePatternVariableButton_Click(object sender, RoutedEventArgs e)
        {
            if (TryPickVariable(SelectVariableMode.FilePattern, out string variableToken))
            {
                InsertVariableToken(PART_FileSavePattern, variableToken);
            }
        }

        private static bool TryPickVariable(SelectVariableMode mode, out string variableToken)
        {
            variableToken = null;
            var picker = new SelectVariable(mode);
            Window window = PlayniteUiHelper.CreateExtensionWindow(
                ResourceProvider.GetString("LOCCommonSelectVariable"),
                picker);
            window.ResizeMode = ResizeMode.CanResize;

            if (window.ShowDialog() == true && picker.WasSelected)
            {
                variableToken = picker.SelectedVariable;
                return true;
            }

            return false;
        }

        private static void InsertVariableToken(TextBox targetTextBox, string variableToken)
        {
            if (targetTextBox == null || string.IsNullOrWhiteSpace(variableToken))
            {
                return;
            }

            var currentText = targetTextBox.Text ?? string.Empty;
            var selectionStart = targetTextBox.SelectionStart;
            var selectionLength = targetTextBox.SelectionLength;

            if (selectionStart < 0 || selectionStart > currentText.Length)
            {
                selectionStart = currentText.Length;
            }

            if (selectionLength < 0)
            {
                selectionLength = 0;
            }

            if (selectionStart + selectionLength > currentText.Length)
            {
                selectionLength = currentText.Length - selectionStart;
            }

            var updatedText = currentText.Remove(selectionStart, selectionLength).Insert(selectionStart, variableToken);
            targetTextBox.Text = updatedText;
            targetTextBox.Focus();
            targetTextBox.CaretIndex = selectionStart + variableToken.Length;
        }
    }
}
