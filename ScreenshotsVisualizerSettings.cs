using Newtonsoft.Json;
using Playnite.SDK;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenshotsVisualizer
{
    public class ScreenshotsVisualizerSettings : ISettings
    {
        private readonly ScreenshotsVisualizer plugin;
        private ScreenshotsVisualizerSettings editingClone;

        public bool EnableCheckVersion { get; set; } = true;
        public bool MenuInExtensions { get; set; } = true;

        public bool EnableTag { get; set; } = false;

        public bool EnableIntegrationButton { get; set; } = false;
        public bool EnableIntegrationInDescription { get; set; } = true;

        public bool EnableIntegrationInDescriptionOnlyIcon { get; set; } = false;
        public bool EnableIntegrationButtonDetails { get; set; } = false;
        public bool EnableIntegrationInDescriptionWithToggle { get; set; } = false;

        public bool IntegrationShowTitle { get; set; } = false;
        public bool IntegrationTopGameDetails { get; set; } = false;
        public bool IntegrationShowSinglePicture { get; set; } = false;
        public bool IntegrationShowPictures { get; set; } = false;
        public bool IntegrationShowPicturesVertical { get; set; } = false;

        public bool OpenViewerWithOnSelection { get; set; } = false;
        public bool LinkWithSinglePicture { get; set; } = false;

        public double IntegrationShowSinglePictureHeight { get; set; } = 150;

        public double IntegrationShowPicturesHeight { get; set; } = 150;

        public bool EnableIntegrationInCustomTheme { get; set; } = false;

        public bool EnableIntegrationFS { get; set; } = false;

        public bool AddBorder { get; set; } = true;
        public bool AddRoundedCorner { get; set; } = false;

        public List<GameSettings> gameSettings { get; set; } = new List<GameSettings>();


        // Playnite serializes settings object to a JSON object and saves it as text file.
        // If you want to exclude some property from being saved then use `JsonIgnore` ignore attribute.
        [JsonIgnore]
        public bool OptionThatWontBeSaved { get; set; } = false;

        // Parameterless constructor must exist if you want to use LoadPluginSettings method.
        public ScreenshotsVisualizerSettings()
        {
        }

        public ScreenshotsVisualizerSettings(ScreenshotsVisualizer plugin)
        {
            // Injecting your plugin instance is required for Save/Load method because Playnite saves data to a location based on what plugin requested the operation.
            this.plugin = plugin;

            // Load saved settings.
            var savedSettings = plugin.LoadPluginSettings<ScreenshotsVisualizerSettings>();

            // LoadPluginSettings returns null if not saved data is available.
            if (savedSettings != null)
            {
                EnableCheckVersion = savedSettings.EnableCheckVersion;
                MenuInExtensions = savedSettings.MenuInExtensions;

                EnableTag = savedSettings.EnableTag;

                EnableIntegrationButton = savedSettings.EnableIntegrationButton;
                EnableIntegrationInDescription = savedSettings.EnableIntegrationInDescription;

                EnableIntegrationInDescriptionOnlyIcon = savedSettings.EnableIntegrationInDescriptionOnlyIcon;
                EnableIntegrationButtonDetails = savedSettings.EnableIntegrationButtonDetails;

                IntegrationShowTitle = savedSettings.IntegrationShowTitle;
                IntegrationTopGameDetails = savedSettings.IntegrationTopGameDetails;
                IntegrationShowSinglePicture = savedSettings.IntegrationShowSinglePicture;
                IntegrationShowPictures = savedSettings.IntegrationShowPictures;
                IntegrationShowPicturesVertical = savedSettings.IntegrationShowPicturesVertical;

                OpenViewerWithOnSelection = savedSettings.OpenViewerWithOnSelection;
                LinkWithSinglePicture = savedSettings.LinkWithSinglePicture;

                IntegrationShowSinglePictureHeight = savedSettings.IntegrationShowSinglePictureHeight;

                IntegrationShowPicturesHeight = savedSettings.IntegrationShowPicturesHeight;

                EnableIntegrationInCustomTheme = savedSettings.EnableIntegrationInCustomTheme;

                EnableIntegrationFS = savedSettings.EnableIntegrationFS;

                gameSettings = savedSettings.gameSettings;

                AddBorder = savedSettings.AddBorder;
                AddRoundedCorner = savedSettings.AddRoundedCorner;
            }
        }

        // Code executed when settings view is opened and user starts editing values.
        public void BeginEdit()
        {
            editingClone = this.GetClone();
        }

        // Code executed when user decides to cancel any changes made since BeginEdit was called.
        // This method should revert any changes made to Option1 and Option2.
        public void CancelEdit()
        {
            LoadValues(editingClone);
        }

        private void LoadValues(ScreenshotsVisualizerSettings source)
        {
            source.CopyProperties(this, false, null, true);
        }

        // Code executed when user decides to confirm changes made since BeginEdit was called.
        // This method should save settings made to Option1 and Option2.
        public void EndEdit()
        {
            gameSettings = new List<GameSettings>();
            foreach (var item in ScreenshotsVisualizerSettingsView.listGameScreenshots)
            {
                gameSettings.Add(new GameSettings
                {
                    Id = item.Id,
                    ScreenshotsFolder = item.ScreenshotsFolder,
                    UsedFilePattern = item.UsedFilePattern,
                    FilePattern = item.FilePattern
                });
            }

            plugin.SavePluginSettings(this);

            ScreenshotsVisualizer.screenshotsVisualizerUI.RemoveElements();

            var TaskIntegrationUI = Task.Run(() =>
            {
                ScreenshotsVisualizer.PluginDatabase.IsLoaded = false;
                ScreenshotsVisualizer.PluginDatabase.InitializeDatabase();

                System.Threading.SpinWait.SpinUntil(() => ScreenshotsVisualizer.PluginDatabase.IsLoaded, -1);

                var dispatcherOp = ScreenshotsVisualizer.screenshotsVisualizerUI.AddElements();
                dispatcherOp.Completed += (s, e) => { ScreenshotsVisualizer.screenshotsVisualizerUI.RefreshElements(ScreenshotsVisualizer.GameSelected); };
            });
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