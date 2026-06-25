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

namespace ScreenshotsVisualizer.ViewModels.Settings
{
    /// <summary>
    /// View model for the folder source editor dialog with live path/pattern preview.
    /// </summary>
    public class SsvFolderSourceEditorViewModel : INotifyPropertyChanged
    {
        private readonly SsvPathResolver _pathResolver = new SsvPathResolver();
        private TestGameItem _selectedTestGame;

        /// <summary>
        /// Initializes a new editor view model.
        /// </summary>
        /// <param name="workingCopy">Editable folder source copy bound to the form.</param>
        /// <param name="preferredGameId">Optional preferred game id for preview context.</param>
        public SsvFolderSourceEditorViewModel(FolderEntryItem workingCopy, Guid? preferredGameId)
        {
            WorkingCopy = workingCopy ?? throw new ArgumentNullException(nameof(workingCopy));
            TestGames = new ObservableCollection<TestGameItem>();

            WorkingCopy.PropertyChanged += WorkingCopy_PropertyChanged;

            LoadTestGames(preferredGameId);
            UpdatePreview();
        }

        /// <summary>
        /// Gets the editable source settings.
        /// </summary>
        public FolderEntryItem WorkingCopy { get; }

        /// <summary>
        /// Gets games available as test context for live preview.
        /// </summary>
        public ObservableCollection<TestGameItem> TestGames { get; }

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

        private void WorkingCopy_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FolderEntryItem.ScreenshotsFolder)
                || e.PropertyName == nameof(FolderEntryItem.UsedFilePattern)
                || e.PropertyName == nameof(FolderEntryItem.FilePattern))
            {
                UpdatePreview();
            }
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
