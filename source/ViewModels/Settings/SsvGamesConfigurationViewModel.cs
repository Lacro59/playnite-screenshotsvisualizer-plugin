using CommonPluginsShared;
using Playnite.SDK;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using StorePlayniteTools = CommonPluginsStores.PlayniteTools;

namespace ScreenshotsVisualizer.ViewModels.Settings
{
    /// <summary>
    /// View model for the games and screenshot folders configuration section in plugin settings.
    /// </summary>
    public class SsvGamesConfigurationViewModel : ObservableObject
    {
        private static readonly ILogger Logger = LogManager.GetLogger();

        private readonly string _pluginName;
        private readonly object _availableGamesLoadLock = new object();
        private string _searchText = string.Empty;
        private AvailableGameItem _selectedAvailableGame;
        private bool _availableGamesLoaded;
        private int _configuredGamesBulkUpdateDepth;
        private List<ConfiguredGameItem> _cancelSnapshot;

        /// <summary>
        /// Raised once after a bulk update of <see cref="ConfiguredGames"/> completes.
        /// </summary>
        public event EventHandler ConfiguredGamesBulkUpdateCompleted;

        /// <summary>
        /// Gets whether <see cref="ConfiguredGames"/> is being updated in bulk (suppress per-item UI rebuilds).
        /// </summary>
        public bool IsConfiguredGamesBulkUpdate => _configuredGamesBulkUpdateDepth > 0;

        /// <summary>
        /// Initializes a new games configuration view model.
        /// </summary>
        /// <param name="pluginName">Plugin name used for progress dialogs and logging.</param>
        public SsvGamesConfigurationViewModel(string pluginName)
        {
            _pluginName = pluginName;
            AvailableGames = new ObservableCollection<AvailableGameItem>();
            ConfiguredGames = new ObservableCollection<ConfiguredGameItem>();

            AvailableGamesView = CollectionViewSource.GetDefaultView(AvailableGames);
            AvailableGamesView.Filter = FilterGameItem;

            ConfiguredGamesView = CollectionViewSource.GetDefaultView(ConfiguredGames);
            ConfiguredGamesView.Filter = FilterGameItem;

            ConfiguredGames.CollectionChanged += (s, e) => NotifyConfiguredGamesStateChanged();

            ConvertPathsRelativeCommand = new RelayCommand(ConvertAllPathsToRelative);
            ConvertPathsAbsoluteCommand = new RelayCommand(ConvertAllPathsToAbsolute);
            AddSelectedGameCommand = new RelayCommand(AddSelectedGame);
            RemoveConfiguredGameCommand = new RelayCommand<object>(RemoveConfiguredGameFromCommand);
        }

        /// <summary>
        /// Gets games that are not yet configured for screenshots.
        /// </summary>
        public ObservableCollection<AvailableGameItem> AvailableGames { get; }

        /// <summary>
        /// Gets games with at least one screenshot folder configuration.
        /// </summary>
        public ObservableCollection<ConfiguredGameItem> ConfiguredGames { get; }

        /// <summary>
        /// Gets a filtered view of <see cref="AvailableGames"/>.
        /// </summary>
        public ICollectionView AvailableGamesView { get; }

        /// <summary>
        /// Gets a filtered view of <see cref="ConfiguredGames"/>.
        /// </summary>
        public ICollectionView ConfiguredGamesView { get; }

        /// <summary>
        /// Gets or sets the search text applied to both game lists.
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                SetValue(ref _searchText, value);
                RefreshFilters();
            }
        }

        /// <summary>
        /// Gets or sets the selected game in the available games list.
        /// </summary>
        public AvailableGameItem SelectedAvailableGame
        {
            get => _selectedAvailableGame;
            set
            {
                if (_selectedAvailableGame == value)
                {
                    return;
                }

                _selectedAvailableGame = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedAvailableGame));
            }
        }

        /// <summary>
        /// Gets the number of configured games.
        /// </summary>
        public int ConfiguredGamesCount => ConfiguredGames.Count;

        /// <summary>
        /// Gets whether at least one game is configured.
        /// </summary>
        public bool HasConfiguredGames => ConfiguredGames.Count > 0;

        /// <summary>
        /// Gets whether no game is configured yet.
        /// </summary>
        public bool HasNoConfiguredGames => ConfiguredGames.Count == 0;

        /// <summary>
        /// Gets the localized section header for configured games including the count.
        /// </summary>
        public string ConfiguredGamesSectionHeader =>
            string.Format("{0} ({1})", ResourceProvider.GetString("LOCSsvConfigSectionConfiguredGames"), ConfiguredGamesCount);

        /// <summary>
        /// Gets the command that converts all folder paths to relative paths.
        /// </summary>
        public RelayCommand ConvertPathsRelativeCommand { get; }

        /// <summary>
        /// Gets the command that converts all folder paths to absolute paths.
        /// </summary>
        public RelayCommand ConvertPathsAbsoluteCommand { get; }

        /// <summary>
        /// Gets the command that adds the selected available game.
        /// </summary>
        public RelayCommand AddSelectedGameCommand { get; }

        /// <summary>
        /// Gets the command that removes a configured game.
        /// </summary>
        public RelayCommand<object> RemoveConfiguredGameCommand { get; }

        /// <summary>
        /// Gets whether an available game is selected for manual add.
        /// </summary>
        public bool HasSelectedAvailableGame => SelectedAvailableGame != null;

        /// <summary>
        /// Clears the search text and refreshes both list filters.
        /// </summary>
        public void ClearSearch()
        {
            SearchText = string.Empty;
        }

        /// <summary>
        /// Reloads configured games from persisted settings. Unconfigured games are loaded on demand.
        /// </summary>
        /// <param name="gameSettings">Persisted per-game settings.</param>
        public void ReloadFrom(IEnumerable<GameSettings> gameSettings)
        {
            LoadConfiguredGames(gameSettings);
        }

        /// <summary>
        /// Captures a deep snapshot of configured games for fast <see cref="RestoreCancelSnapshot"/>.
        /// </summary>
        public void CaptureCancelSnapshot()
        {
            _cancelSnapshot = ConfiguredGames.Select(CloneConfiguredGameItem).ToList();
        }

        /// <summary>
        /// Restores configured games from the snapshot taken at <see cref="CaptureCancelSnapshot"/>.
        /// </summary>
        public void RestoreCancelSnapshot()
        {
            if (_cancelSnapshot == null)
            {
                return;
            }

            using (BeginConfiguredGamesBulkUpdate())
            {
                List<ConfiguredGameItem> restored = _cancelSnapshot.Select(CloneConfiguredGameItem).ToList();
                ReplaceCollection(ConfiguredGames, restored);
                InvalidateAvailableGamesCache();
                RefreshFilters();
                NotifyConfiguredGamesStateChanged();
            }
        }

        /// <summary>
        /// Begins a bulk update of <see cref="ConfiguredGames"/>; dispose to complete and raise <see cref="ConfiguredGamesBulkUpdateCompleted"/>.
        /// </summary>
        /// <returns>A disposable scope.</returns>
        public IDisposable BeginConfiguredGamesBulkUpdate()
        {
            return new ConfiguredGamesBulkUpdateScope(this);
        }

        /// <summary>
        /// Ensures <see cref="AvailableGames"/> is populated with library games not yet configured.
        /// </summary>
        public void EnsureAvailableGamesLoaded()
        {
            if (_availableGamesLoaded)
            {
                return;
            }

            lock (_availableGamesLoadLock)
            {
                if (_availableGamesLoaded)
                {
                    return;
                }

                _ = SpinWait.SpinUntil(() => API.Instance.Database.IsOpen, -1);

                HashSet<Guid> configuredIds = new HashSet<Guid>(ConfiguredGames.Select(x => x.Id));
                List<AvailableGameItem> available = BuildAvailableGamesList(configuredIds);

                ReplaceCollection(AvailableGames, available);
                SortCollection(AvailableGames, (x, y) => string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase));
                AvailableGamesView?.Refresh();
                _availableGamesLoaded = true;
            }
        }

        /// <summary>
        /// Loads configured games asynchronously after the Playnite database is open.
        /// Unconfigured games are not loaded until <see cref="EnsureAvailableGamesLoaded"/> is called.
        /// </summary>
        /// <param name="gameSettings">Persisted per-game settings.</param>
        /// <returns>A task that completes when configured games are populated on the UI thread.</returns>
        public Task LoadFromAsync(IEnumerable<GameSettings> gameSettings)
        {
            IEnumerable<GameSettings> snapshot = gameSettings?.ToList() ?? new List<GameSettings>();

            return Task.Run(() =>
            {
                _ = SpinWait.SpinUntil(() => API.Instance.Database.IsOpen, -1);

                List<ConfiguredGameItem> configured = BuildConfiguredGamesList(snapshot);

                Application.Current.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    using (BeginConfiguredGamesBulkUpdate())
                    {
                        ReplaceCollection(ConfiguredGames, configured);
                        InvalidateAvailableGamesCache();
                        RefreshFilters();
                        NotifyConfiguredGamesStateChanged();
                    }
                }));
            });
        }

        /// <summary>
        /// Maps configured games to persisted <see cref="GameSettings"/> entries.
        /// </summary>
        /// <returns>Game settings ready for serialization.</returns>
        public List<GameSettings> ToGameSettingsList()
        {
            return ConfiguredGames.Select(x => x.ToGameSettings()).ToList();
        }

        /// <summary>
        /// Adds the selected available game to the configured games list.
        /// </summary>
        public void AddSelectedGame()
        {
            if (SelectedAvailableGame == null)
            {
                return;
            }

            AddConfiguredGame(SelectedAvailableGame);
            SelectedAvailableGame = null;
        }

        /// <summary>
        /// Adds a game to the configured list and removes it from available games.
        /// </summary>
        /// <param name="game">Available game to configure.</param>
        public void AddConfiguredGame(AvailableGameItem game)
        {
            if (game == null)
            {
                return;
            }

            AvailableGameItem existing = AvailableGames.FirstOrDefault(x => x.Id == game.Id);
            if (existing != null)
            {
                _ = AvailableGames.Remove(existing);
            }

            ConfiguredGames.Add(new ConfiguredGameItem
            {
                Id = game.Id,
                Icon = game.Icon,
                Name = game.Name,
                SourceName = game.SourceName,
                SourceIcon = game.SourceIcon,
                OverrideGlobalConfigs = false,
                UsedFilePattern = false,
                FilePattern = string.Empty,
                ScanSubFolders = false
            });

            SortCollections();
            RefreshFilters();
            NotifyConfiguredGamesStateChanged();
        }

        /// <summary>
        /// Removes a configured game and returns it to the available games list.
        /// </summary>
        /// <param name="gameId">Configured game identifier.</param>
        public void RemoveConfiguredGame(Guid gameId)
        {
            ConfiguredGameItem configured = ConfiguredGames.FirstOrDefault(x => x.Id == gameId);
            if (configured == null)
            {
                return;
            }

            _ = ConfiguredGames.Remove(configured);

            if (_availableGamesLoaded)
            {
                AvailableGames.Add(new AvailableGameItem
                {
                    Id = configured.Id,
                    Icon = configured.Icon,
                    Name = configured.Name,
                    SourceName = configured.SourceName,
                    SourceIcon = configured.SourceIcon
                });
            }

            SortCollections();
            RefreshFilters();
            NotifyConfiguredGamesStateChanged();
        }

        /// <summary>
        /// Converts all configured folder paths to relative paths.
        /// </summary>
        public void ConvertAllPathsToRelative()
        {
            ConvertAllPaths(relative: true);
        }

        /// <summary>
        /// Converts all configured folder paths to absolute paths.
        /// </summary>
        public void ConvertAllPathsToAbsolute()
        {
            ConvertAllPaths(relative: false);
        }

        /// <summary>
        /// Refreshes search filters on both game list views.
        /// </summary>
        public void RefreshFilters()
        {
            AvailableGamesView?.Refresh();
            ConfiguredGamesView?.Refresh();
        }

        private void LoadConfiguredGames(IEnumerable<GameSettings> gameSettings)
        {
            _ = SpinWait.SpinUntil(() => API.Instance.Database.IsOpen, -1);

            List<ConfiguredGameItem> configured = BuildConfiguredGamesList(gameSettings);

            using (BeginConfiguredGamesBulkUpdate())
            {
                ReplaceCollection(ConfiguredGames, configured);
                InvalidateAvailableGamesCache();
                RefreshFilters();
                NotifyConfiguredGamesStateChanged();
            }
        }

        private void InvalidateAvailableGamesCache()
        {
            _availableGamesLoaded = false;
            AvailableGames.Clear();
        }

        private void RemoveConfiguredGameFromCommand(object parameter)
        {
            if (parameter is ConfiguredGameItem game)
            {
                RemoveConfiguredGame(game.Id);
            }
            else if (parameter is Guid gameId)
            {
                RemoveConfiguredGame(gameId);
            }
        }

        private void NotifyConfiguredGamesStateChanged()
        {
            OnPropertyChanged(nameof(ConfiguredGamesCount));
            OnPropertyChanged(nameof(HasConfiguredGames));
            OnPropertyChanged(nameof(HasNoConfiguredGames));
            OnPropertyChanged(nameof(ConfiguredGamesSectionHeader));
        }

        private List<ConfiguredGameItem> BuildConfiguredGamesList(IEnumerable<GameSettings> gameSettings)
        {
            List<ConfiguredGameItem> configured = new List<ConfiguredGameItem>();

            foreach (GameSettings item in gameSettings ?? Enumerable.Empty<GameSettings>())
            {
                Game game = API.Instance.Database.Games.Get(item.Id);
                if (game == null)
                {
                    Logger.Warn(string.Format("Game is deleted - {0}", item.Id));
                    continue;
                }

                string icon = string.Empty;
                if (!game.Icon.IsNullOrEmpty())
                {
                    icon = API.Instance.Database.GetFullFilePath(game.Icon);
                }

                string sourceName = PlayniteTools.GetSourceName(item.Id);
                configured.Add(ConfiguredGameItem.FromGameSettings(
                    item,
                    icon,
                    game.Name,
                    sourceName,
                    TransformIcon.Get(sourceName, returnDefault: true)));
            }

            return configured
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private List<AvailableGameItem> BuildAvailableGamesList(HashSet<Guid> configuredIds)
        {
            List<AvailableGameItem> available = new List<AvailableGameItem>();

            foreach (Game game in API.Instance.Database.Games)
            {
                if (configuredIds.Contains(game.Id))
                {
                    continue;
                }

                string icon = string.Empty;
                if (!game.Icon.IsNullOrEmpty())
                {
                    icon = API.Instance.Database.GetFullFilePath(game.Icon);
                }

                string sourceName = PlayniteTools.GetSourceName(game.Id);
                available.Add(new AvailableGameItem
                {
                    Id = game.Id,
                    Icon = icon,
                    Name = game.Name,
                    SourceName = sourceName,
                    SourceIcon = TransformIcon.Get(sourceName, returnDefault: true)
                });
            }

            return available;
        }

        private void ConvertAllPaths(bool relative)
        {
            ClearSearch();

            GlobalProgressOptions progressOptions = new GlobalProgressOptions(_pluginName, true)
            {
                IsIndeterminate = false
            };

            _ = API.Instance.Dialogs.ActivateGlobalProgress(activateGlobalProgress =>
            {
                try
                {
                    activateGlobalProgress.ProgressMaxValue = ConfiguredGames.Count;

                    foreach (ConfiguredGameItem game in ConfiguredGames)
                    {
                        if (activateGlobalProgress.CancelToken.IsCancellationRequested)
                        {
                            break;
                        }

                        Game playniteGame = API.Instance.Database.Games.Get(game.Id);
                        if (playniteGame == null)
                        {
                            continue;
                        }

                        foreach (FolderEntryItem folder in game.ScreenshotsFolders)
                        {
                            folder.ScreenshotsFolder = relative
                                ? StorePlayniteTools.PathToRelativeWithStores(playniteGame, folder.ScreenshotsFolder)
                                : StorePlayniteTools.StringExpandWithStores(playniteGame, folder.ScreenshotsFolder);
                        }

                        activateGlobalProgress.CurrentProgressValue++;
                    }

                    Application.Current.Dispatcher?.BeginInvoke(new Action(() =>
                    {
                        SortCollections();
                        RefreshFilters();
                    }));
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, _pluginName);
                }
            }, progressOptions);
        }

        private bool FilterGameItem(object item)
        {
            if (string.IsNullOrEmpty(SearchText))
            {
                return true;
            }

            if (item is AvailableGameItem available)
            {
                return available.Name.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            if (item is ConfiguredGameItem configured)
            {
                return configured.Name.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            return true;
        }

        private void SortCollections()
        {
            using (BeginConfiguredGamesBulkUpdate())
            {
                SortCollection(ConfiguredGames, (x, y) => string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase));
            }

            SortCollection(AvailableGames, (x, y) => string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase));
        }

        private static ConfiguredGameItem CloneConfiguredGameItem(ConfiguredGameItem source)
        {
            if (source == null)
            {
                return null;
            }

            return ConfiguredGameItem.FromGameSettings(
                source.ToGameSettings(),
                source.Icon,
                source.Name,
                source.SourceName,
                source.SourceIcon);
        }

        private void EndConfiguredGamesBulkUpdate()
        {
            _configuredGamesBulkUpdateDepth--;
            if (_configuredGamesBulkUpdateDepth == 0)
            {
                ConfiguredGamesBulkUpdateCompleted?.Invoke(this, EventArgs.Empty);
            }
        }

        private sealed class ConfiguredGamesBulkUpdateScope : IDisposable
        {
            private readonly SsvGamesConfigurationViewModel _owner;
            private bool _disposed;

            public ConfiguredGamesBulkUpdateScope(SsvGamesConfigurationViewModel owner)
            {
                _owner = owner;
                _owner._configuredGamesBulkUpdateDepth++;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _owner.EndConfiguredGamesBulkUpdate();
            }
        }

        private static void SortCollection<T>(ObservableCollection<T> collection, Comparison<T> comparison)
        {
            List<T> sorted = collection.OrderBy(x => x, Comparer<T>.Create(comparison)).ToList();
            ReplaceCollection(collection, sorted);
        }

        private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> source)
        {
            target.Clear();
            foreach (T item in source)
            {
                target.Add(item);
            }
        }
    }
}
