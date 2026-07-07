using CommonPluginsShared;
using CommonPluginsShared.Collections;
using CommonPluginsShared.Extensions;
using CommonPluginsShared.Interfaces;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using ScreenshotsVisualizer.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;

namespace ScreenshotsVisualizer.Services
{
    /// <summary>
    /// Manages context menus for the ScreenshotsVisualizer plugin (game menu and main menu).
    /// </summary>
    public class ScreenshotsVisualizerMenus : PluginMenus
    {
        private static readonly ILogger Logger = LogManager.GetLogger();
        private readonly ScreenshotsVisualizer _plugin;
        private ScreenshotsVisualizerDatabase Database => (ScreenshotsVisualizerDatabase)_database;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScreenshotsVisualizerMenus"/> class.
        /// </summary>
        /// <param name="settings">Plugin settings used for menu visibility and options.</param>
        /// <param name="database">Plugin database instance.</param>
        /// <param name="plugin">Plugin instance (settings navigation).</param>
        public ScreenshotsVisualizerMenus(IPluginSettings settings, IPluginDatabase database, ScreenshotsVisualizer plugin) : base(settings, database)
        {
            _plugin = plugin;
        }

        /// <inheritdoc />
        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            if (args?.Games == null || !args.Games.Any())
            {
                return Enumerable.Empty<GameMenuItem>();
            }

            List<Game> includedGames = args.Games
                .Where(game => PlayniteTools.ShouldIncludeLibraryGame(game, _settings))
                .ToList();

            if (includedGames.Count == 0)
            {
                Common.LogDebug(true, string.Format(
                    "[LibraryFilter] ScreenshotsVisualizerMenus: game menu hidden — no eligible game in selection ({0} selected, IncludeEmulatedGames={1}, SourceFilter={2})",
                    args.Games.Count,
                    _settings.IncludeEmulatedGames,
                    PlayniteTools.FormatSourceFilterForLog(_settings)));

                return Enumerable.Empty<GameMenuItem>();
            }

            int excludedCount = args.Games.Count - includedGames.Count;
            if (excludedCount > 0)
            {
                Common.LogDebug(true, string.Format(
                    "[LibraryFilter] ScreenshotsVisualizerMenus: {0}/{1} selected game(s) excluded from menu actions (IncludeEmulatedGames={2}, SourceFilter={3})",
                    excludedCount,
                    args.Games.Count,
                    _settings.IncludeEmulatedGames,
                    PlayniteTools.FormatSourceFilterForLog(_settings)));
            }

            Game gameMenu = includedGames.First();
            List<Guid> ids = includedGames.Select(x => x.Id).ToList();
            GameScreenshots gameScreenshots = Database.Get(gameMenu);

            List<GameMenuItem> gameMenuItems = new List<GameMenuItem>();

            if (gameScreenshots.HasData)
            {
                gameMenuItems.Add(new GameMenuItem
                {
                    MenuSection = ResourceProvider.GetString("LOCSsv"),
                    Description = ResourceProvider.GetString("LOCSsvViewScreenshots"),
                    Action = gameMenuItem =>
                    {
                        Database.PluginWindows.ShowPluginGameDataWindow(gameMenu);
                    }
                });

                if (gameScreenshots.ScreenshotsFolders?.Count != 0 && gameScreenshots.FoldersExist)
                {
                    gameMenuItems.Add(new GameMenuItem
                    {
                        MenuSection = ResourceProvider.GetString("LOCSsv"),
                        Description = ResourceProvider.GetString("LOCSsvOpenScreenshotsDirectory"),
                        Action = gameMenuItem =>
                        {
                            foreach (string folder in gameScreenshots.ScreenshotsFolders)
                            {
                                if (Directory.Exists(folder))
                                {
                                    Process.Start(folder);
                                }
                            }
                        }
                    });
                }

                if (gameScreenshots.Items.Count > 0 && Database.PluginSettings.EnableFolderToSave)
                {
                    gameMenuItems.Add(new GameMenuItem
                    {
                        MenuSection = ResourceProvider.GetString("LOCSsv"),
                        Description = ResourceProvider.GetString("LOCSsvMoveToSave"),
                        Action = gameMenuItem =>
                        {
                            Common.LogDebug(true, string.Format(
                                "[SsvMenus] MoveToFolderToSave requested for {0} game(s) (global archive config)",
                                ids.Count));

                            if (ids.Count == 1)
                            {
                                Database.MoveToFolderToSave(gameMenu);
                            }
                            else
                            {
                                Database.MoveToFolderToSave(ids);
                            }
                        }
                    });
                }

                if (gameScreenshots.Items.Count > 0)
                {
                    AddGameConversionMenuItems(gameMenuItems, ResourceProvider.GetString("LOCSsv"), ids);
                }

                gameMenuItems.Add(new GameMenuItem
                {
                    MenuSection = ResourceProvider.GetString("LOCSsv"),
                    Description = "-"
                });
            }

            gameMenuItems.Add(new GameMenuItem
            {
                MenuSection = ResourceProvider.GetString("LOCSsv"),
                Description = ResourceProvider.GetString("LOCCommonRefreshGameData"),
                Action = gameMenuItem =>
                {
                    if (ids.Count == 1)
                    {
                        Database.Refresh(gameMenu.Id);
                    }
                    else
                    {
                        Database.Refresh(ids);
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
                Action = mainMenuItem => { }
            });
#endif

            return gameMenuItems;
        }

        /// <inheritdoc />
        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            string menuInExtensions = string.Empty;
            if (_settings.MenuInExtensions)
            {
                menuInExtensions = "@";
            }

            string section = menuInExtensions + ResourceProvider.GetString("LOCSsv");

            List<MainMenuItem> mainMenuItems = new List<MainMenuItem>
            {
                new MainMenuItem
                {
                    MenuSection = section,
                    Description = ResourceProvider.GetString("LOCCommonRefreshAllData"),
                    Action = mainMenuItem =>
                    {
                        Database.Refresh(API.Instance.Database.Games?.Select(x => x.Id));
                    }
                }
            };

            if (_settings.EnableTag)
            {
                mainMenuItems.Add(new MainMenuItem
                {
                    MenuSection = section,
                    Description = "-"
                });

                mainMenuItems.Add(new MainMenuItem
                {
                    MenuSection = section,
                    Description = ResourceProvider.GetString("LOCCommonAddTPlugin"),
                    Action = mainMenuItem => Database.AddTagSelectData()
                });

                mainMenuItems.Add(new MainMenuItem
                {
                    MenuSection = section,
                    Description = ResourceProvider.GetString("LOCCommonAddAllTags"),
                    Action = mainMenuItem => _commands.CmdAddTag.Execute(null)
                });

                mainMenuItems.Add(new MainMenuItem
                {
                    MenuSection = section,
                    Description = ResourceProvider.GetString("LOCCommonRemoveAllTags"),
                    Action = mainMenuItem => _commands.CmdRemoveTag.Execute(null)
                });
            }

            mainMenuItems.Add(new MainMenuItem
            {
                MenuSection = section,
                Description = "-"
            });

            mainMenuItems.Add(new MainMenuItem
            {
                MenuSection = section,
                Description = ResourceProvider.GetString("LOCCommonExport"),
                Action = mainMenuItem => Database.ExtractToCsv()
            });

            mainMenuItems.Add(new MainMenuItem
            {
                MenuSection = section,
                Description = "-"
            });

            AddMainConversionMenuItems(mainMenuItems, section);

            if (Database.PluginSettings.EnableFolderToSave)
            {
                mainMenuItems.Add(new MainMenuItem
                {
                    MenuSection = section,
                    Description = ResourceProvider.GetString("LOCSsvMoveToSave"),
                    Action = gameMenuItem =>
                    {
                        MessageBoxResult dialogResult = API.Instance.Dialogs.ShowMessage(
                            ResourceProvider.GetString("LOCSsvWarningToMove"),
                            ResourceProvider.GetString("LOCSsv"),
                            MessageBoxButton.YesNo);

                        if (dialogResult == MessageBoxResult.Yes)
                        {
                            if (Database.PluginSettings.FolderToSave.IsNullOrEmpty() || Database.PluginSettings.FileSavePattern.IsNullOrEmpty())
                            {
                                Logger.Warn("[SsvMenus] No settings to use folder to save (global FolderToSave / FileSavePattern)");
                                API.Instance.Notifications.Add(new NotificationMessage(
                                    $"{Database.PluginName}-MoveToFolderToSave-Errors",
                                    $"{Database.PluginName}\r\n" + ResourceProvider.GetString("LOCSsvMoveToFolderToSaveError"),
                                    NotificationType.Error,
                                    () => _plugin.OpenSettingsView()));
                            }
                            else
                            {
                                Common.LogDebug(true, "[SsvMenus] MoveToFolderToSaveAll requested (global archive config)");
                                Database.MoveToFolderToSaveAll();
                            }
                        }
                    }
                });
            }

#if DEBUG
            mainMenuItems.Add(new MainMenuItem
            {
                MenuSection = section,
                Description = "-"
            });
            mainMenuItems.Add(new MainMenuItem
            {
                MenuSection = section,
                Description = "Test",
                Action = mainMenuItem => { }
            });
#endif

            return mainMenuItems;
        }

        /// <summary>
        /// Adds game menu entries for each configured ImageMagick conversion profile.
        /// </summary>
        /// <param name="menuItems">Target menu item collection.</param>
        /// <param name="menuSection">Root menu section (<c>LOCSsv</c>).</param>
        /// <param name="gameIds">Selected game identifiers to convert.</param>
        private void AddGameConversionMenuItems(List<GameMenuItem> menuItems, string menuSection, List<Guid> gameIds)
        {
            IList<SsvImageConversionCustomCmd> commands = GetConfiguredConversionCommands();
            if (commands.Count == 0)
            {
                return;
            }

            string convertImagesParentSection = BuildConvertImagesMenuSection(menuSection);

            foreach (SsvImageConversionCustomCmd command in commands)
            {
                SsvImageConversionCustomCmd commandCopy = command;
                menuItems.Add(new GameMenuItem
                {
                    MenuSection = convertImagesParentSection,
                    Description = GetConversionCommandMenuLabel(commandCopy),
                    Action = gameMenuItem =>
                    {
                        Common.LogDebug(true, string.Format(
                            "[SsvMenus] ConvertGameScreenshots requested — profile '{0}', {1} game(s)",
                            GetConversionCommandMenuLabel(commandCopy),
                            gameIds.Count));
                        Database.ConvertGameScreenshots(gameIds, commandCopy);
                    }
                });
            }
        }

        /// <summary>
        /// Adds main menu entries for each configured ImageMagick conversion profile (all cached games).
        /// </summary>
        /// <param name="menuItems">Target menu item collection.</param>
        /// <param name="menuSection">Root menu section.</param>
        private void AddMainConversionMenuItems(List<MainMenuItem> menuItems, string menuSection)
        {
            IList<SsvImageConversionCustomCmd> commands = GetConfiguredConversionCommands();
            if (commands.Count == 0)
            {
                return;
            }

            string convertImagesParentSection = BuildConvertImagesMenuSection(menuSection);
            List<Guid> allGameIds = Database.GetAllCache().Select(x => x.Id).ToList();

            foreach (SsvImageConversionCustomCmd command in commands)
            {
                SsvImageConversionCustomCmd commandCopy = command;
                menuItems.Add(new MainMenuItem
                {
                    MenuSection = convertImagesParentSection,
                    Description = GetConversionCommandMenuLabel(commandCopy),
                    Action = mainMenuItem =>
                    {
                        Common.LogDebug(true, string.Format(
                            "[SsvMenus] ConvertGameScreenshots requested (all games) — profile '{0}', {1} game(s)",
                            GetConversionCommandMenuLabel(commandCopy),
                            allGameIds.Count));
                        Database.ConvertGameScreenshots(allGameIds, commandCopy);
                    }
                });
            }
        }

        /// <summary>
        /// Returns persisted conversion profiles without seeding defaults.
        /// </summary>
        /// <returns>Configured commands or an empty list.</returns>
        private IList<SsvImageConversionCustomCmd> GetConfiguredConversionCommands()
        {
            List<SsvImageConversionCustomCmd> commands = Database.PluginSettings?.CustomConversionCmds;
            if (commands == null || commands.Count == 0)
            {
                return new List<SsvImageConversionCustomCmd>();
            }

            return commands.Where(x => x != null).ToList();
        }

        /// <summary>
        /// Builds the nested menu section for conversion profile children.
        /// </summary>
        /// <param name="rootMenuSection">Root plugin menu section.</param>
        /// <returns>Section path <c>LOCSsv|Convert images</c>.</returns>
        private static string BuildConvertImagesMenuSection(string rootMenuSection)
        {
            return rootMenuSection + "|" + ResourceProvider.GetString("LOCSsvConvertImages");
        }

        /// <summary>
        /// Resolves the menu label for a conversion profile.
        /// </summary>
        /// <param name="command">Conversion profile.</param>
        /// <returns>Display name for the menu entry.</returns>
        private static string GetConversionCommandMenuLabel(SsvImageConversionCustomCmd command)
        {
            if (!string.IsNullOrWhiteSpace(command?.Name))
            {
                return command.Name.Trim();
            }

            return ResourceProvider.GetString("LOCSsvImageConversionUnnamed");
        }
    }
}
