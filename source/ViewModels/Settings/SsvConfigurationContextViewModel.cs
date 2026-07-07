using CommonPluginsShared;
using Playnite.SDK;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.Services;
using ScreenshotsVisualizer.Views.Settings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;

namespace ScreenshotsVisualizer.ViewModels.Settings
{
    /// <summary>
    /// View model for master-detail configuration: global or per-game context on the left, folder sources on the right.
    /// </summary>
    public class SsvConfigurationContextViewModel : ObservableObject
    {
        private readonly ObservableCollection<FolderEntryItem> _globalSources = new ObservableCollection<FolderEntryItem>();
        private SsvConfigurationContextItem _selectedContext = SsvConfigurationContextItem.Global;
        private ObservableCollection<FolderEntryItem> _activeSources;
        private bool _selectedIsGlobal = true;
        private Guid? _selectedGameId;
        private List<FolderSettings> _cancelGlobalSourcesSnapshot;
        private bool _cancelSelectedIsGlobal = true;
        private Guid? _cancelSelectedGameId;
        private string _contextSearchText = string.Empty;
        private readonly ObservableCollection<SsvConfigurationContextItem> _filteredContextItems = new ObservableCollection<SsvConfigurationContextItem>();

        /// <summary>
        /// Initializes a new configuration context view model.
        /// </summary>
        /// <param name="gamesConfiguration">Shared games configuration view model.</param>
        public SsvConfigurationContextViewModel(SsvGamesConfigurationViewModel gamesConfiguration)
        {
            GamesConfiguration = gamesConfiguration ?? throw new ArgumentNullException(nameof(gamesConfiguration));

            ContextItems = new ObservableCollection<SsvConfigurationContextItem> { SsvConfigurationContextItem.Global };
            _filteredContextItems.Add(SsvConfigurationContextItem.Global);
            _activeSources = _globalSources;

            _globalSources.CollectionChanged += OnGlobalSourcesCollectionChanged;
            GamesConfiguration.ConfiguredGames.CollectionChanged += OnConfiguredGamesCollectionChanged;
            GamesConfiguration.ConfiguredGamesBulkUpdateCompleted += OnConfiguredGamesBulkUpdateCompleted;
            GamesConfiguration.PropertyChanged += OnGamesConfigurationPropertyChanged;

            AddActiveSourceCommand = new RelayCommand(OpenAddSourceEditor);
            EditActiveSourceCommand = new RelayCommand<object>(EditActiveSourceFromCommand);
            RemoveActiveSourceCommand = new RelayCommand<object>(RemoveActiveSourceFromCommand);
            BrowseActiveSourceCommand = new RelayCommand<object>(BrowseActiveSourceFromCommand);
            ReplaceDigitActiveSourceCommand = new RelayCommand<object>(ReplaceDigitActiveSourceFromCommand);
            ApplyQuickAddPresetCommand = new RelayCommand<object>(ApplyQuickAddPresetFromCommand);
            SelectContextCommand = new RelayCommand<object>(SelectContextFromCommand);
            AddConfiguredGameCommand = new RelayCommand(OpenAddConfiguredGameDialog);

            InitializeQuickAddPresets();
        }

        /// <summary>
        /// Gets the shared games configuration view model.
        /// </summary>
        public SsvGamesConfigurationViewModel GamesConfiguration { get; }

        /// <summary>
        /// Gets selectable context entries (global node followed by configured games).
        /// </summary>
        public ObservableCollection<SsvConfigurationContextItem> ContextItems { get; }

        /// <summary>
        /// Gets the filtered context list shown in the left panel.
        /// </summary>
        public ObservableCollection<SsvConfigurationContextItem> FilteredContextItems => _filteredContextItems;

        /// <summary>
        /// Gets or sets the search text applied to configured game contexts.
        /// </summary>
        public string ContextSearchText
        {
            get => _contextSearchText;
            set
            {
                string normalized = value ?? string.Empty;
                if (string.Equals(_contextSearchText, normalized, StringComparison.Ordinal))
                {
                    return;
                }

                Common.LogDebug(true, $"[SsvConfigContext] ContextSearchText changed: '{_contextSearchText}' -> '{normalized}'");
                SetValue(ref _contextSearchText, normalized);
                RefreshFilteredContexts();
            }
        }

        /// <summary>
        /// Gets folder sources for the currently selected context.
        /// </summary>
        public ObservableCollection<FolderEntryItem> ActiveSources => _activeSources;

        /// <summary>
        /// Gets or sets the selected configuration context.
        /// </summary>
        public SsvConfigurationContextItem SelectedContext
        {
            get => _selectedContext;
            set
            {
                SsvConfigurationContextItem normalizedContext = value ?? SsvConfigurationContextItem.Global;
                if (_selectedContext == normalizedContext)
                {
                    return;
                }

                Common.LogDebug(true, $"[SsvConfigContext] SelectedContext change requested: '{_selectedContext?.DisplayName ?? "null"}' -> '{normalizedContext.DisplayName}'");
                _selectedContext = normalizedContext;
                _selectedIsGlobal = _selectedContext.IsGlobal;
                _selectedGameId = _selectedContext.Game?.Id;
                Common.LogDebug(true, $"[SsvConfigContext] SelectedContext applied: '{_selectedContext.DisplayName}', IsGlobal={_selectedIsGlobal}, GameId={_selectedGameId}");
                OnPropertyChanged();
                NotifyContextSelectionChanged();
                UpdateActiveSourcesBinding();
            }
        }

        /// <summary>
        /// Gets whether the global context is selected.
        /// </summary>
        public bool IsGlobalContextSelected => _selectedContext?.IsGlobal ?? true;

        /// <summary>
        /// Gets whether a configured game context is selected.
        /// </summary>
        public bool IsGameContextSelected => !IsGlobalContextSelected;

        /// <summary>
        /// Gets the configured game for the active game context.
        /// </summary>
        public ConfiguredGameItem SelectedConfiguredGame => _selectedContext?.Game;

        /// <summary>
        /// Gets the display name of the active context (global label or game name).
        /// </summary>
        public string ActiveContextDisplayName => _selectedContext?.DisplayName ?? string.Empty;

        /// <summary>
        /// Gets whether the active context has at least one configured source path.
        /// </summary>
        public bool HasActiveSources =>
            _activeSources?.Any(x => !string.IsNullOrWhiteSpace(x.ScreenshotsFolder)) ?? false;

        /// <summary>
        /// Gets whether the active context has no sources yet.
        /// </summary>
        public bool HasNoActiveSources => !HasActiveSources;

        /// <summary>
        /// Gets the localized sources panel header for the active context.
        /// </summary>
        public string ActiveSourcesSectionHeader =>
            string.Format(
                ResourceProvider.GetString("LOCSsvConfigSectionSourcesFor"),
                ActiveContextDisplayName);

        /// <summary>
        /// Gets the localized configured games section header including the count.
        /// </summary>
        public string ConfiguredGamesSectionHeader => GamesConfiguration.ConfiguredGamesSectionHeader;

        /// <summary>
        /// Gets the command that opens the editor to add a source in the active context.
        /// </summary>
        public RelayCommand AddActiveSourceCommand { get; }

        /// <summary>
        /// Gets the command that opens the editor for an existing source in the active context.
        /// </summary>
        public RelayCommand<object> EditActiveSourceCommand { get; }

        /// <summary>
        /// Gets the command that removes a source row from the active context.
        /// </summary>
        public RelayCommand<object> RemoveActiveSourceCommand { get; }

        /// <summary>
        /// Gets the command that opens a folder picker for a source row in the active context.
        /// </summary>
        public RelayCommand<object> BrowseActiveSourceCommand { get; }

        /// <summary>
        /// Gets the command that replaces numeric sequences with the digit token in the active context.
        /// </summary>
        public RelayCommand<object> ReplaceDigitActiveSourceCommand { get; }

        /// <summary>
        /// Gets built-in quick-add preset buttons bound from the catalog.
        /// </summary>
        public ObservableCollection<SsvFolderPresetQuickAddItem> QuickAddPresets { get; } = new ObservableCollection<SsvFolderPresetQuickAddItem>();

        /// <summary>
        /// Gets the command that applies a built-in preset to global sources.
        /// </summary>
        public RelayCommand<object> ApplyQuickAddPresetCommand { get; }

        /// <summary>
        /// Gets the command that selects a context item from a command parameter.
        /// </summary>
        public RelayCommand<object> SelectContextCommand { get; }

        /// <summary>
        /// Gets the command that opens the unconfigured game selection dialog.
        /// </summary>
        public RelayCommand AddConfiguredGameCommand { get; }

        private FolderEntryItem _selectedActiveSource;

        /// <summary>
        /// Gets or sets the selected source row in the active context list.
        /// </summary>
        public FolderEntryItem SelectedActiveSource
        {
            get => _selectedActiveSource;
            set
            {
                SetValue(ref _selectedActiveSource, value);
                OnPropertyChanged(nameof(HasSelectedActiveSource));
            }
        }

        /// <summary>
        /// Gets whether a source row is selected for editing.
        /// </summary>
        public bool HasSelectedActiveSource => SelectedActiveSource != null;

        /// <summary>
        /// Opens the editor for <see cref="SelectedActiveSource"/> when set.
        /// </summary>
        public void EditSelectedActiveSource()
        {
            if (SelectedActiveSource != null)
            {
                OpenSourceEditor(SelectedActiveSource);
            }
        }

        /// <summary>
        /// Loads global sources and rebuilds the context list from persisted settings.
        /// </summary>
        /// <param name="settings">Plugin settings being edited.</param>
        public void LoadFrom(ScreenshotsVisualizerSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.EnsureGlobalScreenshotSourcesMigrated();
            ReplaceGlobalSources(settings.GetEffectiveGlobalScreenshotSources());
            RebuildContextItems();
            RestoreSelection();
        }

        /// <summary>
        /// Reloads global sources from settings without changing the selected game id when possible.
        /// </summary>
        /// <param name="settings">Plugin settings being edited.</param>
        public void ReloadFrom(ScreenshotsVisualizerSettings settings)
        {
            LoadFrom(settings);
        }

        /// <summary>
        /// Captures global sources and selection for fast restore on cancel.
        /// </summary>
        public void CaptureCancelSnapshot()
        {
            _cancelGlobalSourcesSnapshot = _globalSources.Select(x => x.ToModel()).ToList();
            _cancelSelectedIsGlobal = _selectedIsGlobal;
            _cancelSelectedGameId = _selectedGameId;
        }

        /// <summary>
        /// Restores global sources and selection from the cancel snapshot.
        /// </summary>
        public void RestoreCancelSnapshot()
        {
            if (_cancelGlobalSourcesSnapshot == null)
            {
                return;
            }

            ReplaceGlobalSources(_cancelGlobalSourcesSnapshot);
            _selectedIsGlobal = _cancelSelectedIsGlobal;
            _selectedGameId = _cancelSelectedGameId;
            RebuildContextItems();
            RestoreSelection();
            UpdateActiveSourcesBinding();
        }

        /// <summary>
        /// Writes global sources from the view model back to plugin settings.
        /// </summary>
        /// <param name="settings">Plugin settings being saved.</param>
        public void ApplyToSettings(ScreenshotsVisualizerSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.GlobalScreenshotSources = _globalSources.Select(x => x.ToModel()).ToList();
        }

        /// <summary>
        /// Selects the global context.
        /// </summary>
        public void SelectGlobal()
        {
            SelectedContext = SsvConfigurationContextItem.Global;
        }

        /// <summary>
        /// Selects a configured game context by identifier.
        /// </summary>
        /// <param name="gameId">Configured game identifier.</param>
        public void SelectGameById(Guid gameId)
        {
            SsvConfigurationContextItem context = ContextItems.FirstOrDefault(x => x.Game?.Id == gameId);
            if (context != null)
            {
                SelectedContext = context;
            }
        }

        /// <summary>
        /// Gets whether a built-in preset can be added to global sources.
        /// </summary>
        /// <param name="presetId">Preset identifier.</param>
        /// <returns><c>true</c> when the canonical preset is not already configured.</returns>
        public bool CanApplyGlobalPreset(SsvFolderPresetId presetId)
        {
            return !SsvFolderPresetService.IsGlobalPresetPresent(_globalSources, presetId);
        }

        /// <summary>
        /// Adds a built-in preset to global screenshot sources (ignores the active game context).
        /// </summary>
        /// <param name="preset">Preset to apply.</param>
        public void ApplyPresetToActiveContext(SsvFolderPresetId preset)
        {
            SelectGlobal();

            SsvFolderPresetService.TryAddToGlobal(_globalSources, preset);
            NotifyActiveSourcesChanged();
            RefreshPresetCommandsCanExecute();
        }

        /// <summary>
        /// Removes a folder row from the active context.
        /// </summary>
        /// <param name="folder">Folder row to remove.</param>
        public void RemoveActiveSource(FolderEntryItem folder)
        {
            if (_activeSources == null || folder == null)
            {
                return;
            }

            if (_activeSources.Remove(folder))
            {
                if (SelectedActiveSource == folder)
                {
                    SelectedActiveSource = null;
                }

                NotifyActiveSourcesChanged();
                RefreshPresetCommandsCanExecute();
            }
        }

        /// <summary>
        /// Opens a folder picker and updates the selected folder path.
        /// </summary>
        /// <param name="folder">Folder row to update.</param>
        public void BrowseActiveSource(FolderEntryItem folder)
        {
            if (folder == null)
            {
                return;
            }

            string selectedFolder = API.Instance.Dialogs.SelectFolder();
            if (!string.IsNullOrEmpty(selectedFolder))
            {
                folder.ScreenshotsFolder = selectedFolder;
            }
        }

        /// <summary>
        /// Replaces numeric sequences in a file pattern with the <c>{digit}</c> token.
        /// </summary>
        /// <param name="folder">Folder row to update.</param>
        public void ReplaceDigitInActiveSource(FolderEntryItem folder)
        {
            if (folder == null || string.IsNullOrEmpty(folder.FilePattern))
            {
                return;
            }

            folder.FilePattern = Regex.Replace(folder.FilePattern, @"\d+", "{digit}");
        }

        private void OnConfiguredGamesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (GamesConfiguration.IsConfiguredGamesBulkUpdate)
            {
                return;
            }

            RebuildContextItems();
            RestoreSelection();
        }

        private void OnConfiguredGamesBulkUpdateCompleted(object sender, EventArgs e)
        {
            RebuildContextItems();
            RestoreSelection();
        }

        private void OnGamesConfigurationPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SsvGamesConfigurationViewModel.ConfiguredGamesSectionHeader))
            {
                OnPropertyChanged(nameof(ConfiguredGamesSectionHeader));
            }
        }

        private void RebuildContextItems()
        {
            ContextItems.Clear();
            ContextItems.Add(SsvConfigurationContextItem.Global);

            foreach (ConfiguredGameItem game in GamesConfiguration.ConfiguredGames)
            {
                ContextItems.Add(SsvConfigurationContextItem.ForGame(game));
            }

            RefreshFilteredContexts();
        }

        private void RefreshFilteredContexts()
        {
            string query = _contextSearchText?.Trim();
            Guid? selectedGameId = _selectedContext?.Game?.Id;

            _filteredContextItems.Clear();

            foreach (SsvConfigurationContextItem context in ContextItems)
            {
                bool keep = context.IsGlobal;

                if (!keep)
                {
                    if (_selectedContext != null && context.Game?.Id == selectedGameId)
                    {
                        keep = true;
                    }
                    else if (string.IsNullOrEmpty(query))
                    {
                        keep = true;
                    }
                    else
                    {
                        keep = context.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                        Common.LogDebug(true, $"[SsvConfigContext] FilterContextItem: query='{query}', context='{context.DisplayName}', match={keep}");
                    }
                }

                if (keep)
                {
                    _filteredContextItems.Add(context);
                }
            }

            OnPropertyChanged(nameof(FilteredContextItems));
        }

        private void RestoreSelection()
        {
            if (_selectedIsGlobal)
            {
                SelectGlobal();
                return;
            }

            if (_selectedGameId.HasValue)
            {
                SsvConfigurationContextItem context = ContextItems.FirstOrDefault(x => x.Game?.Id == _selectedGameId.Value);
                if (context != null)
                {
                    SelectedContext = context;
                    return;
                }
            }

            SelectGlobal();
        }

        private void UpdateActiveSourcesBinding()
        {
            SelectedActiveSource = null;

            if (IsGlobalContextSelected)
            {
                _activeSources = _globalSources;
            }
            else if (SelectedConfiguredGame != null)
            {
                _activeSources = SelectedConfiguredGame.ScreenshotsFolders;
            }
            else
            {
                _activeSources = _globalSources;
            }

            OnPropertyChanged(nameof(ActiveSources));
            NotifyActiveSourcesChanged();
        }

        private void NotifyContextSelectionChanged()
        {
            OnPropertyChanged(nameof(IsGlobalContextSelected));
            OnPropertyChanged(nameof(IsGameContextSelected));
            OnPropertyChanged(nameof(SelectedConfiguredGame));
            OnPropertyChanged(nameof(ActiveContextDisplayName));
            OnPropertyChanged(nameof(ActiveSourcesSectionHeader));
        }

        private void NotifyActiveSourcesChanged()
        {
            OnPropertyChanged(nameof(HasActiveSources));
            OnPropertyChanged(nameof(HasNoActiveSources));
        }

        private void ReplaceGlobalSources(IEnumerable<FolderSettings> sources)
        {
            _globalSources.Clear();

            foreach (FolderSettings source in sources ?? Enumerable.Empty<FolderSettings>())
            {
                if (source == null || string.IsNullOrWhiteSpace(source.ScreenshotsFolder))
                {
                    continue;
                }

                _globalSources.Add(new FolderEntryItem(source));
            }

            RefreshPresetCommandsCanExecute();
        }

        private void OnGlobalSourcesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshPresetCommandsCanExecute();
        }

        private void RefreshPresetCommandsCanExecute()
        {
            foreach (SsvFolderPresetQuickAddItem item in QuickAddPresets)
            {
                item.CanApply = CanApplyGlobalPreset(item.PresetId);
            }
        }

        private void InitializeQuickAddPresets()
        {
            QuickAddPresets.Clear();

            foreach (SsvFolderPreset preset in SsvFolderPresetCatalog.GetAll())
            {
                QuickAddPresets.Add(new SsvFolderPresetQuickAddItem(preset));
            }

            RefreshPresetCommandsCanExecute();
        }

        private void ApplyQuickAddPresetFromCommand(object parameter)
        {
            if (parameter is SsvFolderPresetId presetId)
            {
                ApplyPresetToActiveContext(presetId);
            }
        }

        private void RemoveActiveSourceFromCommand(object parameter)
        {
            if (parameter is FolderEntryItem folder)
            {
                RemoveActiveSource(folder);
            }
        }

        private void BrowseActiveSourceFromCommand(object parameter)
        {
            if (parameter is FolderEntryItem folder)
            {
                BrowseActiveSource(folder);
            }
        }

        private void ReplaceDigitActiveSourceFromCommand(object parameter)
        {
            if (parameter is FolderEntryItem folder)
            {
                ReplaceDigitInActiveSource(folder);
            }
        }

        private void SelectContextFromCommand(object parameter)
        {
            if (parameter is SsvConfigurationContextItem context)
            {
                SelectedContext = context;
            }
            else if (parameter is ConfiguredGameItem game)
            {
                SelectGameById(game.Id);
            }
        }

        private void OpenAddConfiguredGameDialog()
        {
            GamesConfiguration.EnsureAvailableGamesLoaded();

            SsvSelectUnconfiguredGameView view = new SsvSelectUnconfiguredGameView(GamesConfiguration, this);
            Window window = PlayniteUiHelper.CreateExtensionWindow(
                ResourceProvider.GetString("LOCSsvConfigSelectGameTitle"),
                view,
                new WindowOptions
                {
                    Width = 520,
                    Height = 480,
                    MinWidth = 400,
                    MinHeight = 320,
                    CanBeResizable = true
                });
            window.ResizeMode = ResizeMode.CanResize;
            _ = window.ShowDialog();
        }

        private void OpenAddSourceEditor()
        {
            OpenSourceEditor(null);
        }

        private void EditActiveSourceFromCommand(object parameter)
        {
            if (parameter is FolderEntryItem folder)
            {
                OpenSourceEditor(folder);
            }
        }

        private void OpenSourceEditor(FolderEntryItem targetEntry)
        {
            if (_activeSources == null)
            {
                return;
            }

            if (targetEntry != null && !_activeSources.Contains(targetEntry))
            {
                return;
            }

            bool isAdd = targetEntry == null;
            SsvFolderSourceEditorView view = new SsvFolderSourceEditorView(
                targetEntry,
                _activeSources,
                SelectedConfiguredGame?.Id,
                IsGlobalContextSelected,
                OnActiveSourcesEdited);

            string titleKey = isAdd ? "LOCSsvConfigAddSourceTitle" : "LOCSsvConfigEditSourceTitle";
            Window window = PlayniteUiHelper.CreateExtensionWindow(
                ResourceProvider.GetString(titleKey),
                view,
                new WindowOptions
                {
                    Width = 700,
                    Height = IsGlobalContextSelected ? 760 : 500,
                    MinWidth = 560,
                    MinHeight = IsGlobalContextSelected ? 680 : 420,
                    CanBeResizable = true
                });
            window.ResizeMode = ResizeMode.CanResize;
            _ = window.ShowDialog();
        }

        private void OnActiveSourcesEdited()
        {
            NotifyActiveSourcesChanged();
            RefreshPresetCommandsCanExecute();
        }
    }
}
