using CommonPlayniteShared.Common;
using CommonPluginsShared;
using ScreenshotsVisualizer.Services;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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

        private void ActiveSourcesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
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
    }
}
