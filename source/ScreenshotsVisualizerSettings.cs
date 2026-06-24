using Playnite.SDK;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.ViewModels.Settings;
using Playnite.SDK.Data;
using System.Collections.Generic;
using ScreenshotsVisualizer.Models.StartPage;
using System.Linq;
using CommonPluginsShared.Extensions;
using System.Threading.Tasks;
using System;
using CommonPluginsShared.Plugins;
using CommonPluginsShared.Interfaces;

namespace ScreenshotsVisualizer
{
    public class ScreenshotsVisualizerSettings : PluginSettings
    {
        public ScreenshotsVisualizerSettings()
        {
            ApplyFixedLibraryFilterPolicy();
            EnsureGlobalScreenshotSourcesMigrated();
        }

        /// <summary>
        /// Applies fixed library filter values for this plugin (not user-configurable).
        /// </summary>
        public void ApplyFixedLibraryFilterPolicy()
        {
            LibrarySourceFilterMode = SourceFilterMode.All;
            EnabledSources = new List<string>();
            ExcludedSources = new List<string>();
        }

        #region Settings variables

        public bool EnableIntegrationButtonHeader { get; set; } = false;
        public bool EnableIntegrationButtonSide { get; set; } = true;

        private bool _enableIntegrationViewItem = true;
        public bool EnableIntegrationViewItem { get => _enableIntegrationViewItem; set => SetValue(ref _enableIntegrationViewItem, value); }

        private bool _enableIntegrationButton = true;
        public bool EnableIntegrationButton { get => _enableIntegrationButton; set => SetValue(ref _enableIntegrationButton, value); }

        private bool _enableIntegrationButtonDetails = false;
        public bool EnableIntegrationButtonDetails { get => _enableIntegrationButtonDetails; set => SetValue(ref _enableIntegrationButtonDetails, value); }

        private bool _enableIntegrationShowSinglePicture = true;
        public bool EnableIntegrationShowSinglePicture { get => _enableIntegrationShowSinglePicture; set => SetValue(ref _enableIntegrationShowSinglePicture, value); }

        public double IntegrationShowSinglePictureHeight { get; set; } = 150;
        public bool OpenViewerWithOnSelectionSinglePicture { get; set; } = false;
        public bool AddBorderSinglePicture { get; set; } = true;
        public bool AddRoundedCornerSinglePicture { get; set; } = false;

        private bool _enableIntegrationShowPictures = true;
        public bool EnableIntegrationShowPictures { get => _enableIntegrationShowPictures; set => SetValue(ref _enableIntegrationShowPictures, value); }

        private bool _enableIntegrationShowPicturesVertical = true;
        public bool EnableIntegrationShowPicturesVertical { get => _enableIntegrationShowPicturesVertical; set => SetValue(ref _enableIntegrationShowPicturesVertical, value); }

        private bool _enableIntegrationPicturesList = true;
        public bool EnableIntegrationPicturesList { get => _enableIntegrationPicturesList; set => SetValue(ref _enableIntegrationPicturesList, value); }

        public double IntegrationShowPicturesHeight { get; set; } = 150;
        public bool LinkWithSinglePicture { get; set; } = false;
        public bool OpenViewerWithOnSelection { get; set; } = false;
        public bool AddBorder { get; set; } = true;
        public bool AddRoundedCorner { get; set; } = false;

        public bool HideScreenshotsInfos { get; set; } = false;

        public int JpgQuality { get; set; } = 98;

        public bool EnableFolderToSave { get; set; } = false;
        public string FolderToSave { get; set; } = string.Empty;
        public string FileSavePattern { get; set; } = string.Empty;

        /// <summary>
        /// Legacy single global screenshot folder path. Migrated into <see cref="GlobalScreenshotSources"/> on load.
        /// Kept for JSON backward compatibility until all consumers use the list.
        /// </summary>
        public string GlobalScreenshootsPath { get; set; } = string.Empty;

        /// <summary>
        /// Global screenshot folder sources merged into every game's scan configuration.
        /// </summary>
        public List<FolderSettings> GlobalScreenshotSources { get; set; } = new List<FolderSettings>();

        public bool UsedThumbnails { get; set; } = true;

        public List<GameSettings> gameSettings { get; set; } = new List<GameSettings>();


        private bool _carouselAutoChangeEnable = true;
        public bool CarouselAutoChangeEnable { get => _carouselAutoChangeEnable; set => SetValue(ref _carouselAutoChangeEnable, value); }

        private int carouselAutoChangeTimer = 10;
        public int CarouselAutoChangeTimer { get => carouselAutoChangeTimer; set => SetValue(ref carouselAutoChangeTimer, value); }


        private string _ffmpegPath;
        public string FfmpegPath { get => _ffmpegPath; set => SetValue(ref _ffmpegPath, value); }

        private string _ffprobePath;
        public string FfprobePath { get => _ffprobePath; set => SetValue(ref _ffprobePath, value); }

        public bool UseExternalViewer { get; set; } = false;

        #endregion

        #region Settings StartPage

        private SsvCarouselOptions _ssvCarouselOptions = new SsvCarouselOptions();
        public SsvCarouselOptions ssvCarouselOptions { get => _ssvCarouselOptions; set => SetValue(ref _ssvCarouselOptions, value); }
        
        #endregion

        // Playnite serializes settings object to a JSON object and saves it as text file.
        // If you want to exclude some property from being saved then use `JsonDontSerialize` ignore attribute.
        #region Variables exposed

        private List<Screenshot> _listScreenshots = new List<Screenshot>();
        [DontSerialize]
        public List<Screenshot> ListScreenshots { get => _listScreenshots; set => SetValue(ref _listScreenshots, value); }
        
        #endregion

        #region Global screenshot sources

        /// <summary>
        /// Returns global screenshot sources after applying any pending legacy migration.
        /// </summary>
        /// <returns>Migrated global folder settings.</returns>
        public IList<FolderSettings> GetEffectiveGlobalScreenshotSources()
        {
            EnsureGlobalScreenshotSourcesMigrated();
            return GlobalScreenshotSources ?? new List<FolderSettings>();
        }

        /// <summary>
        /// Ensures <see cref="GlobalScreenshotSources"/> is initialized and migrates
        /// <see cref="GlobalScreenshootsPath"/> when present. Safe to call repeatedly.
        /// </summary>
        public void EnsureGlobalScreenshotSourcesMigrated()
        {
            if (GlobalScreenshotSources == null)
            {
                GlobalScreenshotSources = new List<FolderSettings>();
            }

            if (string.IsNullOrEmpty(GlobalScreenshootsPath))
            {
                return;
            }

            bool alreadyMigrated = GlobalScreenshotSources.Exists(x =>
                x != null
                && string.Equals(x.ScreenshotsFolder, GlobalScreenshootsPath, StringComparison.OrdinalIgnoreCase));

            if (!alreadyMigrated)
            {
                GlobalScreenshotSources.Add(new FolderSettings
                {
                    ScreenshotsFolder = GlobalScreenshootsPath
                });
            }
        }

        /// <summary>
        /// Normalizes global screenshot sources before JSON persistence: runs migration,
        /// removes empty entries, and syncs the legacy path field for transitional consumers.
        /// </summary>
        public void NormalizeGlobalScreenshotSourcesForPersistence()
        {
            EnsureGlobalScreenshotSourcesMigrated();

            GlobalScreenshotSources = GlobalScreenshotSources
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.ScreenshotsFolder))
                .ToList();

            SyncLegacyGlobalScreenshootsPath();
        }

        /// <summary>
        /// Keeps <see cref="GlobalScreenshootsPath"/> aligned with a single global source for legacy scan code.
        /// Clears the legacy field when multiple global sources are configured.
        /// </summary>
        private void SyncLegacyGlobalScreenshootsPath()
        {
            if (GlobalScreenshotSources == null || GlobalScreenshotSources.Count == 0)
            {
                GlobalScreenshootsPath = string.Empty;
                return;
            }

            if (GlobalScreenshotSources.Count == 1)
            {
                GlobalScreenshootsPath = GlobalScreenshotSources[0].ScreenshotsFolder ?? string.Empty;
                return;
            }

            GlobalScreenshootsPath = string.Empty;
        }

        #endregion
    }


    public class ScreenshotsVisualizerSettingsViewModel : PluginSettingsViewModel, IPluginSettingsViewModel
    {
        private readonly ScreenshotsVisualizer Plugin;
        private ScreenshotsVisualizerSettings EditingClone { get; set; }

        IPluginSettings IPluginSettingsViewModel.Settings => Settings;

        private ScreenshotsVisualizerSettings settings;
        public ScreenshotsVisualizerSettings Settings { get => settings; set => SetValue(ref settings, value); }

        private SsvGamesConfigurationViewModel _gamesConfiguration;
        /// <summary>
        /// Gets the view model for per-game screenshot folder configuration.
        /// </summary>
        public SsvGamesConfigurationViewModel GamesConfiguration
        {
            get => _gamesConfiguration;
            private set => SetValue(ref _gamesConfiguration, value);
        }

        private SsvConfigurationContextViewModel _configurationContext;
        /// <summary>
        /// Gets the view model for master-detail configuration context and active sources.
        /// </summary>
        public SsvConfigurationContextViewModel ConfigurationContext
        {
            get => _configurationContext;
            private set => SetValue(ref _configurationContext, value);
        }


        public ScreenshotsVisualizerSettingsViewModel(ScreenshotsVisualizer plugin)
        {
            // Injecting your plugin instance is required for Save/Load method because Playnite saves data to a location based on what plugin requested the operation.
            Plugin = plugin;

            // Load saved settings.
            ScreenshotsVisualizerSettings savedSettings = plugin.LoadPluginSettings<ScreenshotsVisualizerSettings>();

            // LoadPluginSettings returns null if not saved data is available.
            Settings = savedSettings ?? new ScreenshotsVisualizerSettings();
            Settings.ApplyFixedLibraryFilterPolicy();
            Settings.EnsureGlobalScreenshotSourcesMigrated();

            GamesConfiguration = new SsvGamesConfigurationViewModel(ScreenshotsVisualizer.PluginName);
            ConfigurationContext = new SsvConfigurationContextViewModel(GamesConfiguration);
            ConfigurationContext.LoadFrom(Settings);
            _ = GamesConfiguration.LoadFromAsync(Settings.gameSettings);

            // Manage source
            _ = Task.Run(() =>
            {
                _ = System.Threading.SpinWait.SpinUntil(() => API.Instance.Database.IsOpen, -1);
                API.Instance.Database.Sources.ForEach(x =>
                {
                    if (!Settings.ssvCarouselOptions.SourcesList.Any(y => y.Name.IsEqual(x.Name)))
                    {
                        Settings.ssvCarouselOptions.SourcesList.Add(new CommonPluginsShared.Models.CheckElement { Name = x.Name });
                    }
                });
                Settings.ssvCarouselOptions.SourcesList = Settings.ssvCarouselOptions.SourcesList.OrderBy(x => x.Name).ToList();
            });
        }

        // Code executed when settings view is opened and user starts editing values.
        public void BeginEdit()
        {
            EditingClone = Serialization.GetClone(Settings);
            GamesConfiguration.ReloadFrom(Settings.gameSettings);
            ConfigurationContext.ReloadFrom(Settings);
            InitializeCommands(ScreenshotsVisualizer.PluginName, ScreenshotsVisualizer.PluginDatabase);
            GamesConfiguration.CaptureCancelSnapshot();
            ConfigurationContext.CaptureCancelSnapshot();
        }

        // Code executed when user decides to cancel any changes made since BeginEdit was called.
        // This method should revert any changes made to Option1 and Option2.
        public void CancelEdit()
        {
            Settings = EditingClone;
            GamesConfiguration.RestoreCancelSnapshot();
            ConfigurationContext.RestoreCancelSnapshot();
        }

        // Code executed when user decides to confirm changes made since BeginEdit was called.
        // This method should save settings made to Option1 and Option2.
        public void EndEdit()
        {
            Settings.ApplyFixedLibraryFilterPolicy();
            ConfigurationContext.ApplyToSettings(Settings);
            Settings.NormalizeGlobalScreenshotSourcesForPersistence();
            Settings.gameSettings = GamesConfiguration.ToGameSettingsList();

            Plugin.SavePluginSettings(Settings);
            ScreenshotsVisualizer.PluginDatabase.PluginSettings = Settings;

            if (API.Instance.ApplicationInfo.Mode == ApplicationMode.Desktop)
            {
                Plugin.TopPanelItem.Visible = Settings.EnableIntegrationButtonHeader;
                Plugin.SidebarItem.Visible = Settings.EnableIntegrationButtonSide;
            }

            this.OnPropertyChanged();
        }

        // Code execute when user decides to confirm changes made since BeginEdit was called.
        // Executed before EndEdit is called and EndEdit is not called if false is returned.
        // List of errors is presented to user if verification fails.
        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            return true;
        }

        public RelayCommand<object> BrowseSelectFfmpegCommand
        {
            get => new RelayCommand<object>((a) =>
            {
                var filePath = API.Instance.Dialogs.SelectFile("ffmpeg|ffmpeg.exe");
                if (!filePath.IsNullOrEmpty())
                {
                    Settings.FfmpegPath = filePath;
                }
            });
        }

        public RelayCommand<object> BrowseSelectFfprobeCommand
        {
            get => new RelayCommand<object>((a) =>
            {
                var filePath = API.Instance.Dialogs.SelectFile("ffProbe|ffProbe.exe");
                if (!filePath.IsNullOrEmpty())
                {
                    Settings.FfprobePath = filePath;
                }
            });
        }

        /// <summary>
        /// Gets the command that opens a folder picker for the unique save folder path.
        /// </summary>
        public RelayCommand<object> BrowseFolderToSaveCommand
        {
            get => new RelayCommand<object>((a) =>
            {
                string selectedFolder = API.Instance.Dialogs.SelectFolder();
                if (!selectedFolder.IsNullOrEmpty())
                {
                    Settings.FolderToSave = selectedFolder;
                }
            });
        }
    }
}