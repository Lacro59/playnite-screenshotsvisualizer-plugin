using Playnite.SDK;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.Views;
using Playnite.SDK.Data;
using System.Collections.Generic;
using ScreenshotsVisualizer.Models.StartPage;
using System.Linq;
using CommonPluginsShared.Extensions;
using System.Threading.Tasks;

namespace ScreenshotsVisualizer
{
    public class ScreenshotsVisualizerSettings : ObservableObject
    {
        #region Settings variables
        public bool MenuInExtensions { get; set; } = true;

        public bool EnableTag { get; set; } = false;


        public bool EnableIntegrationButtonHeader { get; set; } = false;
        public bool EnableIntegrationButtonSide { get; set; } = true;

        private bool _EnableIntegrationViewItem = true;
        public bool EnableIntegrationViewItem { get => _EnableIntegrationViewItem; set => SetValue(ref _EnableIntegrationViewItem, value); }

        private bool _EnableIntegrationButton = true;
        public bool EnableIntegrationButton { get => _EnableIntegrationButton; set => SetValue(ref _EnableIntegrationButton, value); }

        private bool _EnableIntegrationButtonDetails = false;
        public bool EnableIntegrationButtonDetails { get => _EnableIntegrationButtonDetails; set => SetValue(ref _EnableIntegrationButtonDetails, value); }

        private bool _EnableIntegrationShowSinglePicture = true;
        public bool EnableIntegrationShowSinglePicture { get => _EnableIntegrationShowSinglePicture; set => SetValue(ref _EnableIntegrationShowSinglePicture, value); }

        public double IntegrationShowSinglePictureHeight { get; set; } = 150;
        public bool OpenViewerWithOnSelectionSinglePicture { get; set; } = false;
        public bool AddBorderSinglePicture { get; set; } = true;
        public bool AddRoundedCornerSinglePicture { get; set; } = false;

        private bool _EnableIntegrationShowPictures = true;
        public bool EnableIntegrationShowPictures { get => _EnableIntegrationShowPictures; set => SetValue(ref _EnableIntegrationShowPictures, value); }

        private bool _EnableIntegrationShowPicturesVertical = true;
        public bool EnableIntegrationShowPicturesVertical { get => _EnableIntegrationShowPicturesVertical; set => SetValue(ref _EnableIntegrationShowPicturesVertical, value); }

        private bool _EnableIntegrationPicturesList = true;
        public bool EnableIntegrationPicturesList { get => _EnableIntegrationPicturesList; set => SetValue(ref _EnableIntegrationPicturesList, value); }

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


        private bool _CarouselAutoChangeEnable = true;
        public bool CarouselAutoChangeEnable { get => _CarouselAutoChangeEnable; set => SetValue(ref _CarouselAutoChangeEnable, value); }

        private int _CarouselAutoChangeTimer = 10;
        public int CarouselAutoChangeTimer { get => _CarouselAutoChangeTimer; set => SetValue(ref _CarouselAutoChangeTimer, value); }
        #endregion


        #region Settings StartPage
        private SsvCarouselOptions _ssvCarouselOptions { get; set; } = new SsvCarouselOptions();
        public SsvCarouselOptions ssvCarouselOptions
        {
            get => _ssvCarouselOptions;
            set
            {
                _ssvCarouselOptions = value;
                OnPropertyChanged();
            }
        }
        #endregion


        // Playnite serializes settings object to a JSON object and saves it as text file.
        // If you want to exclude some property from being saved then use `JsonDontSerialize` ignore attribute.
        #region Variables exposed
        private bool _HasData = false;
        [DontSerialize]
        public bool HasData { get => _HasData; set => SetValue(ref _HasData, value); }

        private List<Screenshot> _ListScreenshots = new List<Screenshot>();
        [DontSerialize]
        public List<Screenshot> ListScreenshots { get => _ListScreenshots; set => SetValue(ref _ListScreenshots, value); }
        #endregion  
    }


    public class ScreenshotsVisualizerSettingsViewModel : ObservableObject, ISettings
    {
        private readonly ScreenshotsVisualizer Plugin;
        private ScreenshotsVisualizerSettings EditingClone { get; set; }

        private ScreenshotsVisualizerSettings _Settings;
        public ScreenshotsVisualizerSettings Settings { get => _Settings; set => SetValue(ref _Settings, value); }


        public ScreenshotsVisualizerSettingsViewModel(ScreenshotsVisualizer plugin)
        {
            // Injecting your plugin instance is required for Save/Load method because Playnite saves data to a location based on what plugin requested the operation.
            Plugin = plugin;

            // Load saved settings.
            ScreenshotsVisualizerSettings savedSettings = plugin.LoadPluginSettings<ScreenshotsVisualizerSettings>();

            // LoadPluginSettings returns null if not saved data is available.
            Settings = savedSettings ?? new ScreenshotsVisualizerSettings();

            // Manage source
            Task.Run(() => 
            {
                System.Threading.SpinWait.SpinUntil(() => API.Instance.Database.IsOpen, -1);
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
            foreach (ListGameScreenshot item in ScreenshotsVisualizerSettingsView.listGameScreenshots)
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
                Plugin.topPanelItem.Visible = Settings.EnableIntegrationButtonHeader;
                Plugin.ssvViewSidebar.Visible = Settings.EnableIntegrationButtonSide;
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
    }
}
