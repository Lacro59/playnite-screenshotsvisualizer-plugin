using CommonPluginsShared;
using CommonPluginsShared.Extensions;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ScreenshotsVisualizer.Views.StartPage
{
    /// <summary>
    /// Logique d'interaction pour SsvCarousel .xaml
    /// </summary>
    public partial class SsvCarousel : UserControl
    {
        internal ScreenshotsVisualizerDatabase PluginDatabase => ScreenshotsVisualizer.PluginDatabase;

        private ObservableCollection<Screenshot> Screenshots { get; set; } = new ObservableCollection<Screenshot>();
        private int Index { get; set; } = 0;
        private System.Timers.Timer Timer { get; set; }
        private bool WindowsIsActivated { get; set; } = true;
        private bool IsNext { get; set; } = true;


        public SsvCarousel()
        {
            PluginDatabase.PluginSettings.Settings.PropertyChanged += Settings_PropertyChanged;
            PluginDatabase.PluginSettings.PropertyChanged += SettingsViewModel_PropertyChanged;

            InitializeComponent();

            this.DataContext = new
            {
                GameName = string.Empty
            };

            Update();

            ButtonNext.Visibility = Visibility.Collapsed;
            ButtonPrev.Visibility = Visibility.Collapsed;
        }


        private void SetImage(Screenshot screenshot)
        {
            bool IsVideo = screenshot?.IsVideo ?? false;

            if (File.Exists(screenshot?.FileName))
            {
                string PictureSource;
                if (IsVideo)
                {
                    PictureSource = screenshot.FileName;
                }
                else
                {
                    if (PluginDatabase.PluginSettings.Settings.ssvCarouselOptions.EnableLowerRezolution)
                    {
                        bool tmp = PluginDatabase.PluginSettings.Settings.UsedThumbnails;
                        PluginDatabase.PluginSettings.Settings.UsedThumbnails = true;
                        PictureSource = screenshot.ImageThumbnail;
                        PluginDatabase.PluginSettings.Settings.UsedThumbnails = tmp;
                    }
                    else
                    {
                        PictureSource = screenshot.FileName;
                    }
                }

                this.DataContext = new
                {
                    PictureSource,
                    IsVideo,
                    AddBorder = true,
                    AddGameName = PluginDatabase.PluginSettings.Settings.ssvCarouselOptions.AddGameName,
                    GameName = API.Instance.Database.Games.Get(screenshot.GameId)?.Name,
                    GameId = API.Instance.Database.Games.Get(screenshot.GameId)?.Id,
                    GoToGame = PluginDatabase.GoToGame
                };
            }
            else
            {
                if (IsNext)
                {
                    ButtonNext_Click(null, null);
                }
                else
                {
                    ButtonPrev_Click(null, null);
                }
            }
        }


        private void SettingsViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (this.IsVisible)
            {
                Update();
            }
        }

        private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (this.IsVisible)
            {
                Update();
            }
        }


        private void Update()
        {
            PART_Contener.Margin = new Thickness(PluginDatabase.PluginSettings.Settings.ssvCarouselOptions.Margin);

            _ = Task.Run(() =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (Timer != null)
                    {
                        Timer.Stop();
                        Timer.Dispose();
                        Timer = null;
                    }

                    SetImage(null);
                    Screenshots.Clear();
                });

                Random r = new Random();

                List<KeyValuePair<Guid, GameScreenshots>> PluginData = PluginDatabase.Database.Items.Where(x => x.Value.Count > 0).ToList();
                PluginData.Sort((z, y) => r.Next(-1, 1));

                if (PluginDatabase.PluginSettings.Settings.ssvCarouselOptions.SourcesList?.Where(x => x.IsCheck)?.Count() > 0)
                {
                    PluginData = PluginData.Where(x => PluginDatabase.PluginSettings.Settings.ssvCarouselOptions.SourcesList.Where(y => y.IsCheck).Any(y => y.Name.IsEqual(x.Value.Source?.Name))).ToList();
                }

                if (PluginDatabase.PluginSettings.Settings.ssvCarouselOptions.OnlyFavorite)
                {
                    PluginData = PluginData.Where(x => x.Value.Favorite).ToList();
                }

                if (PluginDatabase.PluginSettings.Settings.ssvCarouselOptions.LimitGame != 0)
                {
                    PluginData = PluginData.Take(PluginDatabase.PluginSettings.Settings.ssvCarouselOptions.LimitGame).ToList();
                }

                List<Screenshot> temp = Screenshots.ToList();
                PluginData.Where(x => x.Value.Count > 0 && !x.Value.Hidden).ForEach(x =>
                {
                    List<Screenshot> data = Serialization.GetClone(x.Value.Items.Where(y => PluginDatabase.PluginSettings.Settings.ssvCarouselOptions.WithVideo ? true : !y.IsVideo).ToList());
                    if (PluginDatabase.PluginSettings.Settings.ssvCarouselOptions.OnlyMostRecent)
                    {
                        data = data.OrderByDescending(z => z.Modifed).ToList();
                    }
                    else
                    {
                        r = new Random();
                        data.Sort((z, y) => r.Next(-1, 1));
                    }

                    if (PluginDatabase.PluginSettings.Settings.ssvCarouselOptions.LimitPerGame != 0)
                    {
                        data = data.Take(PluginDatabase.PluginSettings.Settings.ssvCarouselOptions.LimitPerGame).ToList();
                    }

                    data.ForEach(c => c.GameId = x.Key);
                    temp.AddRange(data);
                });

                if (PluginDatabase.PluginSettings.Settings.ssvCarouselOptions.EnableAllRandom)
                {
                    r = new Random();
                    temp.Sort((z, y) => r.Next(-1, 1));
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Screenshots = temp.Where(x => File.Exists(x.FileName)).ToObservable();
                    if (Screenshots?.Count > 2 && PluginDatabase.PluginSettings.Settings.ssvCarouselOptions.EnableAutoChange)
                    {
                        Timer = new System.Timers.Timer(PluginDatabase.PluginSettings.Settings.ssvCarouselOptions.Time * 1000);
                        Timer.Start();
                        Timer.Elapsed += Timer_Elapsed;
                    }
                    if (Screenshots?.Count > 1)
                    {
                        Index = 0;
                        SetImage(Screenshots[Index]);
                    }
                });
            });
        }


        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!WindowsIsActivated)
            {
                return;
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                ButtonNext_Click(null, null);
            });
        }


        private void PART_Contener_MouseDown(object sender, MouseButtonEventArgs e)
        {
            bool IsGood = false;
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
            {
                IsGood = true;
            }

            if (IsGood)
            {
                WindowOptions windowOptions = new WindowOptions
                {
                    ShowMinimizeButton = false,
                    ShowMaximizeButton = true,
                    ShowCloseButton = true,
                    CanBeResizable = true,
                    Height = 720,
                    Width = 1280
                };

                Game game = API.Instance.Database.Games.Get(Screenshots[Index].GameId);
                string Title = game != null
                    ? ResourceProvider.GetString("LOCSsv") + " - " + game.Name + " - " + Screenshots[Index].FileNameOnly
                    : ResourceProvider.GetString("LOCSsv") + " - " + Screenshots[Index].FileNameOnly;

                SsvSinglePictureView ViewExtension = new SsvSinglePictureView(Screenshots[Index], null);
                Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(Title, ViewExtension, windowOptions);
                _ = windowExtension.ShowDialog();
            }
        }


        #region Image navigation
        private void ButtonPrev_Click(object sender, RoutedEventArgs e)
        {
            IsNext = false;
            if (Screenshots?.Count > 1)
            {
                if (Index == 0)
                {
                    Index = Screenshots.Count - 1;
                }
                else
                {
                    Index--;
                }

                if (Timer != null)
                {
                    Timer.Stop();
                    Timer.Interval = PluginDatabase.PluginSettings.Settings.ssvCarouselOptions.Time * 1000;
                    Timer.Start();
                }

                SetImage(Screenshots[Index]);
            }
        }

        private void ButtonNext_Click(object sender, RoutedEventArgs e)
        {
            IsNext = true;
            if (Screenshots?.Count > 1)
            {
                if (Index == Screenshots.Count - 1)
                {
                    Index = 0;
                }
                else
                {
                    Index++;
                }

                if (Timer != null)
                {
                    Timer.Stop();
                    Timer.Interval = PluginDatabase.PluginSettings.Settings.ssvCarouselOptions.Time * 1000;
                    Timer.Start();
                }

                SetImage(Screenshots[Index]);
            }
        }
        #endregion


        private void PART_Contener_MouseEnter(object sender, MouseEventArgs e)
        {
            if (Screenshots?.Count > 2)
            {
                ButtonNext.Visibility = Visibility.Visible;
                ButtonPrev.Visibility = Visibility.Visible;
            }
        }

        private void PART_Contener_MouseLeave(object sender, MouseEventArgs e)
        {
            ButtonNext.Visibility = Visibility.Collapsed;
            ButtonPrev.Visibility = Visibility.Collapsed;
        }


        private void PART_Contener_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (((FrameworkElement)sender).IsVisible)
            {
                Timer?.Start();
            }
            else
            {
                Timer?.Stop();
            }
        }


        #region Activate/Deactivated carousel
        private void Application_Deactivated(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                Thread.Sleep(1000);
                Application.Current.Dispatcher?.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    WindowsIsActivated = false;
                    PART_Video.LoadedBehavior = MediaState.Pause;

                    Timer?.Stop();
                }));
            });
        }

        private void Application_Activated(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                Thread.Sleep(1000);
                Application.Current.Dispatcher?.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    WindowsIsActivated = true;
                    PART_Video.LoadedBehavior = MediaState.Pause;

                    Timer?.Start();
                }));
            });
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            switch (((Window)sender).WindowState)
            {
                case WindowState.Normal:
                case WindowState.Maximized:
                    Application_Activated(sender, e);
                    break;

                case WindowState.Minimized:
                    Application_Deactivated(sender, e);
                    break;

                default:
                    break;
            }
        }
        #endregion

        private void PART_Contener_Loaded(object sender, RoutedEventArgs e)
        {
            // Activate/Deactivated animation
            Application.Current.Activated += Application_Activated;
            Application.Current.Deactivated += Application_Deactivated;
            Application.Current.MainWindow.StateChanged += MainWindow_StateChanged;
        }
    }
}
