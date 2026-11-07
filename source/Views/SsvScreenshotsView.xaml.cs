using CommonPluginsShared;
using CommonPluginsShared.Utilities;
using Playnite.SDK;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ScreenshotsVisualizer.Views
{
    /// <summary>
    /// Logique d'interaction pour SsvScreenshotsView.xaml
    /// </summary>
    public partial class SsvScreenshotsView : UserControl
    {
        private ScreenshotsVisualizerDatabase PluginDatabase => ScreenshotsVisualizer.PluginDatabase;

        private GameScreenshots gameScreenshots { get; set; }
        private bool _suppressListToolbarEvents;


        public SsvScreenshotsView(Game GameSelected)
        {
            InitializeComponent();

            PART_BtFolder.Visibility = Visibility.Collapsed;
            PART_ImgPath.Content = string.Empty;

            InitializeListToolbar();

            gameScreenshots = PluginDatabase.Get(GameSelected);
            RefreshScreenshotList();

            SetInfos();

            PART_Copy.Visibility = Visibility.Collapsed;
        }

        private const string SortCaretAscending = "\uea6a";
        private const string SortCaretDescending = "\uea67";

        private void InitializeListToolbar()
        {
            _suppressListToolbarEvents = true;
            try
            {
                PART_CbSortBy.ItemsSource = SsvScreenshotListOrder.CreateSortByChoices();
                PART_CbMediaFilter.ItemsSource = SsvScreenshotListOrder.CreateMediaFilterChoices();

                PART_CbSortBy.SelectedValue = PluginDatabase.PluginSettings.ScreenshotsListSortBy;
                PART_CbMediaFilter.SelectedValue = PluginDatabase.PluginSettings.ScreenshotsListMediaFilter;
                UpdateSortDirectionButton();
            }
            finally
            {
                _suppressListToolbarEvents = false;
            }
        }

        private void PART_ListToolbar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressListToolbarEvents)
            {
                return;
            }

            PersistListToolbarSettings();
            RefreshScreenshotList();
            SetInfos();
        }

        private void PART_BtSortDirection_Click(object sender, RoutedEventArgs e)
        {
            if (_suppressListToolbarEvents)
            {
                return;
            }

            SsvScreenshotSortDirection current = PluginDatabase.PluginSettings.ScreenshotsListSortDirection;
            PluginDatabase.PluginSettings.ScreenshotsListSortDirection =
                current == SsvScreenshotSortDirection.Ascending
                    ? SsvScreenshotSortDirection.Descending
                    : SsvScreenshotSortDirection.Ascending;

            UpdateSortDirectionButton();
            PersistListToolbarSettings();
            RefreshScreenshotList();
            SetInfos();
        }

        private void UpdateSortDirectionButton()
        {
            bool descending = PluginDatabase.PluginSettings.ScreenshotsListSortDirection
                == SsvScreenshotSortDirection.Descending;
            PART_TbSortDirectionCaret.Text = descending ? SortCaretDescending : SortCaretAscending;
            PART_BtSortDirection.ToolTip = ResourceProvider.GetString(
                descending ? "LOCSsvSortDescending" : "LOCSsvSortAscending");
        }

        private void PersistListToolbarSettings()
        {
            if (PART_CbSortBy.SelectedValue is SsvScreenshotSortBy sortBy)
            {
                PluginDatabase.PluginSettings.ScreenshotsListSortBy = sortBy;
            }

            if (PART_CbMediaFilter.SelectedValue is SsvScreenshotMediaFilter mediaFilter)
            {
                PluginDatabase.PluginSettings.ScreenshotsListMediaFilter = mediaFilter;
            }

            try
            {
                PluginDatabase.PersistSettingsAction?.Invoke();
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }

            Common.LogDebug(string.Format(
                "[SsvScreenshotsView][toolbar] sortBy={0} direction={1} filter={2}",
                PluginDatabase.PluginSettings.ScreenshotsListSortBy,
                PluginDatabase.PluginSettings.ScreenshotsListSortDirection,
                PluginDatabase.PluginSettings.ScreenshotsListMediaFilter));
        }


        private void PART_ListScreenshots_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PART_ListScreenshots.SelectedItem == null)
            {
                Common.LogDebug("[SsvScreenshotsView][preview] cleared");
                return;
            }

            Screenshot screenshot = (Screenshot)PART_ListScreenshots.SelectedItem;
            PART_BtFolder.Visibility = Visibility.Visible;
            PART_BtFolder.Tag = screenshot.FileName;
            PART_ImgPath.Content = screenshot.FileName;

            bool exists = File.Exists(screenshot.FileName);
            Common.LogDebug(string.Format(
                "[SsvScreenshotsView][preview] isVideo={0} exists={1} file={2}",
                screenshot.IsVideo,
                exists,
                screenshot.FileNameOnly));

            if (!exists)
            {
                return;
            }

            if (screenshot.IsVideo)
            {
                PART_Screenshot.Source = null;
                PART_Screenshot.Visibility = Visibility.Collapsed;

                PART_Video.Source = new Uri(screenshot.FileName);
                PART_Video.Visibility = Visibility.Visible;

                PART_Video.LoadedBehavior = MediaState.Play;
            }
            else
            {
                PART_Video.Source = null;
                PART_Video.Visibility = Visibility.Collapsed;

                CommonPluginsShared.Converters.ImageConverter imageConverter = new CommonPluginsShared.Converters.ImageConverter();
                PART_Screenshot.Source = (BitmapImage)imageConverter.Convert(new[] { screenshot.FileName, "0" }, null, null, null);
                PART_Screenshot.Visibility = Visibility.Visible;
            }
        }

        private void PART_BtDelete_Click(object sender, RoutedEventArgs e)
        {
            int index = int.Parse(((Button)sender).Tag.ToString());

            Screenshot screenshot = (Screenshot)PART_ListScreenshots.Items[index];

            MessageBoxResult RessultDialog = API.Instance.Dialogs.ShowMessage(
                string.Format(ResourceProvider.GetString("LOCSsvDeleteConfirm"), screenshot.FileNameOnly),
                PluginDatabase.PluginName,
                MessageBoxButton.YesNo
            );

            if (RessultDialog == MessageBoxResult.Yes)
            {
                try
                {
                    ClearScreenshotPreview();

                    SsvScreenshotDeleteResult result = PluginDatabase.TryDeleteScreenshot(gameScreenshots.Id, screenshot);
                    if (result == SsvScreenshotDeleteResult.Success
                        || result == SsvScreenshotDeleteResult.SkippedMissingPhysicalFile)
                    {
                        RefreshScreenshotList();
                        PART_ListScreenshots.SelectedIndex = -1;
                        SetInfos();
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, PluginDatabase.PluginName);
                }
            }
        }

        private void RefreshScreenshotList()
        {
            GameScreenshots cached = PluginDatabase.GetOnlyCache(gameScreenshots.Id);
            if (cached != null)
            {
                gameScreenshots = cached;
            }

            SsvScreenshotSortBy sortBy = PluginDatabase.PluginSettings.ScreenshotsListSortBy;
            SsvScreenshotSortDirection sortDirection = PluginDatabase.PluginSettings.ScreenshotsListSortDirection;
            SsvScreenshotMediaFilter mediaFilter = PluginDatabase.PluginSettings.ScreenshotsListMediaFilter;

            int skippedMissing;
            List<Screenshot> items = SsvScreenshotListOrder.Apply(
                gameScreenshots?.Items,
                mediaFilter,
                sortBy,
                sortDirection,
                out skippedMissing);

            PART_ListScreenshots.ItemsSource = items;

            SsvScreenshotListStats stats = SsvScreenshotListOrder.ComputeStats(items);
            Common.LogDebug(string.Format(
                "[SsvScreenshotsView][refresh] files={0} images={1} videos={2} skippedMissing={3} sortBy={4} direction={5} filter={6}",
                items.Count,
                stats.ImageCount,
                stats.VideoCount,
                skippedMissing,
                sortBy,
                sortDirection,
                mediaFilter));
        }

        private void ClearScreenshotPreview()
        {
            PART_Screenshot.Source = null;
            PART_Screenshot.Visibility = Visibility.Collapsed;
            PART_Screenshot.UpdateLayout();

            PART_Video.Source = null;
            PART_Video.Visibility = Visibility.Collapsed;

            PART_BtFolder.Visibility = Visibility.Collapsed;
            PART_BtFolder.Tag = null;
            PART_ImgPath.Content = string.Empty;
        }


        private void SetInfos()
        {
            IEnumerable<Screenshot> displayed = PART_ListScreenshots.ItemsSource as IEnumerable<Screenshot>;
            SsvScreenshotListStats stats = SsvScreenshotListOrder.ComputeStats(displayed);

            PART_ScreenshotsCount.Content = stats.ImageCount > 1
                ? string.Format(ResourceProvider.GetString("LOCSsvScreenshots"), stats.ImageCount)
                : string.Format(ResourceProvider.GetString("LOCSsvScreenshot"), stats.ImageCount);
            PART_ScreenshotsSize.Content = UtilityTools.SizeSuffix(stats.ImageTotalSize);

            PART_VideosCount.Content = stats.VideoCount > 1
                ? string.Format(ResourceProvider.GetString("LOCSsvVideos"), stats.VideoCount)
                : string.Format(ResourceProvider.GetString("LOCSsvVideo"), stats.VideoCount);
            PART_VideosSize.Content = UtilityTools.SizeSuffix(stats.VideoTotalSize);

            PART_FilesCount.Content = stats.FileCount;
            PART_FilesSize.Content = UtilityTools.SizeSuffix(stats.FileTotalSize);
        }


        private void PART_Video_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && this.Parent is Window)
            {
                ((Window)this.Parent).WindowState = ((Window)this.Parent).WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
        }

        private void PART_Screenshot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && this.Parent is Window)
            {
                ((Window)this.Parent).WindowState = ((Window)this.Parent).WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
        }


        private void PART_Copy_Click(object sender, RoutedEventArgs e)
        {
            Screenshot screenshot = PART_ListScreenshots.SelectedItem as Screenshot;
            if (screenshot != null && !screenshot.IsVideo && File.Exists(screenshot.FileName))
            {
                SsvClipboardHelper.TryCopyImageFile(screenshot.FileName);
            }
        }

        private void Grid_MouseEnter(object sender, MouseEventArgs e)
        {
            Screenshot screenshot = (Screenshot)PART_ListScreenshots.SelectedItem;
            if (screenshot != null && !screenshot.IsVideo && File.Exists(screenshot.FileName))
            {
                PART_Copy.Visibility = Visibility.Visible;
            }
        }

        private void Grid_MouseLeave(object sender, MouseEventArgs e)
        {
            PART_Copy.Visibility = Visibility.Collapsed;
        }


        private void PART_BtFolder_Click(object sender, RoutedEventArgs e)
        {
            string dirPath = Path.GetDirectoryName(PART_BtFolder.Tag.ToString());
            if (Directory.Exists(dirPath))
            {
                Process.Start(dirPath);
            }
        }
    }
}
