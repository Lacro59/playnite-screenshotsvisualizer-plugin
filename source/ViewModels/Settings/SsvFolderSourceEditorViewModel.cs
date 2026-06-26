using CommonPluginsShared;
using CommonPluginsShared.Interfaces;
using Playnite.SDK;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace ScreenshotsVisualizer.ViewModels.Settings
{
    /// <summary>
    /// View model for the folder source editor dialog with live path/pattern preview.
    /// </summary>
    public class SsvFolderSourceEditorViewModel : INotifyPropertyChanged
    {
        private readonly SsvPathResolver _pathResolver = new SsvPathResolver();
        private readonly bool _isGlobalSourceEditor;
        private TestGameItem _selectedTestGame;
        private string _selectedAvailableSource;
        private SourceFilterMode _applicableSourceFilterMode;
        private SsvApplicableEmulatorFilter _applicableEmulatorFilter;

        /// <summary>
        /// Initializes a new editor view model.
        /// </summary>
        /// <param name="workingCopy">Editable folder source copy bound to the form.</param>
        /// <param name="preferredGameId">Optional preferred game id for preview context.</param>
        /// <param name="isGlobalSourceEditor">When <c>true</c>, shows global applicability options.</param>
        public SsvFolderSourceEditorViewModel(FolderEntryItem workingCopy, Guid? preferredGameId, bool isGlobalSourceEditor)
        {
            WorkingCopy = workingCopy ?? throw new ArgumentNullException(nameof(workingCopy));
            _isGlobalSourceEditor = isGlobalSourceEditor;
            TestGames = new ObservableCollection<TestGameItem>();
            AvailableSourceNames = new ObservableCollection<string>();
            ApplicableSourceEntries = new ObservableCollection<string>();

            WorkingCopy.PropertyChanged += WorkingCopy_PropertyChanged;

            if (_isGlobalSourceEditor)
            {
                LoadAvailableSourceNames();
                LoadApplicabilityFromWorkingCopy();
                NotifyPropertyChanged(nameof(IsApplicableSourceFilterAll));
                NotifyPropertyChanged(nameof(IsApplicableSourceFilterWhitelist));
                NotifyPropertyChanged(nameof(IsApplicableSourceFilterBlacklist));
                NotifyEmulatorFilterModePropertiesChanged();
                AddApplicableSourceCommand = new RelayCommand(AddApplicableSource);
                RemoveApplicableSourceCommand = new RelayCommand<object>(RemoveApplicableSource);
            }

            LoadTestGames(preferredGameId);
            UpdatePreview();
        }

        /// <summary>
        /// Gets whether applicability options are shown (global source editor).
        /// </summary>
        public bool IsGlobalSourceEditor => _isGlobalSourceEditor;

        /// <summary>
        /// Gets the editable source settings.
        /// </summary>
        public FolderEntryItem WorkingCopy { get; }

        /// <summary>
        /// Gets games available as test context for live preview.
        /// </summary>
        public ObservableCollection<TestGameItem> TestGames { get; }

        /// <summary>
        /// Gets distinct Playnite source names available for applicability filtering.
        /// </summary>
        public ObservableCollection<string> AvailableSourceNames { get; }

        /// <summary>
        /// Gets or sets the source name selected in the add-source combo box.
        /// </summary>
        public string SelectedAvailableSource
        {
            get => _selectedAvailableSource;
            set
            {
                if (string.Equals(_selectedAvailableSource, value, StringComparison.Ordinal))
                {
                    return;
                }

                _selectedAvailableSource = value;
                NotifyPropertyChanged();
            }
        }

        /// <summary>
        /// Gets the editable list of applicable Playnite source names.
        /// </summary>
        public ObservableCollection<string> ApplicableSourceEntries { get; }

        /// <summary>
        /// Gets or sets how Playnite sources constrain this global source.
        /// </summary>
        public SourceFilterMode ApplicableSourceFilterMode
        {
            get => _applicableSourceFilterMode;
            set
            {
                if (_applicableSourceFilterMode == value)
                {
                    return;
                }

                _applicableSourceFilterMode = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(IsApplicableSourceListEnabled));
                NotifyPropertyChanged(nameof(IsApplicableSourceFilterAll));
                NotifyPropertyChanged(nameof(IsApplicableSourceFilterWhitelist));
                NotifyPropertyChanged(nameof(IsApplicableSourceFilterBlacklist));
            }
        }

        /// <summary>
        /// Gets whether the applicable source list is enabled.
        /// </summary>
        public bool IsApplicableSourceListEnabled =>
            ApplicableSourceFilterMode == SourceFilterMode.Whitelist
            || ApplicableSourceFilterMode == SourceFilterMode.Blacklist;

        /// <summary>
        /// Gets or sets the optional emulator applicability filter.
        /// </summary>
        public SsvApplicableEmulatorFilter ApplicableEmulatorFilter
        {
            get => _applicableEmulatorFilter;
            set
            {
                if (_applicableEmulatorFilter == value)
                {
                    return;
                }

                _applicableEmulatorFilter = value;
                NotifyPropertyChanged();
                NotifyEmulatorFilterModePropertiesChanged();
            }
        }

        /// <summary>
        /// Gets or sets whether all Playnite sources are applicable.
        /// </summary>
        public bool IsApplicableSourceFilterAll
        {
            get => ApplicableSourceFilterMode == SourceFilterMode.All;
            set
            {
                if (value)
                {
                    ApplicableSourceFilterMode = SourceFilterMode.All;
                }
            }
        }

        /// <summary>
        /// Gets or sets whether only listed sources are applicable.
        /// </summary>
        public bool IsApplicableSourceFilterWhitelist
        {
            get => ApplicableSourceFilterMode == SourceFilterMode.Whitelist;
            set
            {
                if (value)
                {
                    ApplicableSourceFilterMode = SourceFilterMode.Whitelist;
                }
            }
        }

        /// <summary>
        /// Gets or sets whether listed sources are excluded.
        /// </summary>
        public bool IsApplicableSourceFilterBlacklist
        {
            get => ApplicableSourceFilterMode == SourceFilterMode.Blacklist;
            set
            {
                if (value)
                {
                    ApplicableSourceFilterMode = SourceFilterMode.Blacklist;
                }
            }
        }

        /// <summary>
        /// Gets or sets whether no emulator filter is applied.
        /// </summary>
        public bool IsApplicableEmulatorNone
        {
            get => ApplicableEmulatorFilter == SsvApplicableEmulatorFilter.None;
            set
            {
                if (value)
                {
                    ApplicableEmulatorFilter = SsvApplicableEmulatorFilter.None;
                }
            }
        }

        /// <summary>
        /// Gets or sets whether only RetroArch games are applicable.
        /// </summary>
        public bool IsApplicableEmulatorRetroArch
        {
            get => ApplicableEmulatorFilter == SsvApplicableEmulatorFilter.RetroArch;
            set
            {
                if (value)
                {
                    ApplicableEmulatorFilter = SsvApplicableEmulatorFilter.RetroArch;
                }
            }
        }

        /// <summary>
        /// Gets or sets whether only ScummVM games are applicable.
        /// </summary>
        public bool IsApplicableEmulatorScummVM
        {
            get => ApplicableEmulatorFilter == SsvApplicableEmulatorFilter.ScummVM;
            set
            {
                if (value)
                {
                    ApplicableEmulatorFilter = SsvApplicableEmulatorFilter.ScummVM;
                }
            }
        }

        /// <summary>
        /// Gets the command that adds the selected source to the applicability list.
        /// </summary>
        public ICommand AddApplicableSourceCommand { get; }

        /// <summary>
        /// Gets the command that removes a source from the applicability list.
        /// </summary>
        public ICommand RemoveApplicableSourceCommand { get; }

        /// <summary>
        /// Gets or sets the selected test game used for preview expansion.
        /// </summary>
        public TestGameItem SelectedTestGame
        {
            get => _selectedTestGame;
            set
            {
                if (_selectedTestGame == value)
                {
                    return;
                }

                _selectedTestGame = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(HasSelectedTestGame));
                UpdatePreview();
            }
        }

        /// <summary>
        /// Gets whether a test game is selected.
        /// </summary>
        public bool HasSelectedTestGame => SelectedTestGame != null;

        /// <summary>
        /// Gets the resolved folder path preview.
        /// </summary>
        public string ResolvedPath { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the resolved file pattern regex preview.
        /// </summary>
        public string ResolvedFilePattern { get; private set; } = string.Empty;

        /// <summary>
        /// Gets whether the resolved folder currently exists.
        /// </summary>
        public bool ResolvedPathExists { get; private set; }

        /// <summary>
        /// Gets the localized status text for resolved folder existence.
        /// </summary>
        public string ResolvedPathStatusText =>
            ResolvedPathExists
                ? ResourceProvider.GetString("LOCSsvConfigResolvedPathExists")
                : ResourceProvider.GetString("LOCSsvConfigResolvedPathMissing");

        /// <summary>
        /// Copies applicability fields from the view model into <see cref="WorkingCopy"/>.
        /// </summary>
        public void ApplyApplicabilityToWorkingCopy()
        {
            if (!_isGlobalSourceEditor)
            {
                return;
            }

            WorkingCopy.ApplicableSourceFilterMode = ApplicableSourceFilterMode;
            WorkingCopy.ApplicableEmulatorFilter = ApplicableEmulatorFilter;
            WorkingCopy.SetApplicableSources(ApplicableSourceEntries);
        }

        private void WorkingCopy_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FolderEntryItem.ScreenshotsFolder)
                || e.PropertyName == nameof(FolderEntryItem.UsedFilePattern)
                || e.PropertyName == nameof(FolderEntryItem.FilePattern))
            {
                UpdatePreview();
            }
        }

        private void LoadAvailableSourceNames()
        {
            foreach (string sourceName in API.Instance.Database.Games
                .Select(PlayniteTools.GetSourceName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                AvailableSourceNames.Add(sourceName);
            }

            SelectedAvailableSource = AvailableSourceNames.FirstOrDefault();
        }

        private void LoadApplicabilityFromWorkingCopy()
        {
            ApplicableSourceFilterMode = WorkingCopy.ApplicableSourceFilterMode;
            ApplicableEmulatorFilter = WorkingCopy.ApplicableEmulatorFilter;
            ApplicableSourceEntries.Clear();
            foreach (string source in WorkingCopy.ApplicableSources)
            {
                ApplicableSourceEntries.Add(source);
            }
        }

        private void AddApplicableSource()
        {
            if (!IsApplicableSourceListEnabled
                || string.IsNullOrWhiteSpace(SelectedAvailableSource)
                || ApplicableSourceEntries.Any(x => string.Equals(x, SelectedAvailableSource, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            ApplicableSourceEntries.Add(SelectedAvailableSource.Trim());
        }

        private void RemoveApplicableSource(object parameter)
        {
            if (parameter is string source)
            {
                ApplicableSourceEntries.Remove(source);
            }
        }

        private void NotifyEmulatorFilterModePropertiesChanged()
        {
            NotifyPropertyChanged(nameof(IsApplicableEmulatorNone));
            NotifyPropertyChanged(nameof(IsApplicableEmulatorRetroArch));
            NotifyPropertyChanged(nameof(IsApplicableEmulatorScummVM));
        }

        private void LoadTestGames(Guid? preferredGameId)
        {
            foreach (Game game in API.Instance.Database.Games.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                TestGames.Add(new TestGameItem(game));
            }

            if (!TestGames.Any())
            {
                SelectedTestGame = null;
                return;
            }

            SelectedTestGame = preferredGameId.HasValue
                ? TestGames.FirstOrDefault(x => x.Id == preferredGameId.Value) ?? TestGames[0]
                : TestGames[0];
        }

        /// <summary>
        /// Refreshes resolved path and pattern previews from the current inputs.
        /// </summary>
        public void UpdatePreview()
        {
            if (!HasSelectedTestGame)
            {
                ResolvedPath = string.Empty;
                ResolvedFilePattern = string.Empty;
                ResolvedPathExists = false;
                NotifyPreviewChanged();
                return;
            }

            FolderSettings model = WorkingCopy.ToModel();
            ResolvedPath = _pathResolver.ResolvePath(SelectedTestGame.Game, model);
            ResolvedFilePattern = _pathResolver.ResolveFilePatternRegex(SelectedTestGame.Game, model);
            ResolvedPathExists = !string.IsNullOrEmpty(ResolvedPath) && Directory.Exists(ResolvedPath);
            NotifyPreviewChanged();
        }

        private void NotifyPreviewChanged()
        {
            NotifyPropertyChanged(nameof(ResolvedPath));
            NotifyPropertyChanged(nameof(ResolvedFilePattern));
            NotifyPropertyChanged(nameof(ResolvedPathExists));
            NotifyPropertyChanged(nameof(ResolvedPathStatusText));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Lightweight game item used by source editor preview selection.
    /// </summary>
    public class TestGameItem
    {
        public TestGameItem(Game game)
        {
            Game = game;
        }

        public Guid Id => Game?.Id ?? Guid.Empty;

        public string Name => Game?.Name ?? string.Empty;

        public Game Game { get; }
    }
}
