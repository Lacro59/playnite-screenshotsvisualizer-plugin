using ScreenshotsVisualizer.Models;
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
    /// Logique d'interaction pour SsvSinglePictureView.xaml
    /// </summary>
    public partial class SsvSinglePictureView : UserControl
    {
        private DispatcherTimer timer;


        public SsvSinglePictureView(Screenshot screenshot)
        {
            InitializeComponent();

            string PictureSource = string.Empty;
            if (File.Exists(screenshot.FileName))
            {
                PictureSource = screenshot.FileName;
            }


            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += timer_Tick;
            timer.Start();


            this.DataContext = new
            {
                PictureSource,
                IsVideo = screenshot.IsVideo
            };
        }


        private void PART_Contener_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            PART_ScreenshotsPicture.Height = PART_Contener.ActualHeight;
            PART_ScreenshotsPicture.Width = PART_Contener.ActualWidth;
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
                e.Handled = true;

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
                e.Handled = true;

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
