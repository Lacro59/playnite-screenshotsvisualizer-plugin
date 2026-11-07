using CommonPluginsShared;
using CommonPluginsShared.Commands;
using CommonPluginsShared.UI;
using CommonPluginsShared.Utilities;
using Playnite.SDK;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ScreenshotsVisualizer.Views
{
    /// <summary>
    /// Logique d'interaction pour SsvScreenshotsManager.xaml
    /// </summary>
    public partial class SsvScreenshotsManager : UserControl
    {
        private ScreenshotsVisualizerDatabase PluginDatabase => ScreenshotsVisualizer.PluginDatabase;

        private bool _suppressListToolbarEvents;
        private readonly List<LveGame> _allLveGames = new List<LveGame>();
        private string _gameNameFilter = string.Empty;

        public SsvScreenshotsManager()
        {
            InitializeComponent();
            DataContext = new SsvScreenshotsManagerData();

            InitializeListToolbar();
            SetData();

            PluginDatabase.DatabaseItemUpdated += Database_ItemUpdated;

            PART_ListScreenshots.AddHandler(UIElement.MouseDownEvent, new MouseButtonEventHandler(PluginDatabase.ListBoxItem_MouseLeftButtonDownClick), true);
            PART_Copy.Visibility = Visibility.Collapsed;
        }

        private const string SortCaretAscending = "\uea6a";
        private const string SortCaretDescending = "\uea67";

        private void InitializeListToolbar()
        {
            _suppressListToolbarEvents = true;
            try
            {
                PART_CbSortBy.ItemsSource = SsvScreenshotListOrder.CreateSortByChoices();
                PART_CbMediaFilter.ItemsSource = SsvScreenshotListOrder.CreateMediaFilterChoices();

                PART_CbSortBy.SelectedValue = PluginDatabase.PluginSettings.ScreenshotsListSortBy;
                PART_CbMediaFilter.SelectedValue = PluginDatabase.PluginSettings.ScreenshotsListMediaFilter;
                UpdateSortDirectionButton();
            }
            finally
            {
                _suppressListToolbarEvents = false;
            }
        }

        private void PART_ListToolbar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressListToolbarEvents)
            {
                return;
            }

            PersistListToolbarSettings();
            RefreshListAfterToolbarChange();
        }

        private void PART_BtSortDirection_Click(object sender, RoutedEventArgs e)
        {
            if (_suppressListToolbarEvents)
            {
                return;
            }

            SsvScreenshotSortDirection current = PluginDatabase.PluginSettings.ScreenshotsListSortDirection;
            PluginDatabase.PluginSettings.ScreenshotsListSortDirection =
                current == SsvScreenshotSortDirection.Ascending
                    ? SsvScreenshotSortDirection.Descending
                    : SsvScreenshotSortDirection.Ascending;

            UpdateSortDirectionButton();
            PersistListToolbarSettings();
            RefreshListAfterToolbarChange();
        }

        private void RefreshListAfterToolbarChange()
        {
            LveGame selectedGame = PART_LveGames.SelectedItem as LveGame;
            if (selectedGame != null)
            {
                RefreshScreenshotsForGame(selectedGame.Id);
            }
        }

        private void UpdateSortDirectionButton()
        {
            bool descending = PluginDatabase.PluginSettings.ScreenshotsListSortDirection
                == SsvScreenshotSortDirection.Descending;
            PART_TbSortDirectionCaret.Text = descending ? SortCaretDescending : SortCaretAscending;
            PART_BtSortDirection.ToolTip = ResourceProvider.GetString(
                descending ? "LOCSsvSortDescending" : "LOCSsvSortAscending");
        }

        private void PersistListToolbarSettings()
        {
            if (PART_CbSortBy.SelectedValue is SsvScreenshotSortBy sortBy)
            {
                PluginDatabase.PluginSettings.ScreenshotsListSortBy = sortBy;
            }

            if (PART_CbMediaFilter.SelectedValue is SsvScreenshotMediaFilter mediaFilter)
            {
                PluginDatabase.PluginSettings.ScreenshotsListMediaFilter = mediaFilter;
            }

            try
            {
                PluginDatabase.PersistSettingsAction?.Invoke();
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }

            Common.LogDebug(string.Format(
                "[SsvManager][toolbar] sortBy={0} direction={1} filter={2}",
                PluginDatabase.PluginSettings.ScreenshotsListSortBy,
                PluginDatabase.PluginSettings.ScreenshotsListSortDirection,
                PluginDatabase.PluginSettings.ScreenshotsListMediaFilter));
        }

        private void Database_ItemUpdated(object sender, ItemUpdatedEventArgs<GameScreenshots> e)
        {
            _ = Application.Current.Dispatcher?.BeginInvoke(DispatcherPriority.Loaded, new ThreadStart(delegate
            {
                try
                {
                    Guid? selectedGameId = (PART_LveGames.SelectedItem as LveGame)?.Id;
                    SetData(selectedGameId);
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, true);
                }
            }));
        }

        private void SetData(Guid? selectedGameId = null)
        {
            PART_DataLoad.Visibility = Visibility.Visible;
            PART_Data.Visibility = Visibility.Hidden;

            _ = Task.Run(() =>
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                try
                {
                    ObservableCollection<LveGame> LveGames = PluginDatabase.GetAllCache().Where(x => x.HasData)
                                                                    .Select(x =>
                                                                    {
                                                                        SsvScreenshotListStats gameStats = SsvScreenshotListOrder.ComputeStats(x.Items);
                                                                        return new LveGame
                                                                        {
                                                                            Id = x.Id,
                                                                            Icon = API.Instance.Database.GetFullFilePath(x.Icon),
                                                                            Name = x.Name,
                                                                            LastActivity = x.LastActivity,
                                                                            SourceName = PlayniteTools.GetSourceName(x.Id),

                                                                            LastSsv = x.Items.Select(y => y.Modifed).Max(),
                                                                            ImageCount = gameStats.ImageCount,
                                                                            VideoCount = gameStats.VideoCount,
                                                                            ImageTotalSize = gameStats.ImageTotalSize,
                                                                            VideoTotalSize = gameStats.VideoTotalSize,
                                                                            Total = gameStats.FileCount,
                                                                            TotalSizeBytes = gameStats.FileTotalSize,
                                                                            TotalSizeString = UtilityTools.SizeSuffix(gameStats.FileTotalSize)
                                                                        };
                                                                    }).ToObservable();
                    stopwatch.Stop();
                    Common.LogDebug(string.Format(
                        "[SsvManager][load] games={0} ms={1}",
                        LveGames.Count,
                        stopwatch.ElapsedMilliseconds));
                    return LveGames;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    Common.LogDebug(string.Format(
                        "[SsvManager][load] games=0 ms={0} failed=true",
                        stopwatch.ElapsedMilliseconds));
                    Common.LogError(ex, false, true, PluginDatabase.PluginName);
                    return new ObservableCollection<LveGame>();
                }
            }).ContinueWith(antecedent =>
            {
                _ = Application.Current.Dispatcher?.BeginInvoke(DispatcherPriority.Loaded, new ThreadStart(delegate
                {
                    ObservableCollection<LveGame> games = antecedent.Result ?? new ObservableCollection<LveGame>();
                    _allLveGames.Clear();
                    foreach (LveGame game in games)
                    {
                        if (game != null)
                        {
                            _allLveGames.Add(game);
                        }
                    }

                    PublishLibraryInfosFromGames(_allLveGames);
                    ApplyGameNameFilter(selectedGameId);

                    PART_DataLoad.Visibility = Visibility.Hidden;
                    PART_Data.Visibility = Visibility.Visible;
                }));
            });
        }

        private void PART_GameNameFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = PART_GameNameFilter?.Text ?? string.Empty;
            if (string.Equals(_gameNameFilter, searchText, StringComparison.Ordinal))
            {
                return;
            }

            _gameNameFilter = searchText;
            Guid? selectedGameId = (PART_LveGames.SelectedItem as LveGame)?.Id;
            ApplyGameNameFilter(selectedGameId);
        }

        private void ApplyGameNameFilter(Guid? preferredSelectedId)
        {
            string query = (_gameNameFilter ?? string.Empty).Trim();
            List<LveGame> filtered = new List<LveGame>();
            if (string.IsNullOrEmpty(query))
            {
                filtered.AddRange(_allLveGames);
            }
            else
            {
                foreach (LveGame game in _allLveGames)
                {
                    if (game?.Name != null
                        && game.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        filtered.Add(game);
                    }
                }
            }

            ObservableCollection<LveGame> visible = filtered.ToObservable();
            ((SsvScreenshotsManagerData)DataContext).LveGames = visible;
            PART_LveGames.Sorting();

            LveGame match = null;
            if (preferredSelectedId.HasValue)
            {
                match = visible.FirstOrDefault(x => x.Id == preferredSelectedId.Value);
            }

            bool selectionKept = match != null;
            if (selectionKept)
            {
                PART_LveGames.SelectedItem = match;
            }
            else
            {
                PART_LveGames.SelectedItem = null;
                ((SsvScreenshotsManagerData)DataContext).Screenshots = new ObservableCollection<Screenshot>();
                ClearScreenshotPreview();
            }

            Common.LogDebug(string.Format(
                "[SsvManager][game-filter] queryLen={0} visible={1} total={2} selectionKept={3}",
                query.Length,
                visible.Count,
                _allLveGames.Count,
                selectionKept));
        }


        private void PublishLibraryInfosFromGames(IEnumerable<LveGame> games)
        {
            SsvScreenshotListStats libraryStats = new SsvScreenshotListStats();
            if (games != null)
            {
                foreach (LveGame game in games)
                {
                    if (game == null)
                    {
                        continue;
                    }

                    libraryStats.ImageCount += game.ImageCount;
                    libraryStats.VideoCount += game.VideoCount;
                    libraryStats.ImageTotalSize += game.ImageTotalSize;
                    libraryStats.VideoTotalSize += game.VideoTotalSize;
                }
            }

            SsvScreenshotsManagerData dataContext = (SsvScreenshotsManagerData)DataContext;
            dataContext.InfosScreenshotsCountText = FormatScreenshotsCount(libraryStats.ImageCount);
            dataContext.InfosScreenshotsSizeText = UtilityTools.SizeSuffix(libraryStats.ImageTotalSize);
            dataContext.InfosVideosCountText = FormatVideosCount(libraryStats.VideoCount);
            dataContext.InfosVideosSizeText = UtilityTools.SizeSuffix(libraryStats.VideoTotalSize);
            dataContext.InfosFilesCountText = libraryStats.FileCount.ToString();
            dataContext.InfosFilesSizeText = UtilityTools.SizeSuffix(libraryStats.FileTotalSize);

            Common.LogDebug(string.Format(
                "[SsvManager][library] files={0} images={1} videos={2}",
                libraryStats.FileCount,
                libraryStats.ImageCount,
                libraryStats.VideoCount));
        }

        private static string FormatScreenshotsCount(int count)
        {
            return count > 1
                ? string.Format(ResourceProvider.GetString("LOCSsvScreenshots"), count)
                : string.Format(ResourceProvider.GetString("LOCSsvScreenshot"), count);
        }

        private static string FormatVideosCount(int count)
        {
            return count > 1
                ? string.Format(ResourceProvider.GetString("LOCSsvVideos"), count)
                : string.Format(ResourceProvider.GetString("LOCSsvVideo"), count);
        }


        private void PART_LveGames_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender == null)
            {
                return;
            }


            ListBox listBox = (ListBox)sender;
            LveGame lveGame = (LveGame)listBox.SelectedItem;

            if (lveGame == null)
            {
                return;
            }

            RefreshScreenshotsForGame(lveGame.Id);
        }

        private void RefreshScreenshotsForGame(Guid gameId)
        {
            SsvScreenshotSortBy sortBy = PluginDatabase.PluginSettings.ScreenshotsListSortBy;
            SsvScreenshotSortDirection sortDirection = PluginDatabase.PluginSettings.ScreenshotsListSortDirection;
            SsvScreenshotMediaFilter mediaFilter = PluginDatabase.PluginSettings.ScreenshotsListMediaFilter;

            _ = Task.Run(() =>
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                GameScreenshots gameScreenshots = PluginDatabase.Get(gameId, true);
                int skippedMissing;
                List<Screenshot> screenshots = SsvScreenshotListOrder.Apply(
                    gameScreenshots?.Items,
                    mediaFilter,
                    sortBy,
                    sortDirection,
                    out skippedMissing);
                SsvScreenshotListStats stats = SsvScreenshotListOrder.ComputeStats(screenshots);

                stopwatch.Stop();
                Common.LogDebug(string.Format(
                    "[SsvManager][refresh] files={0} images={1} videos={2} skippedMissing={3} sortBy={4} direction={5} filter={6} ms={7}",
                    screenshots.Count,
                    stats.ImageCount,
                    stats.VideoCount,
                    skippedMissing,
                    sortBy,
                    sortDirection,
                    mediaFilter,
                    stopwatch.ElapsedMilliseconds));

                _ = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new ThreadStart(delegate
                {
                    ((SsvScreenshotsManagerData)DataContext).Screenshots = screenshots.ToObservable();
                }));

                return true;
            });
        }

        private void ClearScreenshotPreview()
        {
            SsvScreenshotsManagerData dataContext = (SsvScreenshotsManagerData)DataContext;
            dataContext.FileNameImage = string.Empty;
            dataContext.FileNameVideo = string.Empty;
            dataContext.FilePath = string.Empty;
            Common.LogDebug("[SsvManager][preview] cleared");
        }

        private void PART_ListScreenshots_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender == null)
            {
                return;
            }

            ListBox listbox = sender as ListBox;
            if (listbox.SelectedItem is null)
            {
                ((SsvScreenshotsManagerData)DataContext).FileNameImage = string.Empty;
                ((SsvScreenshotsManagerData)DataContext).FileNameVideo = string.Empty;
                ((SsvScreenshotsManagerData)DataContext).FilePath = string.Empty;
                Common.LogDebug("[SsvManager][preview] cleared");
                return;
            }

            Screenshot item = listbox.SelectedItem as Screenshot;
            if (item == null)
            {
                return;
            }

            bool exists = File.Exists(item.FileName);
            Common.LogDebug(string.Format(
                "[SsvManager][preview] isVideo={0} exists={1} file={2}",
                item.IsVideo,
                exists,
                item.FileNameOnly));

            if (item.IsVideo)
            {
                ((SsvScreenshotsManagerData)DataContext).FileNameImage = string.Empty;
                ((SsvScreenshotsManagerData)DataContext).FileNameVideo = item.FileName;
            }
            else
            {
                ((SsvScreenshotsManagerData)DataContext).FileNameImage = item.FileName;
                ((SsvScreenshotsManagerData)DataContext).FileNameVideo = string.Empty;
            }
            ((SsvScreenshotsManagerData)DataContext).FilePath = item.FileName;
        }


        private void PART_BtDelete_Click(object sender, RoutedEventArgs e)
        {
            ListBoxItem item = UIHelper.FindParent<ListBoxItem>((Button)sender);
            Screenshot screenshot = item?.DataContext as Screenshot;
            if (screenshot == null)
            {
                return;
            }

            LveGame selectedGame = PART_LveGames.SelectedItem as LveGame;
            if (selectedGame == null)
            {
                return;
            }

            Guid gameId = selectedGame.Id;

            MessageBoxResult RessultDialog = API.Instance.Dialogs.ShowMessage(
                string.Format(ResourceProvider.GetString("LOCSsvDeleteConfirm"), screenshot.FileNameOnly),
                PluginDatabase.PluginName,
                MessageBoxButton.YesNo
            );

            if (RessultDialog == MessageBoxResult.Yes)
            {
                try
                {
                    ((SsvScreenshotsManagerData)DataContext).Screenshots = new ObservableCollection<Screenshot>();
                    ClearScreenshotPreview();

                    SsvScreenshotDeleteResult result = PluginDatabase.TryDeleteScreenshot(gameId, screenshot);
                    if (result == SsvScreenshotDeleteResult.Success
                        || result == SsvScreenshotDeleteResult.SkippedMissingPhysicalFile)
                    {
                        RefreshScreenshotsForGame(gameId);
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, PluginDatabase.PluginName);
                }
            }
        }


        private void PART_Copy_Click(object sender, RoutedEventArgs e)
        {
            Screenshot screenshot = PART_ListScreenshots.SelectedItem as Screenshot;
            if (screenshot != null && !screenshot.IsVideo && File.Exists(screenshot.FileName))
            {
                SsvClipboardHelper.TryCopyImageFile(screenshot.FileName);
            }
        }

        private void Grid_MouseEnter(object sender, MouseEventArgs e)
        {
            Screenshot screenshot = (Screenshot)PART_ListScreenshots.SelectedItem;
            if (screenshot != null && !screenshot.IsVideo && File.Exists(screenshot.FileName))
            {
                PART_Copy.Visibility = Visibility.Visible;
            }
        }

        private void Grid_MouseLeave(object sender, MouseEventArgs e)
        {
            PART_Copy.Visibility = Visibility.Collapsed;
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string filePath = Path.GetDirectoryName(((SsvScreenshotsManagerData)DataContext).FilePath);
            if (Directory.Exists(filePath))
            {
                _ = Process.Start(filePath);
            }
        }
    }


    public class SsvScreenshotsManagerData : ObservableObject
    {
        private ObservableCollection<LveGame> _LveGames = new ObservableCollection<LveGame>();
        public ObservableCollection<LveGame> LveGames { get => _LveGames; set => SetValue(ref _LveGames, value); }

        private ObservableCollection<Screenshot> _Screenshots = new ObservableCollection<Screenshot>();
        public ObservableCollection<Screenshot> Screenshots { get => _Screenshots; set => SetValue(ref _Screenshots, value); }

        private string _FileNameImage = string.Empty;
        public string FileNameImage { get => _FileNameImage; set => SetValue(ref _FileNameImage, value); }

        private string _FileNameVideo = string.Empty;
        public string FileNameVideo { get => _FileNameVideo; set => SetValue(ref _FileNameVideo, value); }

        private string _FilePath = string.Empty;
        public string FilePath { get => _FilePath; set => SetValue(ref _FilePath, value); }

        private string _infosScreenshotsCountText = string.Empty;
        public string InfosScreenshotsCountText { get => _infosScreenshotsCountText; set => SetValue(ref _infosScreenshotsCountText, value); }

        private string _infosScreenshotsSizeText = string.Empty;
        public string InfosScreenshotsSizeText { get => _infosScreenshotsSizeText; set => SetValue(ref _infosScreenshotsSizeText, value); }

        private string _infosVideosCountText = string.Empty;
        public string InfosVideosCountText { get => _infosVideosCountText; set => SetValue(ref _infosVideosCountText, value); }

        private string _infosVideosSizeText = string.Empty;
        public string InfosVideosSizeText { get => _infosVideosSizeText; set => SetValue(ref _infosVideosSizeText, value); }

        private string _infosFilesCountText = "0";
        public string InfosFilesCountText { get => _infosFilesCountText; set => SetValue(ref _infosFilesCountText, value); }

        private string _infosFilesSizeText = string.Empty;
        public string InfosFilesSizeText { get => _infosFilesSizeText; set => SetValue(ref _infosFilesSizeText, value); }
    }


    public class LveGame
    {
        private ScreenshotsVisualizerDatabase PluginDatabase => ScreenshotsVisualizer.PluginDatabase;

        public Guid Id { get; set; }
        public string Icon { get; set; }
        public string Name { get; set; }
        public DateTime? LastActivity { get; set; }
        public string SourceName { get; set; }
        public string SourceIcon => TransformIcon.Get(SourceName);

        public DateTime LastSsv { get; set; }
        public int ImageCount { get; set; }
        public int VideoCount { get; set; }
        public long ImageTotalSize { get; set; }
        public long VideoTotalSize { get; set; }
        public int Total { get; set; }
        public long TotalSizeBytes { get; set; }
        public string TotalSizeString { get; set; }

        public RelayCommand<Guid> GoToGame => CommandsNavigation.GoToGame;
        public bool GameExist => API.Instance.Database.Games.Get(Id) != null;
    }
}
