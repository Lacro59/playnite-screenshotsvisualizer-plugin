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

        public bool EnableCheckVersion { get; set; } = true;
        public bool MenuInExtensions { get; set; } = true;

        public bool EnableIntegrationButton { get; set; } = false;
        public bool EnableIntegrationInDescription { get; set; } = true;

        public bool EnableIntegrationInDescriptionOnlyIcon { get; set; } = false;
        public bool EnableIntegrationButtonDetails { get; set; } = false;
        public bool EnableIntegrationInDescriptionWithToggle { get; set; } = false;

        public bool IntegrationShowTitle { get; set; } = false;
        public bool IntegrationTopGameDetails { get; set; } = false;
        public bool IntegrationShowSinglePicture { get; set; } = false;
        public bool IntegrationShowPictures { get; set; } = false;

        public double IntegrationShowSinglePictureHeight { get; set; } = 150;

        public double IntegrationShowPicturesHeight { get; set; } = 150;

        public bool EnableIntegrationInCustomTheme { get; set; } = false;

        public bool EnableIntegrationFS { get; set; } = false;

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

                EnableIntegrationButton = savedSettings.EnableIntegrationButton;
                EnableIntegrationInDescription = savedSettings.EnableIntegrationInDescription;

                EnableIntegrationInDescriptionOnlyIcon = savedSettings.EnableIntegrationInDescriptionOnlyIcon;
                EnableIntegrationButtonDetails = savedSettings.EnableIntegrationButtonDetails;

                IntegrationShowTitle = savedSettings.IntegrationShowTitle;
                IntegrationTopGameDetails = savedSettings.IntegrationTopGameDetails;
                IntegrationShowSinglePicture = savedSettings.IntegrationShowSinglePicture;
                IntegrationShowPictures = savedSettings.IntegrationShowPictures;

                IntegrationShowSinglePictureHeight = savedSettings.IntegrationShowSinglePictureHeight;

                IntegrationShowPicturesHeight = savedSettings.IntegrationShowPicturesHeight;

                EnableIntegrationInCustomTheme = savedSettings.EnableIntegrationInCustomTheme;

                EnableIntegrationFS = savedSettings.EnableIntegrationFS;

                gameSettings = savedSettings.gameSettings;
            }
        }

        public void BeginEdit()
        {
            // Code executed when settings view is opened and user starts editing values.
        }

        public void CancelEdit()
        {
            // Code executed when user decides to cancel any changes made since BeginEdit was called.
            // This method should revert any changes made to Option1 and Option2.
        }

        public void EndEdit()
        {
            // Code executed when user decides to confirm changes made since BeginEdit was called.
            // This method should save settings made to Option1 and Option2.

            gameSettings = new List<GameSettings>();
            foreach (var item in ScreenshotsVisualizerSettingsView.listGameScreenshots)
            {
                gameSettings.Add(new GameSettings
                {
                    Id = item.Id,
                    ScreenshotsFolder = item.ScreenshotsFolder
                });
            }

            plugin.SavePluginSettings(this);

            ScreenshotsVisualizer.screenshotsVisualizerUI.RemoveElements();
            var TaskIntegrationUI = Task.Run(() =>
            {
                var dispatcherOp = ScreenshotsVisualizer.screenshotsVisualizerUI.AddElements();
                dispatcherOp.Completed += (s, e) => { ScreenshotsVisualizer.screenshotsVisualizerUI.RefreshElements(ScreenshotsVisualizer.GameSelected); };
            });

            ScreenshotsVisualizer.PluginDatabase.IsLoaded = false;
            ScreenshotsVisualizer.PluginDatabase.InitializeDatabase();
        }

        public bool VerifySettings(out List<string> errors)
        {
            // Code execute when user decides to confirm changes made since BeginEdit was called.
            // Executed before EndEdit is called and EndEdit is not called if false is returned.
            // List of errors is presented to user if verification fails.
            errors = new List<string>();
            return true;
        }
    }
}