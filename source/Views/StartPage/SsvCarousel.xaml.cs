using CommonPluginsShared;
using CommonPluginsShared.Commands;
using CommonPluginsShared.Extensions;
using Playnite.SDK;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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
        private int _updateSequence;

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
            if (screenshot == null)
            {
                Common.LogDebug("[SsvCarousel][preview] cleared");
                return;
            }

            bool isVideo = screenshot.IsVideo;
            bool exists = File.Exists(screenshot.FileName);
            Common.LogDebug(string.Format(
                "[SsvCarousel][preview] isVideo={0} exists={1} file={2}",
                isVideo,
                exists,
                screenshot.FileNameOnly));

            if (exists)
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
            int sequence = Interlocked.Increment(ref _updateSequence);

            _ = Task.Run(() =>
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                if (!TryInvokeOnUi(() =>
                {
                    StopAndDisposeTimer();

                    SetImage(null);
                    Screenshots.Clear();
                    Index = 0;
                }))
                {
                    stopwatch.Stop();
                    Common.LogDebug(string.Format(
                        "[SsvCarousel][update] aborted=start screenshots=0 ms={0}",
                        stopwatch.ElapsedMilliseconds));
                    return;
                }

                if (sequence != Volatile.Read(ref _updateSequence))
                {
                    return;
                }

                var carouselOptions = PluginDatabase.PluginSettings.ssvCarouselOptions;
                bool withVideo = carouselOptions.WithVideo;
                bool onlyMostRecent = carouselOptions.OnlyMostRecent;
                bool enableAllRandom = carouselOptions.EnableAllRandom;
                int limitGame = carouselOptions.LimitGame;
                int limitPerGame = carouselOptions.LimitPerGame;

                List<string> checkedSourceNames = carouselOptions.SourcesList?
                    .Where(x => x.IsCheck)
                    .Select(x => x.Name)
                    .ToList() ?? new List<string>();
                bool filterBySource = checkedSourceNames.Count > 0;

                // Use Items.Count / HasData — not ScreenshotsCount (that property File.Exists every file).
                List<GameScreenshots> pluginData = PluginDatabase.GetAllCache()
                    .Where(x => x.HasData && !x.Hidden)
                    .ToList();

                if (filterBySource)
                {
                    pluginData = pluginData
                        .Where(x => checkedSourceNames.Any(name => name.IsEqual(x.Source?.Name)))
                        .ToList();
                }

                if (carouselOptions.OnlyFavorite)
                {
                    pluginData = pluginData.Where(x => x.Favorite).ToList();
                }

                ShuffleInPlace(pluginData);

                if (limitGame != 0 && pluginData.Count > limitGame)
                {
                    pluginData = pluginData.Take(limitGame).ToList();
                }

                List<Screenshot> temp = new List<Screenshot>();
                foreach (GameScreenshots gameScreenshots in pluginData)
                {
                    if (gameScreenshots.Items == null || gameScreenshots.Items.Count == 0)
                    {
                        continue;
                    }

                    List<Screenshot> data = gameScreenshots.Items
                        .Where(y => y != null && (withVideo || !y.IsVideo))
                        .ToList();

                    if (onlyMostRecent)
                    {
                        data = data.OrderByDescending(z => z.Modifed).ToList();
                    }
                    else
                    {
                        ShuffleInPlace(data);
                    }

                    if (limitPerGame != 0 && data.Count > limitPerGame)
                    {
                        data = data.Take(limitPerGame).ToList();
                    }

                    foreach (Screenshot screenshot in data)
                    {
                        screenshot.GameId = gameScreenshots.Id;
                    }

                    temp.AddRange(data);
                }

                if (enableAllRandom)
                {
                    ShuffleInPlace(temp);
                }

                if (sequence != Volatile.Read(ref _updateSequence))
                {
                    return;
                }

                List<Screenshot> existingScreenshots = SsvScreenshotListOrder.FilterExistingFiles(temp);
                if (!TryInvokeOnUi(() =>
                {
                    if (sequence != Volatile.Read(ref _updateSequence))
                    {
                        return;
                    }

                    Screenshots = existingScreenshots.ToObservable();
                    if (Screenshots?.Count > 2 && carouselOptions.EnableAutoChange)
                    {
                        Timer = new System.Timers.Timer(carouselOptions.Time * 1000);
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
                    stopwatch.Stop();
                    Common.LogDebug(string.Format(
                        "[SsvCarousel][update] aborted=apply screenshots={0} ms={1}",
                        existingScreenshots.Count,
                        stopwatch.ElapsedMilliseconds));
                    return;
                }

                stopwatch.Stop();
                Common.LogDebug(string.Format(
                    "[SsvCarousel][update] screenshots={0} ms={1}",
                    existingScreenshots.Count,
                    stopwatch.ElapsedMilliseconds));
            });
        }

        /// <summary>
        /// Fisher–Yates shuffle (unbiased), replacing biased Sort with Random.Next(-1, 1).
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="list">List to shuffle in place.</param>
        private static void ShuffleInPlace<T>(IList<T> list)
        {
            if (list == null || list.Count < 2)
            {
                return;
            }

            Random random = new Random();
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                T swap = list[i];
                list[i] = list[j];
                list[j] = swap;
            }
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
                Common.LogDebug("[SsvCarousel][timer] skipped — no UI dispatcher (shutdown)");
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
                    Common.LogDebug("[SsvCarousel][deactivated] skipped — no UI dispatcher (shutdown)");
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
                    Common.LogDebug("[SsvCarousel][activated] skipped — no UI dispatcher (shutdown)");
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
                Common.LogDebug("[SsvCarousel][loaded] empty carousel — refreshing");
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
            Common.LogDebug("[SsvCarousel][unloaded] timer stopped and lifecycle hooks detached");
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
                Common.LogDebug("[SsvCarousel][loaded] skipped lifecycle hooks — Application.Current is null");
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
