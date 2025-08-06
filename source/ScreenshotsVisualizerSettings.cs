using Playnite.SDK;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.Views;
using Playnite.SDK.Data;
using System.Collections.Generic;
using ScreenshotsVisualizer.Models.StartPage;
using System.Linq;
using CommonPluginsShared.Extensions;
using System.Threading.Tasks;
using System;
using CommonPluginsShared.Plugins;

namespace ScreenshotsVisualizer
{
    public class ScreenshotsVisualizerSettings : PluginSettings
    {
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

        public string GlobalScreenshootsPath { get; set; } = string.Empty;

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
    }


    public class ScreenshotsVisualizerSettingsViewModel : ObservableObject, ISettings
    {
        private readonly ScreenshotsVisualizer Plugin;
        private ScreenshotsVisualizerSettings EditingClone { get; set; }

        private ScreenshotsVisualizerSettings settings;
        public ScreenshotsVisualizerSettings Settings { get => settings; set => SetValue(ref settings, value); }


        public ScreenshotsVisualizerSettingsViewModel(ScreenshotsVisualizer plugin)
        {
            // Injecting your plugin instance is required for Save/Load method because Playnite saves data to a location based on what plugin requested the operation.
            Plugin = plugin;

            // Load saved settings.
            ScreenshotsVisualizerSettings savedSettings = plugin.LoadPluginSettings<ScreenshotsVisualizerSettings>();

            // LoadPluginSettings returns null if not saved data is available.
            Settings = savedSettings ?? new ScreenshotsVisualizerSettings();

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
        }

        // Code executed when user decides to cancel any changes made since BeginEdit was called.
        // This method should revert any changes made to Option1 and Option2.
        public void CancelEdit()
        {
            Settings = EditingClone;
        }

        // Code executed when user decides to confirm changes made since BeginEdit was called.
        // This method should save settings made to Option1 and Option2.
        public void EndEdit()
        {
            Settings.gameSettings = new List<GameSettings>();
            foreach (ListGameScreenshot item in ScreenshotsVisualizerSettingsView.ListGameScreenshots)
            {
                Settings.gameSettings.Add(new GameSettings
                {
                    Id = item.Id,
                    ScreenshotsFolders = item.ScreenshotsFolders,
                    UsedFilePattern = item.UsedFilePattern,
                    FilePattern = item.FilePattern,
                    ScanSubFolders = item.ScanSubFolders
                });
            }

            Plugin.SavePluginSettings(Settings);
            ScreenshotsVisualizer.PluginDatabase.PluginSettings = this;

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
    }
}