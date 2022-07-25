using CommonPluginsShared;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ScreenshotsVisualizer.Views
{
    /// <summary>
    /// Logique d'interaction pour SsvSinglePictureView.xaml
    /// </summary>
    public partial class SsvSinglePictureView : UserControl
    {
        private List<Screenshot> screenshots = new List<Screenshot>();
        private int index = 0;


        public SsvSinglePictureView(Screenshot screenshot, List<Screenshot> screenshots = null)
        {
            this.screenshots = screenshots;
            if (screenshots != null)
            {
                index = screenshots.FindIndex(x => x == screenshot);
            }
            else
            {
                ButtonNext.Visibility = Visibility.Collapsed;
                ButtonPrev.Visibility = Visibility.Collapsed;
            }

            InitializeComponent();

            SetImage(screenshot);
        }


        private void SetImage(Screenshot screenshot)
        {
            string PictureSource = string.Empty;
            if (File.Exists(screenshot.FileName))
            {
                PictureSource = screenshot.FileName;
            }


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


        #region Image navigation
        private void ButtonPrev_Click(object sender, RoutedEventArgs e)
        {
            if (screenshots != null)
            {
                if (index == 0)
                {
                    index = screenshots.Count - 1;
                }
                else
                {
                    index--;
                }

                SetImage(screenshots[index]);
            }
        }

        private void ButtonNext_Click(object sender, RoutedEventArgs e)
        {
            if (screenshots != null)
            {
                if (index == screenshots.Count - 1)
                {
                    index = 0;
                }
                else
                {
                    index++;
                }

                SetImage(screenshots[index]);
            }
        }
        #endregion


        private void SsvSinglePictureView_KeyDown(object sender, KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.Key)
            {
                case Key.Right:
                    ButtonNext_Click(null, null);
                    break;
                case Key.Left:
                    ButtonPrev_Click(null, null);
                    break;
            }
        }

        private void PART_Contener_Loaded(object sender, RoutedEventArgs e)
        {
            Window win = UI.FindParent<Window>((FrameworkElement)sender);
            win.KeyDown += new KeyEventHandler(SsvSinglePictureView_KeyDown);
        }
    }
}
