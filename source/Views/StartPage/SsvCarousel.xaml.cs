using CommonPluginsShared;
using CommonPluginsShared.Commands;
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
        private static ILogger Logger => LogManager.GetLogger();
        private static ScreenshotsVisualizerDatabase PluginDatabase => ScreenshotsVisualizer.PluginDatabase;

        private ObservableCollection<Screenshot> Screenshots { get; set; } = new ObservableCollection<Screenshot>();
        private int Index { get; set; } = 0;
        private System.Timers.Timer Timer { get; set; }
        private bool WindowsIsActivated { get; set; } = true;
        private bool IsNext { get; set; } = true;

        private bool _lifecycleHooksAttached;
        private Window _mainWindowHooked;


        public SsvCarousel()
        {
            PluginDatabase.PluginSettings.PropertyChanged += Settings_PropertyChanged;

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
            bool isVideo = screenshot?.IsVideo ?? false;

            if (File.Exists(screenshot?.FileName))
            {
                string pictureSource;
                if (isVideo)
                {
                    pictureSource = screenshot.FileName;
                }
                else
                {
                    pictureSource = PluginDatabase.PluginSettings.ssvCarouselOptions.EnableLowerResolution
                        ? screenshot.ImageThumbnail
                        : screenshot.FileName;
                }

                this.DataContext = new
                {
                    PictureSource = pictureSource,
                    IsVideo = isVideo,
                    AddBorder = true,
                    AddGameName = PluginDatabase.PluginSettings.ssvCarouselOptions.AddGameName,
                    GameName = API.Instance.Database.Games.Get(screenshot.GameId)?.Name,
                    GameId = API.Instance.Database.Games.Get(screenshot.GameId)?.Id,
                    GoToGame = CommandsNavigation.GoToGame
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

        private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (this.IsVisible)
            {
                Update();
            }
        }

        private void Update()
        {
            PART_Contener.Margin = new Thickness(PluginDatabase.PluginSettings.ssvCarouselOptions.Margin);

            _ = Task.Run(() =>
            {
                if (!TryInvokeOnUi(() =>
                {
                    StopAndDisposeTimer();

                    SetImage(null);
                    Screenshots.Clear();
                    Index = 0;
                }))
                {
                    Logger.Debug("[SsvCarousel][update] aborted — no UI dispatcher at start");
                    return;
                }

                Random r = new Random();

                List<GameScreenshots> pluginData = PluginDatabase.GetAllCache().Where(x => x.ScreenshotsCount > 0).ToList();
                pluginData.Sort((z, y) => r.Next(-1, 1));

                if (PluginDatabase.PluginSettings.ssvCarouselOptions.SourcesList?.Where(x => x.IsCheck)?.Count() > 0)
                {
                    pluginData = pluginData.Where(x => PluginDatabase.PluginSettings.ssvCarouselOptions.SourcesList.Where(y => y.IsCheck).Any(y => y.Name.IsEqual(x.Source?.Name))).ToList();
                }

                if (PluginDatabase.PluginSettings.ssvCarouselOptions.OnlyFavorite)
                {
                    pluginData = pluginData.Where(x => x.Favorite).ToList();
                }

                if (PluginDatabase.PluginSettings.ssvCarouselOptions.LimitGame != 0)
                {
                    pluginData = pluginData.Take(PluginDatabase.PluginSettings.ssvCarouselOptions.LimitGame).ToList();
                }

                List<Screenshot> temp = Screenshots.ToList();
                pluginData.Where(x => x.Count > 0 && !x.Hidden).ToList().ForEach(x =>
                {
                    List<Screenshot> data = Serialization.GetClone(x.Items.Where(y => PluginDatabase.PluginSettings.ssvCarouselOptions.WithVideo ? true : !y.IsVideo).ToList());
                    if (PluginDatabase.PluginSettings.ssvCarouselOptions.OnlyMostRecent)
                    {
                        data = data.OrderByDescending(z => z.Modifed).ToList();
                    }
                    else
                    {
                        r = new Random();
                        data.Sort((z, y) => r.Next(-1, 1));
                    }

                    if (PluginDatabase.PluginSettings.ssvCarouselOptions.LimitPerGame != 0)
                    {
                        data = data.Take(PluginDatabase.PluginSettings.ssvCarouselOptions.LimitPerGame).ToList();
                    }

                    data.ForEach(c => c.GameId = x.Id);
                    temp.AddRange(data);
                });

                if (PluginDatabase.PluginSettings.ssvCarouselOptions.EnableAllRandom)
                {
                    r = new Random();
                    temp.Sort((z, y) => r.Next(-1, 1));
                }

                if (!TryInvokeOnUi(() =>
                {
                    Screenshots = temp.Where(x => File.Exists(x.FileName)).ToObservable();
                    if (Screenshots?.Count > 2 && PluginDatabase.PluginSettings.ssvCarouselOptions.EnableAutoChange)
                    {
                        Timer = new System.Timers.Timer(PluginDatabase.PluginSettings.ssvCarouselOptions.Time * 1000);
                        Timer.Elapsed += Timer_Elapsed;
                        Timer.Start();
                    }
                    if (Screenshots != null && Screenshots.Count > 0)
                    {
                        Index = 0;
                        SetImage(Screenshots[Index]);
                    }
                    else
                    {
                        Index = 0;
                    }
                }))
                {
                    Logger.Debug("[SsvCarousel][update] aborted — no UI dispatcher at apply");
                }
            });
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!WindowsIsActivated)
            {
                return;
            }

            if (!TryInvokeOnUi(() =>
            {
                if (!IsLoaded)
                {
                    return;
                }

                ButtonNext_Click(null, null);
            }))
            {
                Logger.Debug("[SsvCarousel][timer] skipped — no UI dispatcher (shutdown)");
            }
        }

        private void PART_Contener_MouseDown(object sender, MouseButtonEventArgs e)
        {
            bool isGood = false;
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
            {
                isGood = true;
            }

            if (!isGood)
            {
                return;
            }

            if (Screenshots == null || Screenshots.Count == 0 || Index < 0 || Index >= Screenshots.Count)
            {
                Logger.Warn(string.Format(
                    "[SsvCarousel][dblclick] ignored — empty or out-of-range index={0} count={1}",
                    Index,
                    Screenshots?.Count ?? 0));
                return;
            }

            Screenshot screenshot = Screenshots[Index];
            if (PluginDatabase.PluginSettings.UseExternalViewer)
            {
                ScreenshotsVisualizerWindows.OpenWithExternalViewer(screenshot.FileName);
            }
            else
            {
                Game game = API.Instance.Database.Games.Get(screenshot.GameId);
                string title = game != null
                    ? ResourceProvider.GetString("LOCSsv") + " - " + game.Name + " - " + screenshot.FileNameOnly
                    : ResourceProvider.GetString("LOCSsv") + " - " + screenshot.FileNameOnly;
                ScreenshotsVisualizerWindows windows = PluginDatabase.PluginWindows as ScreenshotsVisualizerWindows;
                if (windows != null)
                {
                    windows.ShowSinglePictureWindow(screenshot, null, title);
                }
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
                    Timer.Interval = PluginDatabase.PluginSettings.ssvCarouselOptions.Time * 1000;
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
                    Timer.Interval = PluginDatabase.PluginSettings.ssvCarouselOptions.Time * 1000;
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
                if (!TryBeginInvokeOnUi(() =>
                {
                    if (!IsLoaded)
                    {
                        return;
                    }

                    WindowsIsActivated = false;
                    if (PART_Video != null)
                    {
                        PART_Video.LoadedBehavior = MediaState.Pause;
                    }

                    Timer?.Stop();
                }))
                {
                    Logger.Debug("[SsvCarousel][deactivated] skipped — no UI dispatcher (shutdown)");
                }
            });
        }

        private void Application_Activated(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                Thread.Sleep(1000);
                if (!TryBeginInvokeOnUi(() =>
                {
                    if (!IsLoaded)
                    {
                        return;
                    }

                    WindowsIsActivated = true;
                    if (PART_Video != null)
                    {
                        PART_Video.LoadedBehavior = MediaState.Pause;
                    }

                    Timer?.Start();
                }))
                {
                    Logger.Debug("[SsvCarousel][activated] skipped — no UI dispatcher (shutdown)");
                }
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
            AttachLifecycleHooks();

            // Start Page may fire Unloaded then Loaded again; ctor Update can finish while detached.
            if (Screenshots == null || Screenshots.Count == 0)
            {
                Logger.Debug("[SsvCarousel][loaded] empty carousel — refreshing");
                Update();
            }
            else
            {
                SetImage(Screenshots[Index < Screenshots.Count ? Index : 0]);
                if (Screenshots.Count > 2 && PluginDatabase.PluginSettings.ssvCarouselOptions.EnableAutoChange && Timer == null)
                {
                    Timer = new System.Timers.Timer(PluginDatabase.PluginSettings.ssvCarouselOptions.Time * 1000);
                    Timer.Elapsed += Timer_Elapsed;
                    Timer.Start();
                }
                else
                {
                    Timer?.Start();
                }
            }
        }

        private void PART_Contener_Unloaded(object sender, RoutedEventArgs e)
        {
            DetachLifecycleHooks();
            StopAndDisposeTimer();
            Logger.Debug("[SsvCarousel][unloaded] timer stopped and lifecycle hooks detached");
        }

        private void AttachLifecycleHooks()
        {
            if (_lifecycleHooksAttached)
            {
                return;
            }

            Application app = Application.Current;
            if (app == null)
            {
                Logger.Debug("[SsvCarousel][loaded] skipped lifecycle hooks — Application.Current is null");
                return;
            }

            app.Activated += Application_Activated;
            app.Deactivated += Application_Deactivated;

            Window mainWindow = app.MainWindow;
            if (mainWindow != null)
            {
                mainWindow.StateChanged += MainWindow_StateChanged;
                _mainWindowHooked = mainWindow;
            }

            _lifecycleHooksAttached = true;
        }

        private void DetachLifecycleHooks()
        {
            if (!_lifecycleHooksAttached)
            {
                return;
            }

            Application app = Application.Current;
            if (app != null)
            {
                app.Activated -= Application_Activated;
                app.Deactivated -= Application_Deactivated;
            }

            if (_mainWindowHooked != null)
            {
                _mainWindowHooked.StateChanged -= MainWindow_StateChanged;
                _mainWindowHooked = null;
            }

            _lifecycleHooksAttached = false;
        }

        private void StopAndDisposeTimer()
        {
            if (Timer == null)
            {
                return;
            }

            Timer.Elapsed -= Timer_Elapsed;
            Timer.Stop();
            Timer.Dispose();
            Timer = null;
        }

        /// <summary>
        /// Invokes <paramref name="action"/> on the UI dispatcher when the host application is still alive.
        /// </summary>
        /// <param name="action">UI work to run.</param>
        /// <returns><c>true</c> when the action was invoked; otherwise <c>false</c>.</returns>
        private bool TryInvokeOnUi(Action action)
        {
            Dispatcher dispatcher = GetLiveUiDispatcher();
            if (dispatcher == null)
            {
                return false;
            }

            try
            {
                dispatcher.Invoke(action);
                return true;
            }
            catch (TaskCanceledException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        /// <summary>
        /// Queues <paramref name="action"/> on the UI dispatcher when the host application is still alive.
        /// </summary>
        /// <param name="action">UI work to run.</param>
        /// <returns><c>true</c> when the action was queued; otherwise <c>false</c>.</returns>
        private bool TryBeginInvokeOnUi(Action action)
        {
            Dispatcher dispatcher = GetLiveUiDispatcher();
            if (dispatcher == null)
            {
                return false;
            }

            try
            {
                _ = dispatcher.BeginInvoke(DispatcherPriority.Normal, action);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private Dispatcher GetLiveUiDispatcher()
        {
            Application app = Application.Current;
            if (app == null)
            {
                return null;
            }

            Dispatcher dispatcher = app.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
            {
                return null;
            }

            return dispatcher;
        }
    }
}
