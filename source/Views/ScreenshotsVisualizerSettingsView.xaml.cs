using CommonPlayniteShared.Common;
using CommonPluginsControls.Controls;
using CommonPluginsShared;
using CommonPluginsShared.UI;
using Playnite.SDK;
using ScreenshotsVisualizer.Services;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ScreenshotsVisualizer.Views
{
    public partial class ScreenshotsVisualizerSettingsView : UserControl
    {
        private const double ConfigurationPanelMinHeight = 280;
        private const double ConfigurationPanelReservedHeight = 130;

        private ScreenshotsVisualizerDatabase PluginDatabase => ScreenshotsVisualizer.PluginDatabase;

        public ScreenshotsVisualizerSettingsView()
        {
            InitializeComponent();
            Loaded += ScreenshotsVisualizerSettingsView_Loaded;
            SizeChanged += ScreenshotsVisualizerSettingsView_SizeChanged;
        }

        private void ScreenshotsVisualizerSettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(UpdateConfigurationPanelHeight), DispatcherPriority.Loaded);
        }

        private void ScreenshotsVisualizerSettingsView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.HeightChanged)
            {
                UpdateConfigurationPanelHeight();
            }
        }

        private void PART_TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateConfigurationPanelHeight();

            if (PART_CacheTab != null
                && PART_TabControl?.SelectedItem == PART_CacheTab
                && DataContext is ScreenshotsVisualizerSettingsViewModel viewModel)
            {
                viewModel.RefreshThumbnailCacheInfo();
            }
        }

        /// <summary>
        /// Sizes the configuration master-detail panel to the available settings viewport height.
        /// </summary>
        private void UpdateConfigurationPanelHeight()
        {
            if (PART_ConfigurationPanel == null)
            {
                return;
            }

            double viewportHeight = GetSettingsViewportHeight();
            if (viewportHeight <= 0)
            {
                return;
            }

            double availableHeight = viewportHeight - ConfigurationPanelReservedHeight;
            if (availableHeight < ConfigurationPanelMinHeight)
            {
                availableHeight = ConfigurationPanelMinHeight;
            }

            PART_ConfigurationPanel.MaxHeight = availableHeight;
        }

        /// <summary>
        /// Resolves the usable vertical space for the settings view.
        /// </summary>
        private double GetSettingsViewportHeight()
        {
            if (PART_TabControl != null && PART_TabControl.ActualHeight > 0)
            {
                return PART_TabControl.ActualHeight;
            }

            if (ActualHeight > 0)
            {
                return ActualHeight;
            }

            var element = Parent as FrameworkElement;
            while (element != null)
            {
                if (element.ActualHeight > 0)
                {
                    return element.ActualHeight;
                }

                element = element.Parent as FrameworkElement;
            }

            return 0;
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

        private void EditSelectedConversionCommand_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ScreenshotsVisualizerSettingsViewModel viewModel)
            {
                viewModel.ImageConversionSettings.EditSelectedCommand();
            }
        }

        private void ConversionCommandsList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataContext is ScreenshotsVisualizerSettingsViewModel viewModel
                && viewModel.ImageConversionSettings.HasSelectedCommand)
            {
                viewModel.ImageConversionSettings.EditSelectedCommand();
            }
        }

        private void ButtonClear_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string pathThumbnail = System.IO.Path.Combine(
                    PluginDatabase.Paths.PluginCachePath,
                    SsvThumbnailService.ThumbnailsFolderName);
                FileSystem.DeleteDirectory(pathThumbnail);
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }

            if (DataContext is ScreenshotsVisualizerSettingsViewModel viewModel)
            {
                viewModel.RefreshThumbnailCacheInfo();
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
