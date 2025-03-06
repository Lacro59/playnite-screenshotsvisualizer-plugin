using CommonPlayniteShared.Common;
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
using StartPage.SDK;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ScreenshotsVisualizer
{
    public class ScreenshotsVisualizer : PluginExtended<ScreenshotsVisualizerSettingsViewModel, ScreenshotsVisualizerDatabase>, StartPage.SDK.IStartPageExtension
    {
        public override Guid Id => Guid.Parse("c6c8276f-91bf-48e5-a1d1-4bee0b493488");

        internal TopPanelItem TopPanelItem { get; set; }
        internal SidebarItem SidebarItem { get; set; }
        internal SidebarItemControl SidebarItemControl { get; set; }


        public ScreenshotsVisualizer(IPlayniteAPI api) : base(api)
        {
            // Manual dll load
            try
            {
                string PluginPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string PathDLL = Path.Combine(PluginPath, "VirtualizingWrapPanel.dll");
                if (File.Exists(PathDLL))
                {
                    Assembly DLL = Assembly.LoadFile(PathDLL);
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }

            // Custom theme button
            EventManager.RegisterClassHandler(typeof(Button), Button.ClickEvent, new RoutedEventHandler(OnCustomThemeButtonClick));

            // Custom elements integration
            AddCustomElementSupport(new AddCustomElementSupportArgs
            {
                ElementList = new List<string> { "PluginButton", "PluginSinglePicture", "PluginScreenshots", "PluginListScreenshots", "PluginListScreenshotsVertical", "PluginViewItem" },
                SourceName = PluginDatabase.PluginName
            });

            // Settings integration
            AddSettingsSupport(new AddSettingsSupportArgs
            {
                SourceName = PluginDatabase.PluginName,
                SettingsRoot = $"{nameof(PluginSettings)}.{nameof(PluginSettings.Settings)}"
            });

            // Initialize top & side bar
            if (API.Instance.ApplicationInfo.Mode == ApplicationMode.Desktop)
            {
                TopPanelItem = new ScreenshotsVisualizerTopPanelItem(this);
                SidebarItem = new ScreenshotsVisualizerViewSidebar(this);
            }
        }


        #region Custom event
        public void OnCustomThemeButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                string ButtonName = ((Button)sender).Name;
                if (ButtonName == "PART_CustomSsvButton")
                {
                    Common.LogDebug(true, $"OnCustomThemeButtonClick()");

                    WindowOptions windowOptions = new WindowOptions
                    {
                        ShowMinimizeButton = false,
                        ShowMaximizeButton = true,
                        ShowCloseButton = true,
                        CanBeResizable = true,
                        Height = 720,
                        Width = 1200
                    };

                    SsvScreenshotsView ViewExtension = new SsvScreenshotsView(PluginDatabase.GameContext);
                    Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(ResourceProvider.GetString("LOCSsvTitle"), ViewExtension, windowOptions);
                    _ = windowExtension.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }
        }
        #endregion


        #region Theme integration
        // Button on top panel
        public override IEnumerable<TopPanelItem> GetTopPanelItems()
        {
            yield return TopPanelItem;
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

            if (args.Name == "PluginScreenshots")
            {
                return new PluginScreenshots();
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

        public override IEnumerable<SidebarItem> GetSidebarItems()
        {
            return new List<SidebarItem>
            {
                SidebarItem
            };
        }
        #endregion


        #region StartPageExtension
        public StartPageExtensionArgs GetAvailableStartPageViews()
        {
            List<StartPageViewArgsBase> views = new List<StartPageViewArgsBase>
            {
                new StartPageViewArgsBase 
                {
                    Name = ResourceProvider.GetString("LOCSsvCarousel"),
                    ViewId = "SsvCarousel",
                    HasSettings = true
                }
            };
            return new StartPageExtensionArgs { ExtensionName = PluginDatabase.PluginName, Views = views };
        }

        public object GetStartPageView(string viewId, Guid instanceId)
        {
            switch (viewId)
            {
                case "SsvCarousel":
                    return new Views.StartPage.SsvCarousel();

                default:
                    return null;
            }
        }

        public Control GetStartPageViewSettings(string viewId, Guid instanceId)
        {
            switch (viewId)
            {
                case "SsvCarousel":
                    return new Views.StartPage.SsvCarouselSettings(this);

                default:
                    return null;
            }
        }

        public void OnViewRemoved(string viewId, Guid instanceId)
        {

        }
        #endregion


        #region Menus
        // To add new game menu items override GetGameMenuItems
        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            Game gameMenu = args.Games.First();
            List<Guid> ids = args.Games.Select(x => x.Id).ToList();
            GameScreenshots gameScreenshots = PluginDatabase.Get(gameMenu);

            List<GameMenuItem> gameMenuItems = new List<GameMenuItem>();

            if (gameScreenshots.HasData)
            {
                gameMenuItems.Add(new GameMenuItem
                {
                    // Delete & download localizations data for the selected game
                    MenuSection = ResourceProvider.GetString("LOCSsv"),
                    Description = ResourceProvider.GetString("LOCSsvViewScreenshots"),
                    Action = (gameMenuItem) =>
                    {
                        WindowOptions windowOptions = new WindowOptions
                        {
                            ShowMinimizeButton = false,
                            ShowMaximizeButton = true,
                            ShowCloseButton = true,
                            CanBeResizable = true,
                            Height = 720,
                            Width = 1200
                        };

                        SsvScreenshotsView ViewExtension = new SsvScreenshotsView(PluginDatabase.GameContext);
                        Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(ResourceProvider.GetString("LOCSsvTitle"), ViewExtension, windowOptions);
                        windowExtension.ShowDialog();
                    }
                });

                if (gameScreenshots.ScreenshotsFolders?.Count != 0 && gameScreenshots.FoldersExist)
                {
                    gameMenuItems.Add(new GameMenuItem
                    {
                        // Open directory
                        MenuSection = ResourceProvider.GetString("LOCSsv"),
                        Description = ResourceProvider.GetString("LOCSsvOpenScreenshotsDirectory"),
                        Action = (gameMenuItem) =>
                        {
                            foreach (string Folder in gameScreenshots.ScreenshotsFolders)
                            {
                                if (Directory.Exists(Folder))
                                {
                                    Process.Start(Folder);
                                }
                            }
                        }
                    });
                }

                if (gameScreenshots.Items.Count > 0 && PluginDatabase.PluginSettings.Settings.EnableFolderToSave)
                {
                    gameMenuItems.Add(new GameMenuItem
                    {
                        MenuSection = ResourceProvider.GetString("LOCSsv"),
                        Description = ResourceProvider.GetString("LOCSsvMoveToSave"),
                        Action = (gameMenuItem) =>
                        {
                            if (ids.Count == 1)
                            {
                                PluginDatabase.MoveToFolderToSave(gameMenu);
                            }
                            else
                            {
                                PluginDatabase.MoveToFolderToSave(ids);
                            }
                        }
                    });
                }

                if (gameScreenshots.Items.Count > 0)
                {
                    gameMenuItems.Add(new GameMenuItem
                    {
                        MenuSection = ResourceProvider.GetString("LOCSsv"),
                        Description = ResourceProvider.GetString("LOCSsvConvertToJPG"),
                        Action = (gameMenuItem) =>
                        {
                            PluginDatabase.ConvertGameSsvToJpg(ids);
                        }
                    });
                }

                gameMenuItems.Add(new GameMenuItem
                {
                    MenuSection = ResourceProvider.GetString("LOCSsv"),
                    Description = "-"
                });
            }

            gameMenuItems.Add(new GameMenuItem
            {
                // Refresh data
                MenuSection = ResourceProvider.GetString("LOCSsv"),
                Description = ResourceProvider.GetString("LOCCommonRefreshGameData"),
                Action = (gameMenuItem) =>
                {
                    if (ids.Count == 1)
                    {
                        PluginDatabase.Refresh(gameMenu);
                    }
                    else
                    {
                        PluginDatabase.Refresh(ids);
                    }
                }
            });

#if DEBUG
            gameMenuItems.Add(new GameMenuItem
            {
                MenuSection = ResourceProvider.GetString("LOCSsv"),
                Description = "-"
            });
            gameMenuItems.Add(new GameMenuItem
            {
                MenuSection = ResourceProvider.GetString("LOCSsv"),
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
                MenuSection = MenuInExtensions + ResourceProvider.GetString("LOCSsv"),
                Description = ResourceProvider.GetString("LOCCommonRefreshAllData"),
                Action = (mainMenuItem) =>
                {
                    PluginDatabase.Refresh(API.Instance.Database.Games?.Select(x => x.Id));
                }
            });

            if (PluginDatabase.PluginSettings.Settings.EnableTag)
            {
                mainMenuItems.Add(new MainMenuItem
                {
                    MenuSection = MenuInExtensions + ResourceProvider.GetString("LOCSsv"),
                    Description = "-"
                });

                // Add tag for selected game in database if data exists
                mainMenuItems.Add(new MainMenuItem
                {
                    MenuSection = MenuInExtensions + ResourceProvider.GetString("LOCSsv"),
                    Description = ResourceProvider.GetString("LOCCommonAddTPlugin"),
                    Action = (mainMenuItem) =>
                    {
                        PluginDatabase.AddTagSelectData();
                    }
                });
                // Add tag for all games
                mainMenuItems.Add(new MainMenuItem
                {
                    MenuSection = MenuInExtensions + ResourceProvider.GetString("LOCSsv"),
                    Description = ResourceProvider.GetString("LOCCommonAddAllTags"),
                    Action = (mainMenuItem) =>
                    {
                        PluginDatabase.AddTagAllGame();
                    }
                });
                // Remove tag for all game in database
                mainMenuItems.Add(new MainMenuItem
                {
                    MenuSection = MenuInExtensions + ResourceProvider.GetString("LOCSsv"),
                    Description = ResourceProvider.GetString("LOCCommonRemoveAllTags"),
                    Action = (mainMenuItem) =>
                    {
                        PluginDatabase.RemoveTagAllGame();
                    }
                });
            }

            mainMenuItems.Add(new MainMenuItem
            {
                MenuSection = MenuInExtensions + ResourceProvider.GetString("LOCSsv"),
                Description = "-"
            });

            mainMenuItems.Add(new MainMenuItem
            {
                MenuSection = MenuInExtensions + ResourceProvider.GetString("LOCSsv"),
                Description = ResourceProvider.GetString("LOCSsvConvertToJPGForAll"),
                Action = (gameMenuItem) =>
                {
                    PluginDatabase.ConvertGameSsvToJpg(PluginDatabase.Database.Items.Select(x => x.Key).ToList());
                }
            });


            if (PluginDatabase.PluginSettings.Settings.EnableFolderToSave)
            {
                mainMenuItems.Add(new MainMenuItem
                {
                    MenuSection = MenuInExtensions + ResourceProvider.GetString("LOCSsv"),
                    Description = ResourceProvider.GetString("LOCSsvMoveToSave"),
                    Action = (gameMenuItem) =>
                    {
                        MessageBoxResult dialogResult = PlayniteApi.Dialogs.ShowMessage(ResourceProvider.GetString("LOCSsvWarningToMove"), ResourceProvider.GetString("LOCSsv"), MessageBoxButton.YesNo);
                        if (dialogResult == MessageBoxResult.Yes)
                        {
                            if (PluginSettings.Settings.FolderToSave.IsNullOrEmpty() || PluginSettings.Settings.FileSavePattern.IsNullOrEmpty())
                            {
                                Logger.Warn("No settings to use folder to save");
                                PlayniteApi.Notifications.Add(new NotificationMessage(
                                    $"{PluginDatabase.PluginName}-MoveToFolderToSave-Errors",
                                    $"{PluginDatabase.PluginName}\r\n" + ResourceProvider.GetString("LOCSsvMoveToFolderToSaveError"),
                                    NotificationType.Error,
                                    () => this.OpenSettingsView()
                                ));
                            }
                            else
                            {
                                PluginDatabase.MoveToFolderToSaveAll();
                            }
                        }
                    }
                });
            }


#if DEBUG
            mainMenuItems.Add(new MainMenuItem
            {
                MenuSection = MenuInExtensions + ResourceProvider.GetString("LOCSsv"),
                Description = "-"
            });
            mainMenuItems.Add(new MainMenuItem
            {
                MenuSection = MenuInExtensions + ResourceProvider.GetString("LOCSsv"),
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
                if (args.NewValue?.Count == 1 && PluginDatabase.IsLoaded)
                {
                    PluginDatabase.GameContext = args.NewValue[0];
                    PluginDatabase.SetThemesResources(PluginDatabase.GameContext);
                }
                else
                {
                    _ = Task.Run(() =>
                    {
                        _ = System.Threading.SpinWait.SpinUntil(() => PluginDatabase.IsLoaded, -1);
                        _ = Application.Current.Dispatcher.BeginInvoke((Action)delegate
                        {
                            if (args.NewValue?.Count == 1)
                            {
                                PluginDatabase.GameContext = args.NewValue[0];
                                PluginDatabase.SetThemesResources(PluginDatabase.GameContext);
                            }
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
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
                _ = Task.Run(() =>
                {
                    try
                    {
                        GameSettings gameSettings = PluginDatabase.GetGameSettings(args.Game.Id);
                        if (gameSettings != null)
                        {
                            PluginDatabase.SetDataFromSettings(gameSettings);
                        }
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false, true, PluginDatabase.PluginName);
                    }

                    if (args.Game.Id == PluginDatabase.GameContext.Id)
                    {
                        PluginDatabase.SetThemesResources(PluginDatabase.GameContext);
                    }
                });
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
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
            return new ScreenshotsVisualizerSettingsView();
        }
        #endregion
    }
}
