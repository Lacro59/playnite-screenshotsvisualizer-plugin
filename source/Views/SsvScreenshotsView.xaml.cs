using CommonPluginsShared;
using Playnite.SDK;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.Services;
using System;
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
        private static IResourceProvider resources = new ResourceProvider();

        private ScreenshotsVisualizerDatabase PluginDatabase = ScreenshotsVisualizer.PluginDatabase;

        private GameScreenshots gameScreenshots;


        public SsvScreenshotsView(Game GameSelected)
        {
            InitializeComponent();

            gameScreenshots = PluginDatabase.Get(GameSelected);
            var Items = gameScreenshots.Items;
            Items.Sort((x, y) => y.Modifed.CompareTo(x.Modifed));

            PART_ListScreenshots.ItemsSource = Items;

            SetInfos();
        }


        private void PART_ListScreenshots_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PART_ListScreenshots.SelectedItem != null)
            {
                Screenshot screenshot = (Screenshot)PART_ListScreenshots.SelectedItem;

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

            var RessultDialog = PluginDatabase.PlayniteApi.Dialogs.ShowMessage(
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

                var Items = gameScreenshots.Items;
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
                    stream.Close();
            }

            //file is not locked
            return false;
        }


        private void SetInfos()
        {
            PART_FilesCount.Content = gameScreenshots.Items.Count + " " + resources.GetString("LOCSsvTitle");

            long TotalSize = 0;
            foreach(var item in gameScreenshots.Items)
            {
                TotalSize += item.FileSize;
            }

            PART_FilesSize.Content = Tools.SizeSuffix(TotalSize);
        }


        private void PART_Video_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                if (this.Parent is Window)
                {
                    if (((Window)this.Parent).WindowState == WindowState.Maximized)
                    {
                        ((Window)this.Parent).WindowState = WindowState.Normal;
                    }
                    else
                    {
                        ((Window)this.Parent).WindowState = WindowState.Maximized;
                    }
                }
            }
        }

        private void PART_Screenshot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                if (this.Parent is Window)
                {
                    if (((Window)this.Parent).WindowState == WindowState.Maximized)
                    {
                        ((Window)this.Parent).WindowState = WindowState.Normal;
                    }
                    else
                    {
                        ((Window)this.Parent).WindowState = WindowState.Maximized;
                    }
                }
            }
        }
    }
}
