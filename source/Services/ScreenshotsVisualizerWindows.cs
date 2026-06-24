using CommonPluginsShared;
using CommonPluginsShared.Extensions;
using CommonPluginsShared.Interfaces;
using CommonPluginsShared.Plugins;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.Views;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace ScreenshotsVisualizer.Services
{
    /// <summary>
    /// Centralizes all ScreenshotsVisualizer windows.
    /// </summary>
    public class ScreenshotsVisualizerWindows : PluginWindows
    {
        private ScreenshotsVisualizerDatabase Database => (ScreenshotsVisualizerDatabase)PluginDatabase;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScreenshotsVisualizerWindows"/> class.
        /// </summary>
        /// <param name="pluginName">Plugin name used in shared window helpers.</param>
        /// <param name="pluginDatabase">Plugin database instance.</param>
        public ScreenshotsVisualizerWindows(string pluginName, IPluginDatabase pluginDatabase) : base(pluginName, pluginDatabase)
        {
        }

        /// <summary>
        /// Opens a screenshot or video file with the system default application.
        /// </summary>
        /// <param name="filePath">Full path to the media file.</param>
        public static void OpenWithExternalViewer(string filePath)
        {
            if (filePath.IsNullOrEmpty() || !File.Exists(filePath))
            {
                return;
            }

            Common.LogDebug(true, string.Format("[SsvWindows] Open screenshot with external viewer: {0}", Path.GetFileName(filePath)));
            _ = Process.Start(filePath);
        }

        /// <inheritdoc />
        public override void ShowPluginGameDataWindow(GenericPlugin plugin, Game gameContext)
        {
            ShowScreenshotsView(gameContext);
        }

        /// <inheritdoc />
        public override void ShowPluginGameDataWindow(GenericPlugin plugin)
        {
            ShowScreenshotsView(Database.GameContext);
        }

        /// <inheritdoc />
        public override void ShowPluginGameDataWindow(Game gameContext)
        {
            ShowScreenshotsView(gameContext);
        }

        /// <summary>
        /// Opens the single picture viewer for one screenshot.
        /// </summary>
        /// <param name="screenshot">Screenshot to display.</param>
        /// <param name="screenshots">Optional screenshots list for navigation.</param>
        /// <param name="customTitle">Optional custom window title.</param>
        public void ShowSinglePictureWindow(Screenshot screenshot, List<Screenshot> screenshots, string customTitle = null)
        {
            if (screenshot == null)
            {
                return;
            }

            WindowOptions windowOptions = new WindowOptions
            {
                ShowMinimizeButton = false,
                ShowMaximizeButton = true,
                ShowCloseButton = true,
                CanBeResizable = true,
                Height = 720,
                Width = 1280
            };

            string title = customTitle ?? (ResourceProvider.GetString("LOCSsv") + " - " + screenshot.FileNameOnly);
            SsvSinglePictureView viewExtension = new SsvSinglePictureView(screenshot, screenshots);
            Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(title, viewExtension, windowOptions);
            _ = windowExtension.ShowDialog();
        }

        private void ShowScreenshotsView(Game gameContext)
        {
            if (gameContext == null)
            {
                return;
            }

            WindowOptions windowOptions = new WindowOptions
            {
                ShowMinimizeButton = false,
                ShowMaximizeButton = true,
                ShowCloseButton = true,
                CanBeResizable = true,
                Height = 720,
                Width = 1200
            };

            SsvScreenshotsView viewExtension = new SsvScreenshotsView(gameContext);
            Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(
                ResourceProvider.GetString("LOCSsvTitle"),
                viewExtension,
                windowOptions);
            _ = windowExtension.ShowDialog();
        }
    }
}
