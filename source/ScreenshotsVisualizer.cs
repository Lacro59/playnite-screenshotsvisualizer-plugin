using CommonPluginsShared;
using CommonPluginsShared.Controls;
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
using System.Windows.Media;

namespace ScreenshotsVisualizer
{
    public class ScreenshotsVisualizer : PluginExtended<ScreenshotsVisualizerSettingsViewModel, ScreenshotsVisualizerDatabase>
    {
        public override Guid Id { get; } = Guid.Parse("c6c8276f-91bf-48e5-a1d1-4bee0b493488");


        public ScreenshotsVisualizer(IPlayniteAPI api) : base(api)
        {            
            // Manual dll load
            try
            {
                string PluginPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string PathDLL = Path.Combine(PluginPath, "VirtualizingWrapPanel.dll");
                if (File.Exists(PathDLL))
                {
                    var DLL = Assembly.LoadFile(PathDLL);
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false);
            }

            // Custom theme button
            EventManager.RegisterClassHandler(typeof(Button), Button.ClickEvent, new RoutedEventHandler(OnCustomThemeButtonClick));

            // Custom elements integration
            AddCustomElementSupport(new AddCustomElementSupportArgs
            {
                ElementList = new List<string> { "PluginButton", "PluginSinglePicture", "PluginListScreenshots", "PluginListScreenshotsVertical", "PluginViewItem" },
                SourceName = "ScreenshotsVisualizer"
            });

            // Settings integration
            AddSettingsSupport(new AddSettingsSupportArgs
            {
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
                if (ButtonName == "PART_CustomSsvButton")
                {
                    Common.LogDebug(true, $"OnCustomThemeButtonClick()");

                    WindowOptions windowOptions = new WindowOptions
                    {
                        ShowMinimizeButton = false,
                        ShowMaximizeButton = true,
                        ShowCloseButton = true
                    };

                    var ViewExtension = new SsvScreenshotsView(PluginDatabase.GameContext);
                    Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PlayniteApi, resources.GetString("LOCSsvTitle"), ViewExtension, windowOptions);
                    windowExtension.ResizeMode = ResizeMode.CanResize;
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
        // Button on top panel
        public override IEnumerable<TopPanelItem> GetTopPanelItems()
        {
            if (PluginSettings.Settings.EnableIntegrationButtonHeader)
            {
                yield return new TopPanelItem()
                {
                    Icon = new TextBlock
                    {
                        Text = "\uea38",
                        FontSize = 20,
                        FontFamily = resources.GetResource("CommonFont") as FontFamily
                    },
                    Title = resources.GetString("LOCSsv"),
                    Activated = () =>
                    {
                        var windowOptions = new WindowOptions
                        {
                            ShowMinimizeButton = false,
                            ShowMaximizeButton = true,
                            ShowCloseButton = true,
                            Width = 1280,
                            Height = 740
                        };

                        var ViewExtension = new SsvScreenshotsManager();
                        Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PlayniteApi, resources.GetString("LOCSsv"), ViewExtension, windowOptions);
                        windowExtension.ResizeMode = ResizeMode.CanResize;
                        windowExtension.ShowDialog();
                    }
                };
            }

            yield break;
        }

        // List custom controls
        public override Control GetGameViewControl(GetGameViewControlArgs args)
        {
            if (args.Name == "PluginButton")
            {
                return new PluginButton();
            }

            if (args.Name == "PluginSinglePicture")
            {
                return new PluginSinglePicture();
            }

            if (args.Name == "PluginListScreenshots")
            {
                return new PluginListScreenshots();
            }

            if (args.Name == "PluginListScreenshotsVertical")
            {
                return new PluginListScreenshotsVertical();
            }

            if (args.Name == "PluginViewItem")
            {
                return new PluginViewItem();
            }

            return null;
        }

        // SidebarItem
        public class SsvViewSidebar : SidebarItem
        {
            public SsvViewSidebar()
            {
                Type = SiderbarItemType.View;
                Title = resources.GetString("LOCSsv");
                Icon = new TextBlock
                {
                    Text = "\uea38",
                    FontFamily = resources.GetResource("CommonFont") as FontFamily
                };
                Opened = () =>
                {
                    SidebarItemControl sidebarItemControl = new SidebarItemControl(PluginDatabase.PlayniteApi);
                    sidebarItemControl.SetTitle(resources.GetString("LOCSsv"));
                    sidebarItemControl.AddContent(new SsvScreenshotsManager());

                    return sidebarItemControl;
                };
            }
        }

        public override IEnumerable<SidebarItem> GetSidebarItems()
        {
            var items = new List<SidebarItem>
            {
                new SsvViewSidebar()
            };
            return items;
        }
        #endregion


        #region Menus
        // To add new game menu items override GetGameMenuItems
        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            Game GameMenu = args.Games.First();
            List<Guid> Ids = args.Games.Select(x => x.Id).ToList();
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
                        WindowOptions windowOptions = new WindowOptions
                        {
                            ShowMinimizeButton = false,
                            ShowMaximizeButton = true,
                            ShowCloseButton = true
                        };

                        var ViewExtension = new SsvScreenshotsView(PluginDatabase.GameContext);
                        Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PlayniteApi, resources.GetString("LOCSsvTitle"), ViewExtension, windowOptions);
                        windowExtension.ResizeMode = ResizeMode.CanResize;
                        windowExtension.ShowDialog();
                    }
                });

                if (gameScreenshots.ScreenshotsFolders != null && gameScreenshots.ScreenshotsFolders.Count != 0 && gameScreenshots.FoldersExist)
                {
                    gameMenuItems.Add(new GameMenuItem
                    {
                        // Open directory
                        MenuSection = resources.GetString("LOCSsv"),
                        Description = resources.GetString("LOCSsvOpenScreenshotsDirectory"),
                        Action = (gameMenuItem) =>
                        {
                            foreach(string Folder in gameScreenshots.ScreenshotsFolders)
                            {
                                Process.Start(Folder);
                            }
                        }
                    });
                }

                if (gameScreenshots.Items.Count > 0 && PluginDatabase.PluginSettings.Settings.EnableFolderToSave)
                {
                    gameMenuItems.Add(new GameMenuItem
                    {
                        // Open directory
                        MenuSection = resources.GetString("LOCSsv"),
                        Description = resources.GetString("LOCSsvMoveToSave"),
                        Action = (gameMenuItem) =>
                        {
                            PluginDatabase.MoveToFolderToSave(GameMenu);
                        }
                    });
                }

                gameMenuItems.Add(new GameMenuItem
                {
                    MenuSection = resources.GetString("LOCSsv"),
                    Description = "-"
                });
            }

            gameMenuItems.Add(new GameMenuItem
            {
                // Refresh data
                MenuSection = resources.GetString("LOCSsv"),
                Description = resources.GetString("LOCCommonRefreshGameData"),
                Action = (gameMenuItem) =>
                {
                    if (Ids.Count == 1)
                    {
                        PluginDatabase.RefreshData(GameMenu);
                    }
                    else
                    {
                        PluginDatabase.RefreshData(Ids);
                    }
                }
            });

#if DEBUG
            gameMenuItems.Add(new GameMenuItem
            {
                MenuSection = resources.GetString("LOCSsv"),
                Description = "-"
            });
            gameMenuItems.Add(new GameMenuItem
            {
                MenuSection = resources.GetString("LOCSsv"),
                Description = "Test",
                Action = (mainMenuItem) => 
                {

                }
            });
#endif

            return gameMenuItems;
        }

        // To add new main menu items override GetMainMenuItems
        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
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
                Description = resources.GetString("LOCCommonRefreshAllData"),
                Action = (mainMenuItem) =>
                {
                    PluginDatabase.RefreshDataAll();
                }
            });

            if (PluginDatabase.PluginSettings.Settings.EnableTag)
            {
                mainMenuItems.Add(new MainMenuItem
                {
                    MenuSection = MenuInExtensions + resources.GetString("LOCSsv"),
                    Description = "-"
                });

                // Add tag for selected game in database if data exists
                mainMenuItems.Add(new MainMenuItem
                {
                    MenuSection = MenuInExtensions + resources.GetString("LOCSsv"),
                    Description = resources.GetString("LOCCommonAddTPlugin"),
                    Action = (mainMenuItem) =>
                    {
                        PluginDatabase.AddTagSelectData();
                    }
                });
                // Add tag for all games
                mainMenuItems.Add(new MainMenuItem
                {
                    MenuSection = MenuInExtensions + resources.GetString("LOCSsv"),
                    Description = resources.GetString("LOCCommonAddAllTags"),
                    Action = (mainMenuItem) =>
                    {
                        PluginDatabase.AddTagAllGame();
                    }
                });
                // Remove tag for all game in database
                mainMenuItems.Add(new MainMenuItem
                {
                    MenuSection = MenuInExtensions + resources.GetString("LOCSsv"),
                    Description = resources.GetString("LOCCommonRemoveAllTags"),
                    Action = (mainMenuItem) =>
                    {
                        PluginDatabase.RemoveTagAllGame();
                    }
                });
            }

#if DEBUG
            mainMenuItems.Add(new MainMenuItem
            {
                MenuSection = MenuInExtensions + resources.GetString("LOCSsv"),
                Description = "-"
            });
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


        #region Game event
        public override void OnGameSelected(OnGameSelectedEventArgs args)
        {
            try
            {
                if (args.NewValue?.Count == 1)
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
        public override void OnGameInstalled(OnGameInstalledEventArgs args)
        {

        }

        // Add code to be executed when game is uninstalled.
        public override void OnGameUninstalled(OnGameUninstalledEventArgs args)
        {

        }

        // Add code to be executed when game is preparing to be started.
        public override void OnGameStarting(OnGameStartingEventArgs args)
        {

        }

        // Add code to be executed when game is started running.
        public override void OnGameStarted(OnGameStartedEventArgs args)
        {

        }

        // Add code to be executed when game is preparing to be started.
        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            try
            {
                var TaskGameStopped = Task.Run(() =>
                {
                    try
                    {
                        GameSettings gameSettings = PluginSettings.Settings.gameSettings.Find(x => x.Id == args.Game.Id);

                        if (gameSettings != null)
                        {
                            PluginDatabase.SetDataFromSettings(gameSettings);
                        }
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false);
                    }

                    if (args.Game.Id == PluginDatabase.GameContext.Id)
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
        #endregion


        #region Application event
        // Add code to be executed when Playnite is initialized.
        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {

        }

        // Add code to be executed when Playnite is shutting down.
        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {

        }
        #endregion


        // Add code to be executed when library is updated.
        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
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
