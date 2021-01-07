using CommonPluginsShared;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using ScreenshotsVisualizer.Services;
using ScreenshotsVisualizer.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ScreenshotsVisualizer
{
    public class ScreenshotsVisualizer : Plugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private static IResourceProvider resources = new ResourceProvider();

        private ScreenshotsVisualizerSettings settings { get; set; }

        public override Guid Id { get; } = Guid.Parse("c6c8276f-91bf-48e5-a1d1-4bee0b493488");

        private readonly TaskHelper taskHelper = new TaskHelper();

        public static string pluginFolder;
        public static ScreenshotsVisualizerDatabase PluginDatabase;
        public static Game GameSelected { get; set; }
        public static ScreenshotsVisualizerUI screenshotsVisualizerUI { get; set; }


        public ScreenshotsVisualizer(IPlayniteAPI api) : base(api)
        {
            settings = new ScreenshotsVisualizerSettings(this);

            // Loading plugin database 
            PluginDatabase = new ScreenshotsVisualizerDatabase(PlayniteApi, settings, this.GetPluginUserDataPath());
            PluginDatabase.InitializeDatabase();

            // Get plugin's location 
            pluginFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            // Add plugin localization in application ressource.
            PluginLocalization.SetPluginLanguage(pluginFolder, api.ApplicationSettings.Language);
            // Add common in application ressource.
            Common.Load(pluginFolder);
            Common.SetEvent(PlayniteApi);

            // Check version
            if (settings.EnableCheckVersion)
            {
                CheckVersion cv = new CheckVersion();

                if (cv.Check("ScreenshotsVisualizer", pluginFolder))
                {
                    cv.ShowNotification(api, "ScreenshotsVisualizer - " + resources.GetString("LOCUpdaterWindowTitle"));
                }
            }

            // Init ui interagration
            screenshotsVisualizerUI = new ScreenshotsVisualizerUI(api, settings, this.GetPluginUserDataPath());

            // Custom theme button
            if (settings.EnableIntegrationInCustomTheme)
            {
                EventManager.RegisterClassHandler(typeof(Button), Button.ClickEvent, new RoutedEventHandler(screenshotsVisualizerUI.OnCustomThemeButtonClick));
            }

            // Add event fullScreen
            if (api.ApplicationInfo.Mode == ApplicationMode.Fullscreen)
            {
                //EventManager.RegisterClassHandler(typeof(Button), Button.ClickEvent, new RoutedEventHandler(BtFullScreen_ClickEvent));
            }
        }


        // To add new game menu items override GetGameMenuItems
        public override List<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            var GameMenu = args.Games.First();

            List<GameMenuItem> gameMenuItems = new List<GameMenuItem>();

            gameMenuItems.Add(new GameMenuItem
            {
                // Delete & download localizations data for the selected game
                MenuSection = resources.GetString("LOCSsv"),
                Description = resources.GetString("LOCSsvViewScreenshots"),
                Action = (gameMenuItem) =>
                {
                    var ViewExtension = new SsvScreenshotsView(PlayniteApi, ScreenshotsVisualizer.GameSelected);
                    Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PlayniteApi, resources.GetString("LOCSsvTitle"), ViewExtension);
                    windowExtension.ShowDialog();

                    if (settings.EnableIntegrationInCustomTheme || settings.EnableIntegrationInDescription)
                    {
                        var TaskIntegrationUI = Task.Run(() =>
                        {
                            screenshotsVisualizerUI.Initial();
                            screenshotsVisualizerUI.taskHelper.Check();
                            var dispatcherOp = screenshotsVisualizerUI.AddElements();
                            if (dispatcherOp != null)
                            {
                                dispatcherOp.Completed += (s, e) => { screenshotsVisualizerUI.RefreshElements(GameSelected); };
                            }
                        });
                    }
                }
            });

            gameMenuItems.Add(new GameMenuItem
            {
                // Refresh data
                MenuSection = resources.GetString("LOCSsv"),
                Description = resources.GetString("LOCCommonRefreshGameData"),
                Action = (gameMenuItem) =>
                {
                    PluginDatabase.RefreshData(GameMenu);

                    screenshotsVisualizerUI.Initial();
                    screenshotsVisualizerUI.taskHelper.Check();
                    var dispatcherOp = screenshotsVisualizerUI.AddElements();
                    if (dispatcherOp != null)
                    {
                        dispatcherOp.Completed += (s, e) => { screenshotsVisualizerUI.RefreshElements(GameMenu); };
                    }
                }
            });

#if DEBUG
            gameMenuItems.Add(new GameMenuItem
            {
                MenuSection = resources.GetString("LOCSsv"),
                Description = "Test",
                Action = (mainMenuItem) => { }
            });
#endif

            return gameMenuItems;
        }

        // To add new main menu items override GetMainMenuItems
        public override List<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            string MenuInExtensions = string.Empty;
            if (settings.MenuInExtensions)
            {
                MenuInExtensions = "@";
            }

            List<MainMenuItem> mainMenuItems = new List<MainMenuItem>();
            
            mainMenuItems.Add(new MainMenuItem
            {
                MenuSection = MenuInExtensions + resources.GetString("LOCSsv"),
                Description = resources.GetString("LOCCommonAddAllTags"),
                Action = (mainMenuItem) =>
                {
                    PluginDatabase.AddTagAllGame();
                }
            });
            mainMenuItems.Add(new MainMenuItem
            {
                MenuSection = MenuInExtensions + resources.GetString("LOCSsv"),
                Description = resources.GetString("LOCCommonRemoveAllTags"),
                Action = (mainMenuItem) =>
                {
                    PluginDatabase.RemoveTagAllGame();
                }
            });

#if DEBUG
            mainMenuItems.Add(new MainMenuItem
            {
                MenuSection = MenuInExtensions + resources.GetString("LOCSsv"),
                Description = "Test",
                Action = (mainMenuItem) => { }
            });
#endif

            return mainMenuItems;
        }


        public override void OnGameSelected(GameSelectionEventArgs args)
        {
            try
            {
                if (args.NewValue != null && args.NewValue.Count == 1)
                {
                    GameSelected = args.NewValue[0];
#if DEBUG
                    logger.Debug($"ScreenshotsVisualizer - OnGameSelected() - {GameSelected.Name} - {GameSelected.Id.ToString()}");
#endif
                    if (settings.EnableIntegrationInCustomTheme || settings.EnableIntegrationInDescription)
                    {
                        var TaskIntegrationUI = Task.Run(() =>
                        {
                            System.Threading.SpinWait.SpinUntil(() => PluginDatabase.IsLoaded, -1);

                            screenshotsVisualizerUI.Initial();
                            screenshotsVisualizerUI.taskHelper.Check();
                            var dispatcherOp = screenshotsVisualizerUI.AddElements();
                            if (dispatcherOp != null)
                            {
                                dispatcherOp.Completed += (s, e) => { screenshotsVisualizerUI.RefreshElements(args.NewValue[0]); };
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "SuccessStory", $"Error on OnGameSelected()");
            }
        }

        // Add code to be executed when game is finished installing.
        public override void OnGameInstalled(Game game)
        {

        }

        // Add code to be executed when game is started running.
        public override void OnGameStarted(Game game)
        {

        }

        // Add code to be executed when game is preparing to be started.
        public override void OnGameStarting(Game game)
        {

        }

        // Add code to be executed when game is preparing to be started.
        public override void OnGameStopped(Game game, long elapsedSeconds)
        {
            var TaskIntegrationUI = Task.Run(() =>
            {
                PluginDatabase.RefreshData(game);

                screenshotsVisualizerUI.Initial();
                screenshotsVisualizerUI.taskHelper.Check();
                var dispatcherOp = screenshotsVisualizerUI.AddElements();
                if (dispatcherOp != null)
                {
                    dispatcherOp.Completed += (s, e) => { screenshotsVisualizerUI.RefreshElements(GameSelected); };
                }
            });
        }

        // Add code to be executed when game is uninstalled.
        public override void OnGameUninstalled(Game game)
        {

        }


        // Add code to be executed when Playnite is initialized.
        public override void OnApplicationStarted()
        {

        }

        // Add code to be executed when Playnite is shutting down.
        public override void OnApplicationStopped()
        {

        }


        // Add code to be executed when library is updated.
        public override void OnLibraryUpdated()
        {

        }


        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new ScreenshotsVisualizerSettingsView(PlayniteApi, this.GetPluginUserDataPath());
        }
    }
}
