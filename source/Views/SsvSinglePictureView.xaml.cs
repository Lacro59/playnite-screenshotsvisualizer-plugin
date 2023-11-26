using CommonPlayniteShared;
using CommonPluginsShared;
using Playnite.SDK;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
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
        internal static IResourceProvider resources => new ResourceProvider();
        internal ScreenshotsVisualizerDatabase PluginDatabase { get; set; } = ScreenshotsVisualizer.PluginDatabase;

        private List<Screenshot> Screenshots { get; set; } = new List<Screenshot>();
        private Screenshot screenshot { get; set; }
        private int index { get; set; } = 0;


        public SsvSinglePictureView(Screenshot screenshot, List<Screenshot> screenshots = null, Game game = null)
        {
            InitializeComponent();

            this.Screenshots = screenshots;
            if (screenshots != null)
            {
                index = screenshots.FindIndex(x => x == screenshot);
            }

            ButtonNext.Visibility = Visibility.Collapsed;
            ButtonPrev.Visibility = Visibility.Collapsed;
            PART_Copy.Visibility = Visibility.Collapsed;
            PART_Game.Visibility = Visibility.Collapsed;

            SetImage(screenshot);
        }


        private void SetImage(Screenshot screenshot)
        {
            string PictureSource = string.Empty;
            Game game = API.Instance.Database.Games.Get(screenshot.gameId);

            if (File.Exists(screenshot.FileName))
            {
                PictureSource = screenshot.FileName;
                this.screenshot = screenshot;

                if (this.Parent is Window)
                {
                    ((Window)this.Parent).Title = game != null
                        ? resources.GetString("LOCSsv") + " - " + game.Name + " - " + screenshot.FileNameOnly
                        : resources.GetString("LOCSsv") + " - " + screenshot.FileNameOnly;
                }
            }

            this.DataContext = new
            {
                PictureSource,
                IsVideo = screenshot.IsVideo,
                Icon = !game?.Icon.IsNullOrEmpty() ?? false ? PluginDatabase.PlayniteApi.Database.GetFullFilePath(game.Icon) : string.Empty,
                GameName = game?.Name,
                GameId = game?.Id,
                GoToGame = PluginDatabase.GoToGame
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
                    ((Window)this.Parent).WindowState = ((Window)this.Parent).WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
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
                    ((Window)this.Parent).WindowState = ((Window)this.Parent).WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                }
            }
        }


        #region Image navigation
        private void ButtonPrev_Click(object sender, RoutedEventArgs e)
        {
            if (Screenshots != null)
            {
                if (index == 0)
                {
                    index = Screenshots.Count - 1;
                }
                else
                {
                    index--;
                }

                SetImage(Screenshots[index]);
            }
        }

        private void ButtonNext_Click(object sender, RoutedEventArgs e)
        {
            if (Screenshots != null)
            {
                if (index == Screenshots.Count - 1)
                {
                    index = 0;
                }
                else
                {
                    index++;
                }

                SetImage(Screenshots[index]);
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
            Window win = UI.FindParent<Window>((FrameworkElement)sender);
            win.KeyDown += new KeyEventHandler(SsvSinglePictureView_KeyDown);
            win.MouseEnter += PART_Contener_MouseEnter;
            win.MouseLeave += PART_Contener_MouseLeave;
        }


        private void PART_Contener_MouseEnter(object sender, MouseEventArgs e)
        {
            if (Screenshots?.Count > 2)
            {
                ButtonNext.Visibility = Visibility.Visible;
                ButtonPrev.Visibility = Visibility.Visible;
            }

            if (!screenshot?.IsVideo ?? true)
            {
                PART_Copy.Visibility = Visibility.Visible;
                PART_Game.Visibility = Visibility.Visible;
            }
        }

        private void PART_Contener_MouseLeave(object sender, MouseEventArgs e)
        {
            ButtonNext.Visibility = Visibility.Collapsed;
            ButtonPrev.Visibility = Visibility.Collapsed;
            PART_Copy.Visibility = Visibility.Collapsed;
            PART_Game.Visibility = Visibility.Collapsed;
        }


        private void PART_Copy_Click(object sender, RoutedEventArgs e)
        {
            if (!screenshot?.IsVideo ?? true && File.Exists(screenshot.FileName))
            {
                try
                {
                    System.Drawing.Image img = System.Drawing.Image.FromFile(screenshot.FileName);
                    Clipboard.SetDataObject(img);
                }
                catch(Exception ex)
                {
                    Common.LogError(ex, false);
                }
            }
        }
    }
}
