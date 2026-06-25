using Playnite.SDK;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.Services;
using ScreenshotsVisualizer.ViewModels.Settings;
using Playnite.SDK.Data;
using System.Collections.Generic;
using ScreenshotsVisualizer.Models.StartPage;
using System.Linq;
using CommonPluginsShared.Extensions;
using System.Threading.Tasks;
using System;
using System.IO;
using CommonPlayniteShared;
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

            if (CustomConversionCmds == null)
            {
                CustomConversionCmds = new List<SsvImageConversionCustomCmd>();
            }
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

        private string _imageMagickPath;
        /// <summary>
        /// Gets or sets the absolute path to the ImageMagick executable (<c>magick.exe</c> or <c>convert.exe</c>).
        /// </summary>
        public string ImageMagickPath { get => _imageMagickPath; set => SetValue(ref _imageMagickPath, value); }

        /// <summary>
        /// Gets or sets the list of ImageMagick conversion profiles exposed in game and main menus.
        /// </summary>
        public List<SsvImageConversionCustomCmd> CustomConversionCmds { get; set; } = new List<SsvImageConversionCustomCmd>();

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

        /// <summary>
        /// Scans persisted per-game settings for archive folder entries that strictly duplicate
        /// <see cref="FolderToSave"/> and <see cref="FileSavePattern"/>.
        /// </summary>
        /// <returns>Duplicate analysis summary for migration planning.</returns>
        public SsvArchiveDuplicateAnalysis AnalyzePersistedArchiveDuplicates()
        {
            return SsvArchiveFolderHelper.AnalyzePersistedArchiveDuplicates(this);
        }

        /// <summary>
        /// Removes persisted archive duplicates that strictly match the global archive configuration.
        /// </summary>
        /// <returns>Total number of removed duplicates.</returns>
        public int RemovePersistedArchiveDuplicates()
        {
            return SsvArchiveFolderHelper.RemovePersistedArchiveDuplicates(this);
        }

        /// <summary>
        /// Creates a ZIP backup of <c>config.json</c> in the plugin user data folder before migration.
        /// </summary>
        /// <param name="pluginUserDataPath">Plugin user data path.</param>
        /// <param name="pluginName">Plugin display name for the archive file name.</param>
        /// <returns>Backup operation result.</returns>
        public SsvArchiveSettingsBackupResult CreateSettingsBackupZip(string pluginUserDataPath, string pluginName)
        {
            return SsvArchiveSettingsMigration.TryCreateSettingsBackupZip(pluginUserDataPath, pluginName);
        }

        #endregion

        #region Image conversion custom commands

        private const int DefaultJpgQuality = 98;

        /// <summary>
        /// Returns custom conversion commands after applying any pending legacy migration.
        /// </summary>
        /// <returns>Migrated conversion profiles.</returns>
        public IList<SsvImageConversionCustomCmd> GetEffectiveCustomConversionCmds()
        {
            EnsureCustomConversionCmdsMigrated();
            return CustomConversionCmds ?? new List<SsvImageConversionCustomCmd>();
        }

        /// <summary>
        /// Ensures <see cref="CustomConversionCmds"/> is initialized and seeds a default JPEG profile
        /// when the list is empty. Safe to call repeatedly.
        /// </summary>
        /// <param name="legacyJpgQuality">Legacy quality from <c>config.json</c> when upgrading existing settings.</param>
        public void EnsureCustomConversionCmdsMigrated(int legacyJpgQuality = DefaultJpgQuality)
        {
            if (CustomConversionCmds == null)
            {
                CustomConversionCmds = new List<SsvImageConversionCustomCmd>();
            }

            if (CustomConversionCmds.Count > 0)
            {
                return;
            }

            CustomConversionCmds.Add(SsvImageConversionCustomCmd.CreateDefaultJpgProfile(ClampJpgQuality(legacyJpgQuality)));
        }

        /// <summary>
        /// Applies image conversion migration using the legacy <c>JpgQuality</c> field from persisted settings.
        /// </summary>
        /// <param name="pluginUserDataPath">Plugin user data folder containing <c>config.json</c>.</param>
        public void ApplyImageConversionMigrationFromConfig(string pluginUserDataPath)
        {
            if (CustomConversionCmds != null && CustomConversionCmds.Count > 0)
            {
                return;
            }

            EnsureCustomConversionCmdsMigrated(TryReadLegacyJpgQualityFromConfig(pluginUserDataPath));
        }

        /// <summary>
        /// Normalizes custom conversion commands before JSON persistence.
        /// </summary>
        public void NormalizeCustomConversionCmdsForPersistence()
        {
            EnsureCustomConversionCmdsMigrated();

            CustomConversionCmds = CustomConversionCmds
                .Where(x => x != null)
                .ToList();

            foreach (SsvImageConversionCustomCmd cmd in CustomConversionCmds)
            {
                if (cmd.Id == Guid.Empty)
                {
                    cmd.Id = Guid.NewGuid();
                }

                if (string.IsNullOrWhiteSpace(cmd.Name))
                {
                    int quality = cmd.Quality ?? DefaultJpgQuality;
                    cmd.Name = string.Format("JPG (quality {0})", quality);
                }

                cmd.DeleteOriginal = true;
            }
        }

        /// <summary>
        /// Reads the legacy <c>JpgQuality</c> value from persisted plugin settings.
        /// </summary>
        /// <param name="pluginUserDataPath">Plugin user data folder.</param>
        /// <returns>Clamped legacy quality, or <see cref="DefaultJpgQuality"/> when absent.</returns>
        public static int TryReadLegacyJpgQualityFromConfig(string pluginUserDataPath)
        {
            if (string.IsNullOrEmpty(pluginUserDataPath))
            {
                return DefaultJpgQuality;
            }

            string configPath = Path.Combine(pluginUserDataPath, PlaynitePaths.ConfigFileName);
            if (!File.Exists(configPath))
            {
                return DefaultJpgQuality;
            }

            try
            {
                LegacyJpgQualitySnapshot snapshot = Serialization.FromJsonFile<LegacyJpgQualitySnapshot>(configPath);
                if (snapshot != null)
                {
                    return ClampJpgQuality(snapshot.JpgQuality);
                }
            }
            catch (Exception)
            {
            }

            return DefaultJpgQuality;
        }

        private static int ClampJpgQuality(int quality)
        {
            if (quality < 1)
            {
                return 1;
            }

            if (quality > 100)
            {
                return 100;
            }

            return quality;
        }

        private sealed class LegacyJpgQualitySnapshot
        {
            public int JpgQuality { get; set; } = DefaultJpgQuality;
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

        private SsvImageConversionSettingsViewModel _imageConversionSettings;
        /// <summary>
        /// Gets the view model for ImageMagick path and conversion profile list management.
        /// </summary>
        public SsvImageConversionSettingsViewModel ImageConversionSettings
        {
            get => _imageConversionSettings;
            private set => SetValue(ref _imageConversionSettings, value);
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
            Settings.ApplyImageConversionMigrationFromConfig(plugin.GetPluginUserDataPath());

            GamesConfiguration = new SsvGamesConfigurationViewModel(ScreenshotsVisualizer.PluginName);
            ConfigurationContext = new SsvConfigurationContextViewModel(GamesConfiguration);
            ConfigurationContext.LoadFrom(Settings);
            ImageConversionSettings = new SsvImageConversionSettingsViewModel();
            ImageConversionSettings.LoadFrom(Settings);
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
            Settings.ApplyImageConversionMigrationFromConfig(Plugin.GetPluginUserDataPath());
            GamesConfiguration.ReloadFrom(Settings.gameSettings);
            ConfigurationContext.ReloadFrom(Settings);
            ImageConversionSettings.LoadFrom(Settings);
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
            ImageConversionSettings.LoadFrom(Settings);
        }

        // Code executed when user decides to confirm changes made since BeginEdit was called.
        // This method should save settings made to Option1 and Option2.
        public void EndEdit()
        {
            Settings.ApplyFixedLibraryFilterPolicy();
            ConfigurationContext.ApplyToSettings(Settings);
            ImageConversionSettings.ApplyToSettings(Settings);
            Settings.NormalizeGlobalScreenshotSourcesForPersistence();
            Settings.NormalizeCustomConversionCmdsForPersistence();
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

        public RelayCommand<object> BrowseSelectImageMagickCommand
        {
            get => new RelayCommand<object>((a) =>
            {
                var filePath = API.Instance.Dialogs.SelectFile("ImageMagick|magick.exe;convert.exe");
                if (!filePath.IsNullOrEmpty())
                {
                    Settings.ImageMagickPath = filePath;
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