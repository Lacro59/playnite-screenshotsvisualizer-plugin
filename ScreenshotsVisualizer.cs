﻿using CommonPluginsShared;
using CommonPluginsShared.PlayniteExtended;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using ScreenshotsVisualizer.Controls;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.Services;
using ScreenshotsVisualizer.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ScreenshotsVisualizer
{
    public class ScreenshotsVisualizer : PluginExtended<ScreenshotsVisualizerSettingsViewModel, ScreenshotsVisualizerDatabase>
    {
        public override Guid Id { get; } = Guid.Parse("c6c8276f-91bf-48e5-a1d1-4bee0b493488");

        private readonly TaskHelper taskHelper = new TaskHelper();


        public ScreenshotsVisualizer(IPlayniteAPI api) : base(api)
        {
            // Custom theme button
            EventManager.RegisterClassHandler(typeof(Button), Button.ClickEvent, new RoutedEventHandler(OnCustomThemeButtonClick));

            // Custom elements integration
            AddCustomElementSupport(new AddCustomElementSupportArgs
            {
                ElementList = new List<string> { "SsvButton", "SsvSinglePicture", "SsvListScreenshots", "SsvViewItem" },
                SourceName = "ScreenshotsVisualizer",
                SettingsRoot = $"{nameof(PluginSettings)}.{nameof(PluginSettings.Settings)}"
            });
        }


        #region Custom event
        public void OnCustomThemeButtonClick(object sender, RoutedEventArgs e)
        {
            string ButtonName = string.Empty;
            try
            {
                ButtonName = ((Button)sender).Name;
                if (ButtonName == "PART_SsvCustomButton")
                {
                    Common.LogDebug(true, $"OnCustomThemeButtonClick()");

                    var ViewExtension = new SsvScreenshotsView(PlayniteApi, PluginDatabase.GameContext);
                    Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PlayniteApi, resources.GetString("LOCSsvTitle"), ViewExtension);
                    windowExtension.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false);
            }
        }
        #endregion


        #region Theme integration

        // List custom controls
        public override Control GetGameViewControl(GetGameViewControlArgs args)
        {
            if (args.Name == "SsvButton")
            {
                return new SsvButton();
            }

            if (args.Name == "SsvSinglePicture")
            {
                return new SsvSinglePicture();
            }

            if (args.Name == "SsvListScreenshots")
            {
                return new SsvListScreenshots();
            }

            if (args.Name == "SsvViewItem")
            {
                return new SsvViewItem();
            }

            return null;
        }
        #endregion


        #region Menus
        // To add new game menu items override GetGameMenuItems
        public override List<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            Game GameMenu = args.Games.First();
            GameScreenshots gameScreenshots = PluginDatabase.Get(GameMenu);

            List<GameMenuItem> gameMenuItems = new List<GameMenuItem>();

            if (gameScreenshots.HasData)
            {
                gameMenuItems.Add(new GameMenuItem
                {
                    // Delete & download localizations data for the selected game
                    MenuSection = resources.GetString("LOCSsv"),
                    Description = resources.GetString("LOCSsvViewScreenshots"),
                    Action = (gameMenuItem) =>
                    {
                        var ViewExtension = new SsvScreenshotsView(PlayniteApi, PluginDatabase.GameContext);
                        Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PlayniteApi, resources.GetString("LOCSsvTitle"), ViewExtension);
                        windowExtension.ShowDialog();
                    }
                });

                if (!gameScreenshots.ScreenshotsFolder.IsNullOrEmpty() && Directory.Exists(gameScreenshots.ScreenshotsFolder))
                {
                    gameMenuItems.Add(new GameMenuItem
                    {
                        // Open directory
                        MenuSection = resources.GetString("LOCSsv"),
                        Description = resources.GetString("LOCSsvOpenScreenshotsDirectory"),
                        Action = (gameMenuItem) =>
                        {
                            Process.Start(gameScreenshots.ScreenshotsFolder);
                        }
                    });
                }
            }

            gameMenuItems.Add(new GameMenuItem
            {
                // Refresh data
                MenuSection = resources.GetString("LOCSsv"),
                Description = resources.GetString("LOCCommonRefreshGameData"),
                Action = (gameMenuItem) =>
                {
                    PluginDatabase.RefreshData(GameMenu);
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
            if (PluginSettings.Settings.MenuInExtensions)
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
        #endregion


        #region Game evenet
        public override void OnGameSelected(GameSelectionEventArgs args)
        {
            try
            {
                if (args.NewValue != null && args.NewValue.Count == 1)
                {
                    PluginDatabase.GameContext = args.NewValue[0];
                    PluginDatabase.SetThemesResources(PluginDatabase.GameContext);
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false);
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
            try
            {
                var TaskGameStopped = Task.Run(() =>
                {
                    if (game.Id == PluginDatabase.GameContext.Id)
                    {
                        PluginDatabase.SetThemesResources(PluginDatabase.GameContext);
                    }
                });
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false);
            }
        }

        // Add code to be executed when game is uninstalled.
        public override void OnGameUninstalled(Game game)
        {

        }
        #endregion


        #region Application event
        // Add code to be executed when Playnite is initialized.
        public override void OnApplicationStarted()
        {

        }

        // Add code to be executed when Playnite is shutting down.
        public override void OnApplicationStopped()
        {

        }
        #endregion


        // Add code to be executed when library is updated.
        public override void OnLibraryUpdated()
        {

        }


        #region Settings
        public override ISettings GetSettings(bool firstRunSettings)
        {
            return PluginSettings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new ScreenshotsVisualizerSettingsView(PlayniteApi, this.GetPluginUserDataPath());
        }
        #endregion
    }
}
