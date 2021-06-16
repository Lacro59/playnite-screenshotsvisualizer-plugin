using CommonPluginsShared;
using Playnite.SDK;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.Services;
using System;
using System.Collections.Generic;
using System.IO;
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
using System.Windows.Threading;

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

        private DispatcherTimer timer;


        public SsvScreenshotsView(Game GameSelected)
        {
            InitializeComponent();

            gameScreenshots = PluginDatabase.Get(GameSelected);
            var Items = gameScreenshots.Items;
            Items.Sort((x, y) => y.Modifed.CompareTo(x.Modifed));

            PART_ListScreenshots.ItemsSource = Items;

            SetInfos();


            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += timer_Tick;
            timer.Start();
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
                        timer.Start();

                        lblStatus.Content = "00:00:00 / 00:00:00";
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
                "ScreenshotsVisualizer",
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
                    Common.LogError(ex, false);
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


        #region Media
        private void timer_Tick(object sender, EventArgs e)
        {
            if (PART_Video.Source != null)
            {
                if (PART_Video.NaturalDuration.HasTimeSpan)
                {
                    timelineSlider.Value = PART_Video.Position.TotalSeconds;
                    lblStatus.Content = PART_Video.Position.ToString(@"hh\:mm\:ss") + " / " + PART_Video.NaturalDuration.TimeSpan.ToString(@"hh\:mm\:ss");
                }
            }
        }


        // Play the media.
        private void OnMouseDownPlayMedia(object sender, RoutedEventArgs e)
        {
            PART_Video.LoadedBehavior = MediaState.Play;
            timer.Start();
        }

        // Pause the media.
        private void OnMouseDownPauseMedia(object sender, RoutedEventArgs e)
        {
            PART_Video.LoadedBehavior = MediaState.Pause;
            timer.Stop();
        }

        // Change the volume of the media.
        private void ChangeMediaVolume(object sender, RoutedPropertyChangedEventArgs<double> args)
        {
            PART_Video.Volume = (double)volumeSlider.Value;
        }

        // When the media opens, initialize the "Seek To" slider maximum value
        // to the total number of miliseconds in the length of the media clip.
        private void PART_Video_MediaOpened(object sender, EventArgs e)
        {
            timelineSlider.Maximum = PART_Video.NaturalDuration.TimeSpan.TotalSeconds;
        }

        // Jump to different parts of the media (seek to).
        private void SeekToMediaPosition(object sender, RoutedPropertyChangedEventArgs<double> args)
        {
            //int SliderValue = (int)timelineSlider.Value;
            //
            //// Overloaded constructor takes the arguments days, hours, minutes, seconds, milliseconds.
            //// Create a TimeSpan with miliseconds equal to the slider value.
            //TimeSpan ts = new TimeSpan(0, 0, 0, 0, SliderValue);
            //PART_Video.Position = ts;
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
            else
            {
                if (PART_Video.LoadedBehavior == MediaState.Pause)
                {
                    PART_Video.LoadedBehavior = MediaState.Play;
                    timer.Start();
                }
                else
                {
                    PART_Video.LoadedBehavior = MediaState.Pause;
                    timer.Stop();
                }
            }
        }
        #endregion


        private void Grid_Unloaded(object sender, RoutedEventArgs e)
        {
            if (timer != null)
            {
                timer.Stop();
                timer = null;
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
