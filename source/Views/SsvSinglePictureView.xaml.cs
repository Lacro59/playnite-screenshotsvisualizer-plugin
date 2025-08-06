using CommonPluginsShared;
using Playnite.SDK;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Controls;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.Services;
using System;
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
        private static ScreenshotsVisualizerDatabase PluginDatabase => ScreenshotsVisualizer.PluginDatabase;

        private List<Screenshot> Screenshots { get; set; } = new List<Screenshot>();
        private Screenshot Screenshot { get; set; }
        private int Index { get; set; } = 0;


        public SsvSinglePictureView(Screenshot screenshot, List<Screenshot> screenshots = null)
        {
            InitializeComponent();

            Screenshots = screenshots;
            if (screenshots != null)
            {
                Index = screenshots.FindIndex(x => x == screenshot);
            }

            ButtonNext.Visibility = Visibility.Collapsed;
            ButtonPrev.Visibility = Visibility.Collapsed;
            PART_Bt.Visibility = Visibility.Collapsed;
            PART_Game.Visibility = Visibility.Collapsed;

            SetImage(screenshot);
        }

        private void SetImage(Screenshot screenshot)
        {
            string pictureSource = string.Empty;
            Game game = API.Instance.Database.Games.Get(screenshot.GameId);

            if (File.Exists(screenshot.FileName))
            {
                pictureSource = screenshot.FileName;
                Screenshot = screenshot;

                if (Parent is Window window)
                {
                    window.Title = game != null
                        ? ResourceProvider.GetString("LOCSsv") + " - " + game.Name + " - " + screenshot.FileNameOnly
                        : ResourceProvider.GetString("LOCSsv") + " - " + screenshot.FileNameOnly;
                }
            }

            DataContext = new
            {
                PictureSource = pictureSource,
                IsVideo = screenshot.IsVideo,
                Icon = !game?.Icon.IsNullOrEmpty() ?? false ? API.Instance.Database.GetFullFilePath(game.Icon) : string.Empty,
                GameName = game?.Name,
                GameId = game?.Id,
                GoToGame = Commands.GoToGame
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

                if (Parent is Window window)
                {
                    window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                }
            }
        }

        private void PART_Screenshot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                e.Handled = true;

                if (Parent is Window window)
                {
                    window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                }
            }
        }

        #region Image navigation

        private void ButtonPrev_Click(object sender, RoutedEventArgs e)
        {
            if (Screenshots != null)
            {
                if (Index == 0)
                {
                    Index = Screenshots.Count - 1;
                }
                else
                {
                    Index--;
                }

                SetImage(Screenshots[Index]);
            }
        }

        private void ButtonNext_Click(object sender, RoutedEventArgs e)
        {
            if (Screenshots != null)
            {
                if (Index == Screenshots.Count - 1)
                {
                    Index = 0;
                }
                else
                {
                    Index++;
                }

                SetImage(Screenshots[Index]);
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

                default:
                    break;
            }
        }

        private void PART_Contener_Loaded(object sender, RoutedEventArgs e)
        {
            Window window = UI.FindParent<Window>((FrameworkElement)sender);
            window.KeyDown += new KeyEventHandler(SsvSinglePictureView_KeyDown);
            window.MouseEnter += PART_Contener_MouseEnter;
            window.MouseLeave += PART_Contener_MouseLeave;
        }

        private void PART_Contener_MouseEnter(object sender, MouseEventArgs e)
        {
            if (Screenshots?.Count > 2)
            {
                ButtonNext.Visibility = Visibility.Visible;
                ButtonPrev.Visibility = Visibility.Visible;
            }

            if (!Screenshot?.IsVideo ?? true)
            {
                PART_Bt.Visibility = Visibility.Visible;
                PART_Game.Visibility = Visibility.Visible;
            }
        }

        private void PART_Contener_MouseLeave(object sender, MouseEventArgs e)
        {
            ButtonNext.Visibility = Visibility.Collapsed;
            ButtonPrev.Visibility = Visibility.Collapsed;
            PART_Bt.Visibility = Visibility.Collapsed;
            PART_Game.Visibility = Visibility.Collapsed;
        }

        private void PART_Copy_Click(object sender, RoutedEventArgs e)
        {
            if ((!Screenshot?.IsVideo ?? true) && File.Exists(Screenshot.FileName))
            {
                try
                {
                    System.Drawing.Image img = System.Drawing.Image.FromFile(Screenshot.FileName);
                    Clipboard.SetDataObject(img);
                }
                catch(Exception ex)
                {
                    Common.LogError(ex, false);
                }
            }
        }

        private void PART_Expand_Click(object sender, RoutedEventArgs e)
        {
            if ((!Screenshot?.IsVideo ?? true) && File.Exists(Screenshot.FileName))
            {
                ZoomBorder parent = UI.FindParent<ZoomBorder>(PART_ScreenshotsPicture);
                if (parent != null)
                {
                    parent.Reset();
                }
            }
        }
    }
}