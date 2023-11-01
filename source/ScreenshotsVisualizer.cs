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
        public override Guid Id { get; } = Guid.Parse("c6c8276f-91bf-48e5-a1d1-4bee0b493488");

        internal TopPanelItem topPanelItem;
        internal SsvViewSidebar ssvViewSidebar;


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
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }

            // Custom theme button
            EventManager.RegisterClassHandler(typeof(Button), Button.ClickEvent, new RoutedEventHandler(OnCustomThemeButtonClick));

            // Custom elements integration
            AddCustomElementSupport(new AddCustomElementSupportArgs
            {
                ElementList = new List<string> { "PluginButton", "PluginSinglePicture", "PluginListScreenshots", "PluginListScreenshotsVertical", "PluginViewItem" },
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
                topPanelItem = new TopPanelItem()
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
                        WindowOptions windowOptions = new WindowOptions
                        {
                            ShowMinimizeButton = false,
                            ShowMaximizeButton = true,
                            ShowCloseButton = true,
                            Width = 1280,
                            Height = 740
                        };

                        SsvScreenshotsManager ViewExtension = new SsvScreenshotsManager();
                        Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PlayniteApi, resources.GetString("LOCSsv"), ViewExtension, windowOptions);
                        windowExtension.ResizeMode = ResizeMode.CanResize;
                        windowExtension.ShowDialog();
                    },
                    Visible = PluginSettings.Settings.EnableIntegrationButtonHeader
                };

                ssvViewSidebar = new SsvViewSidebar(this);
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
                    Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PlayniteApi, resources.GetString("LOCSsvTitle"), ViewExtension, windowOptions);
                    windowExtension.ShowDialog();
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
            yield return topPanelItem;
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
            public SsvViewSidebar(ScreenshotsVisualizer plugin)
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
                Visible = plugin.PluginSettings.Settings.EnableIntegrationButtonSide;
            }
        }

        public override IEnumerable<SidebarItem> GetSidebarItems()
        {
            return new List<SidebarItem>
            {
                ssvViewSidebar
            };
        }
        #endregion


        #region StartPageExtension
        public StartPageExtensionArgs GetAvailableStartPageViews()
        {
            List<StartPageViewArgsBase> views = new List<StartPageViewArgsBase> {
                new StartPageViewArgsBase { Name = resources.GetString("LOCSsvCarousel"), ViewId = "SsvCarousel", HasSettings = true }
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
                            ShowCloseButton = true,
                            CanBeResizable = true,
                            Height = 720,
                            Width = 1200
                        };

                        SsvScreenshotsView ViewExtension = new SsvScreenshotsView(PluginDatabase.GameContext);
                        Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PlayniteApi, resources.GetString("LOCSsvTitle"), ViewExtension, windowOptions);
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
                        MenuSection = resources.GetString("LOCSsv"),
                        Description = resources.GetString("LOCSsvMoveToSave"),
                        Action = (gameMenuItem) =>
                        {
                            if (Ids.Count == 1)
                            {
                                PluginDatabase.MoveToFolderToSave(GameMenu);
                            }
                            else
                            {
                                PluginDatabase.MoveToFolderToSave(Ids);
                            }
                        }
                    });
                }

                if (gameScreenshots.Items.Count > 0)
                {
                    gameMenuItems.Add(new GameMenuItem
                    {
                        MenuSection = resources.GetString("LOCSsv"),
                        Description = resources.GetString("LOCSsvConvertToJPG"),
                        Action = (gameMenuItem) =>
                        {
                            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(
                                $"{PluginDatabase.PluginName} - {resources.GetString("LOCCommonConverting")}",
                                true
                            );
                            globalProgressOptions.IsIndeterminate = Ids.Count == 1;

                            PlayniteApi.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
                            {
                                Stopwatch stopWatch = new Stopwatch();
                                stopWatch.Start();

                                string CancelText = string.Empty;
                                if (Ids.Count > 1)
                                {
                                    activateGlobalProgress.ProgressMaxValue = Ids.Count;
                                }

                                try
                                {
                                    Ids.ForEach(y =>
                                    {
                                        GameScreenshots data = PluginDatabase.Get(y);
                                        if (data.HasData)
                                        {
                                            bool HasConvert = false;
                                            data.Items.ForEach(x =>
                                            {
                                                if (activateGlobalProgress.CancelToken.IsCancellationRequested)
                                                {
                                                    CancelText = " canceled";
                                                    return;
                                                }

                                                if (!x.IsVideo)
                                                {
                                                    string oldFile = x.FileName;
                                                    string newFile = ImageTools.ConvertToJpg(oldFile, PluginDatabase.PluginSettings.Settings.JpgQuality);

                                                    if (!newFile.IsNullOrEmpty())
                                                    {
                                                        DateTime dt = File.GetLastWriteTime(oldFile);
                                                        File.SetLastWriteTime(newFile, dt);
                                                        FileSystem.DeleteFileSafe(oldFile);
                                                        HasConvert = true;
                                                    }
                                                }
                                            });

                                            if (HasConvert)
                                            {
                                                GameSettings gameSettings = PluginDatabase.GetGameSettings(y);
                                                if (gameSettings != null)
                                                {
                                                    PluginDatabase.SetDataFromSettings(gameSettings);
                                                }
                                            }

                                            if (activateGlobalProgress.CancelToken.IsCancellationRequested)
                                            {
                                                CancelText = " canceled";
                                                return;
                                            }

                                            if (Ids.Count > 1)
                                            {
                                                activateGlobalProgress.CurrentProgressValue++;
                                            }
                                        }
                                    });
                                }
                                catch (Exception ex)
                                {
                                    Common.LogError(ex, false, true, PluginDatabase.PluginName);
                                }

                                stopWatch.Stop();
                                TimeSpan ts = stopWatch.Elapsed;
                                logger.Info($"Task RefreshData(){CancelText} - {string.Format("{0:00}:{1:00}.{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10)} for {activateGlobalProgress.CurrentProgressValue}/{Ids.Count} items");
                            }, globalProgressOptions);
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

            if (PluginDatabase.PluginSettings.Settings.EnableFolderToSave)
            {
                mainMenuItems.Add(new MainMenuItem
                {
                    MenuSection = MenuInExtensions + resources.GetString("LOCSsv"),
                    Description = "-"
                });

                mainMenuItems.Add(new MainMenuItem
                {
                    MenuSection = MenuInExtensions + resources.GetString("LOCSsv"),
                    Description = resources.GetString("LOCSsvMoveToSave"),
                    Action = (gameMenuItem) =>
                    {
                        var dialogResult = PlayniteApi.Dialogs.ShowMessage(resources.GetString("LOCSsvWarningToMove"), resources.GetString("LOCSsv"), MessageBoxButton.YesNo);
                        if (dialogResult == MessageBoxResult.Yes)
                        {
                            if (PluginSettings.Settings.FolderToSave.IsNullOrEmpty() || PluginSettings.Settings.FileSavePattern.IsNullOrEmpty())
                            {
                                logger.Error("No settings to use folder to save");
                                PlayniteApi.Notifications.Add(new NotificationMessage(
                                    $"{PluginDatabase.PluginName}-MoveToFolderToSave-Errors",
                                    $"{PluginDatabase.PluginName}\r\n" + resources.GetString("LOCSsvMoveToFolderToSaveError"),
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
                if (args.NewValue?.Count == 1 && PluginDatabase.IsLoaded)
                {
                    PluginDatabase.GameContext = args.NewValue[0];
                    PluginDatabase.SetThemesResources(PluginDatabase.GameContext);
                }
                else
                {
                    Task.Run(() =>
                    {
                        System.Threading.SpinWait.SpinUntil(() => PluginDatabase.IsLoaded, -1);

                        Application.Current.Dispatcher.BeginInvoke((Action)delegate
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
                var TaskGameStopped = Task.Run(() =>
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
