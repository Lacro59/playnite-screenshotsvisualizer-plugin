using CommonPluginsShared;
using Playnite.SDK;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
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
        private static readonly ILogger logger = LogManager.GetLogger();
        private static readonly IResourceProvider resources = new ResourceProvider();

        private readonly ScreenshotsVisualizerDatabase PluginDatabase = ScreenshotsVisualizer.PluginDatabase;

        private GameScreenshots gameScreenshots;


        public SsvScreenshotsView(Game GameSelected)
        {
            InitializeComponent();

            PART_BtFolder.Visibility = Visibility.Collapsed;
            PART_ImgPath.Content = string.Empty;

            gameScreenshots = PluginDatabase.Get(GameSelected);
            List<Screenshot> Items = gameScreenshots.Items;
            Items.Sort((x, y) => y.Modifed.CompareTo(x.Modifed));

            PART_ListScreenshots.ItemsSource = Items;

            SetInfos();

            PART_Copy.Visibility = Visibility.Collapsed;
        }


        private void PART_ListScreenshots_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PART_ListScreenshots.SelectedItem != null)
            {
                Screenshot screenshot = (Screenshot)PART_ListScreenshots.SelectedItem;
                PART_BtFolder.Visibility = Visibility.Visible;
                PART_BtFolder.Tag = screenshot.FileName;
                PART_ImgPath.Content = screenshot.FileName;

                if (File.Exists(screenshot.FileName))
                {
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
            }
        }

        private void PART_BtDelete_Click(object sender, RoutedEventArgs e)
        {
            int index = int.Parse(((Button)sender).Tag.ToString());

            Screenshot screenshot = (Screenshot)PART_ListScreenshots.Items[index];

            MessageBoxResult RessultDialog = PluginDatabase.PlayniteApi.Dialogs.ShowMessage(
                string.Format(resources.GetString("LOCSsvDeleteConfirm"), screenshot.FileNameOnly),
                PluginDatabase.PluginName,
                MessageBoxButton.YesNo
            );

            if (RessultDialog == MessageBoxResult.Yes)
            {
                try
                {
                    if (File.Exists(screenshot.FileName))
                    {
                        PART_Screenshot.Source = null;
                        PART_Screenshot.UpdateLayout();

                        Task.Run(() => 
                        {
                            while(IsFileLocked(new FileInfo(screenshot.FileName)))
                            {

                            }

                            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                                screenshot.FileName,
                                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin,
                                Microsoft.VisualBasic.FileIO.UICancelOption.ThrowException);
                            });

                        gameScreenshots.Items.Remove(screenshot);
                        PluginDatabase.Update(gameScreenshots);
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, PluginDatabase.PluginName);
                }

                List<Screenshot> Items = gameScreenshots.Items;
                Items.Sort((x, y) => y.Modifed.CompareTo(x.Modifed));

                PART_ListScreenshots.SelectedIndex = -1;
                PART_ListScreenshots.ItemsSource = null;
                PART_ListScreenshots.ItemsSource = Items;

                SetInfos();
            }
            else
            {

            }
        }

        protected bool IsFileLocked(FileInfo file)
        {
            FileStream stream = null;

            try
            {
                stream = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            }
            finally
            {
                if (stream != null)
                {
                    stream.Close();
                }
            }

            //file is not locked
            return false;
        }


        private void SetInfos()
        {
            List<Screenshot> screenOnly = gameScreenshots.Items.FindAll(x => !x.IsVideo);
            int ScreenshotsCount = screenOnly.Count;
            long ScreenshotsTotalSize = 0;
            foreach (Screenshot item in screenOnly)
            {
                ScreenshotsTotalSize += item.FileSize;
            }
            PART_ScreenshotsCount.Content = ScreenshotsCount > 1 ? string.Format(resources.GetString("LOCSsvScreenshots"), ScreenshotsCount) : string.Format(resources.GetString("LOCSsvScreenshot"), ScreenshotsCount);
            PART_ScreenshotsSize.Content = Tools.SizeSuffix(ScreenshotsTotalSize);


            List<Screenshot> videoOnly = gameScreenshots.Items.FindAll(x => x.IsVideo);
            int VideosCount = videoOnly.Count;
            long VideosTotalSize = 0;
            foreach (Screenshot item in videoOnly)
            {
                VideosTotalSize += item.FileSize;
            }
            PART_VideosCount.Content = VideosCount > 1 ? string.Format(resources.GetString("LOCSsvVideos"), VideosCount) : string.Format(resources.GetString("LOCSsvVideo"), VideosCount);
            PART_VideosSize.Content = Tools.SizeSuffix(VideosTotalSize);


            PART_FilesCount.Content = ScreenshotsCount + VideosCount;
            PART_FilesSize.Content = Tools.SizeSuffix(ScreenshotsTotalSize + VideosTotalSize);
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
            if (!screenshot?.IsVideo ?? false && File.Exists(screenshot?.FileName))
            {
                try
                {
                    System.Drawing.Image img = System.Drawing.Image.FromFile(screenshot.FileName);
                    Clipboard.SetDataObject(img);
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false);
                }
            }
        }

        private void Grid_MouseEnter(object sender, MouseEventArgs e)
        {
            Screenshot screenshot = (Screenshot)PART_ListScreenshots.SelectedItem;
            if (!screenshot?.IsVideo ?? false && File.Exists(screenshot?.FileName))
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
