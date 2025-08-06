using CommonPluginsShared;
using Playnite.SDK;
using Playnite.SDK.Plugins;
using ScreenshotsVisualizer.Views;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ScreenshotsVisualizer.Services
{
    public class ScreenshotsVisualizerTopPanelItem : TopPanelItem
    {
        public ScreenshotsVisualizerTopPanelItem(ScreenshotsVisualizer plugin)
        {
            Icon = new TextBlock
            {
                Text = "\uea38",
                FontSize = 20,
                FontFamily = ResourceProvider.GetResource("CommonFont") as FontFamily
            };
            Title = ResourceProvider.GetString("LOCSsv");
            Activated = () =>
            {
                WindowOptions windowOptions = new WindowOptions
                {
                    ShowMinimizeButton = false,
                    ShowMaximizeButton = true,
                    ShowCloseButton = true,
                    CanBeResizable = true,
                    Width = 1200,
                    Height = 720
                };

                SsvScreenshotsManager viewExtension = new SsvScreenshotsManager();
                Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(ResourceProvider.GetString("LOCSsv"), viewExtension, windowOptions);
                _ = windowExtension.ShowDialog();
            };
            Visible = plugin.PluginSettings.Settings.EnableIntegrationButtonHeader;
        }
    }
}