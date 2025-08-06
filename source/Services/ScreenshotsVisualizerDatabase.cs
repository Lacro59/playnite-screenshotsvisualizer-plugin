using CommonPluginsShared;
using CommonPluginsShared.Collections;
using Playnite.SDK;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows;
using ScreenshotsVisualizer.Views;
using System.Threading;
using CommonPluginsShared.Extensions;
using CommonPlayniteShared.Common;
using System.Text;

namespace ScreenshotsVisualizer.Services
{
    public class ScreenshotsVisualizerDatabase : PluginDatabaseObject<ScreenshotsVisualizerSettingsViewModel, ScreeshotsVisualizeCollection, GameScreenshots, Screenshot>
    {
        public ScreenshotsVisualizerDatabase(ScreenshotsVisualizerSettingsViewModel pluginSettings, string pluginUserDataPath) : base(pluginSettings, "ScreenshotsVisualizer", pluginUserDataPath)
        {
            TagBefore = "[SSV]";
        }

        public override void RefreshNoLoader(Guid id)
        {
            Game game = API.Instance.Database.Games.Get(id);
            Logger.Info($"RefreshNoLoader({game?.Name} - {game?.Id})");

            GameSettings gameSettings = GetGameSettings(game.Id);
            if (gameSettings != null)
            {
                SetDataFromSettings(gameSettings);
            }

            GameScreenshots gameScreenshots = Get(game, true);
            ActionAfterRefresh(gameScreenshots);
        }

        #region Move data

        /// <summary>
        /// Moves all screenshots in the database to their designated save folders.
        /// This operation displays a global progress dialog, allowing the user to cancel the process.
        /// For each item in the database, the method attempts to move the screenshots using MoveToFolderToSaveWithNoLoader.
        /// Any exceptions encountered are logged. The total operation time is also logged.
        /// </summary>
        public void MoveToFolderToSaveAll()
        {
            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions($"{PluginName} - {ResourceProvider.GetString("LOCSsvMovingToSave")}")
            {
                Cancelable = true,
                IsIndeterminate = false
            };

            _ = API.Instance.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
            {
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();

                string cancelText = string.Empty;
                activateGlobalProgress.ProgressMaxValue = Database.Items.Count;

                foreach (KeyValuePair<Guid, GameScreenshots> item in Database.Items)
                {
                    if (activateGlobalProgress.CancelToken.IsCancellationRequested)
                    {
                        cancelText = " canceled";
                        break;
                    }

                    try
                    {
                        MoveToFolderToSaveWithNoLoader(item.Key, activateGlobalProgress);
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false, true, PluginName);
                    }
                    activateGlobalProgress.CurrentProgressValue++;
                }

                stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed;
                Logger.Info($"MoveToFolderToSaveAll{cancelText} - {string.Format("{0:00}:{1:00}.{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10)}");
            }, globalProgressOptions);
        }

        /// <summary>
        /// Moves all screenshots associated with the specified game to their configured save folder.
        /// This operation is performed within a global progress dialog, which is shown to the user.
        /// The actual move logic is handled by <see cref="MoveToFolderToSaveWithNoLoader(Game)"/>.
        /// </summary>
        /// <param name="game">The game whose screenshots will be moved.</param>
        public void MoveToFolderToSave(Game game)
        {
            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions($"{PluginName} - {ResourceProvider.GetString("LOCSsvMovingToSave")}")
            {
                Cancelable = false,
                IsIndeterminate = true
            };

            _ = API.Instance.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
            {
                MoveToFolderToSaveWithNoLoader(game);
            }, globalProgressOptions);
        }

        /// <summary>
        /// Moves all screenshots for the specified list of game IDs to their configured save folders.
        /// This operation is performed within a global progress dialog, which allows the user to cancel the process.
        /// For each game ID, the method attempts to move the screenshots using <see cref="MoveToFolderToSaveWithNoLoader(Guid, GlobalProgressActionArgs)"/>.
        /// Any exceptions encountered are logged. The total operation time and the number of processed items are also logged.
        /// </summary>
        /// <param name="ids">The list of game IDs whose screenshots will be moved.</param>
        public void MoveToFolderToSave(List<Guid> ids)
        {
            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(
                $"{PluginName} - {ResourceProvider.GetString("LOCSsvMovingToSave")}",
                true
            );
            globalProgressOptions.IsIndeterminate = false;

            _ = API.Instance.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
            {
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();

                string cancelText = string.Empty;
                activateGlobalProgress.ProgressMaxValue = ids.Count;

                Database.BeginBufferUpdate();

                try
                {
                    foreach (Guid Id in ids)
                    {
                        if (activateGlobalProgress.CancelToken.IsCancellationRequested)
                        {
                            cancelText = " canceled";
                            break;
                        }

                        MoveToFolderToSaveWithNoLoader(Id, activateGlobalProgress);
                        activateGlobalProgress.CurrentProgressValue++;
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, PluginName);
                }

                Database.EndBufferUpdate();

                stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed;
                Logger.Info($"Task MoveToFolderToSave(){cancelText} - {string.Format("{0:00}:{1:00}.{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10)} for {activateGlobalProgress.CurrentProgressValue}/{ids.Count} items");
            }, globalProgressOptions);
        }

        /// <summary>
        /// Moves all screenshots for the specified game ID to its configured save folder without displaying a loader.
        /// Updates the progress dialog text with the game's name during the operation.
        /// The actual move logic is handled by <see cref="MoveToFolderToSaveWithNoLoader(Game)"/>.
        /// </summary>
        /// <param name="id">The unique identifier of the game whose screenshots will be moved.</param>
        /// <param name="globalProgressActionArgs">The progress dialog arguments, used to update progress and display the current game name.</param>
        public void MoveToFolderToSaveWithNoLoader(Guid id, GlobalProgressActionArgs globalProgressActionArgs)
        {
            Game game = API.Instance.Database.Games.Get(id);
            if (game != null)
            {
                globalProgressActionArgs.Text = game.Name;
                MoveToFolderToSaveWithNoLoader(game);
            }
        }

        /// <summary>
        /// Moves all screenshots for the specified game to its configured save folder without displaying a loader or progress dialog.
        /// The method checks if the folder-to-save feature is enabled and if the necessary settings are present.
        /// For each screenshot, it generates the destination path and moves the file if it is not already in the target folder.
        /// Handles file name conflicts and updates the game data after the move.
        /// Any errors encountered during the process are logged, and notifications are shown if settings are missing.
        /// </summary>
        /// <param name="game">The game whose screenshots will be moved.</param>
        public void MoveToFolderToSaveWithNoLoader(Game game)
        {
            if (PluginSettings.Settings.EnableFolderToSave)
            {
                try
                {
                    if (PluginSettings.Settings.FolderToSave.IsNullOrEmpty() || PluginSettings.Settings.FileSavePattern.IsNullOrEmpty())
                    {
                        Logger.Error("No settings to use folder to save");
                        API.Instance.Notifications.Add(new NotificationMessage(
                            $"{PluginName}-MoveToFolderToSave-Errors",
                            $"{PluginName}\r\n" + ResourceProvider.GetString("LOCSsvMoveToFolderToSaveError"),
                            NotificationType.Error
                        ));
                    }
                    else
                    {
                        // Refresh data
                        GameSettings gameSettings = GetGameSettings(game.Id);
                        if (gameSettings != null)
                        {
                            SetDataFromSettings(gameSettings);
                        }

                        string pathFolder = PluginSettings.Settings.FolderToSave;
                        if (!PluginSettings.Settings.FolderToSave.Contains("{Name}"))
                        {
                            pathFolder = Path.Combine(pathFolder, "{Name}");
                        }
                        pathFolder = CommonPluginsStores.PlayniteTools.StringExpandWithStores(game, pathFolder);
                        pathFolder = CommonPluginsShared.Paths.GetSafePath(pathFolder, false);

                        GameScreenshots gameScreenshots = Get(game);
                        int digit = 1;

                        FileSystem.CreateDirectory(pathFolder);

                        bool haveDigit = false;
                        foreach (Screenshot screenshot in gameScreenshots.Items)
                        {
                            string pattern = CommonPluginsStores.PlayniteTools.StringExpandWithStores(game, PluginSettings.Settings.FileSavePattern);
                            string patternWithDigit = string.Empty;

                            if (File.Exists(screenshot.FileName) && !screenshot.FileName.Contains(pathFolder, StringComparison.InvariantCultureIgnoreCase))
                            {
                                string ext = Path.GetExtension(screenshot.FileName);

                                pattern = pattern.Replace("{DateModified}", screenshot.Modifed.ToString("yyyy-MM-dd"));
                                pattern = pattern.Replace("{DateTimeModified}", screenshot.Modifed.ToString("yyyy-MM-dd HH_mm_ss"));

                                if (pattern.Contains("{digit}"))
                                {
                                    haveDigit = true;
                                    patternWithDigit = pattern;
                                    pattern = patternWithDigit.Replace("{digit}", string.Format("{0:0000}", digit));
                                    digit++;
                                }

                                pattern = CommonPlayniteShared.Common.Paths.GetSafePathName(pattern);

                                string destFileName = Path.Combine(pathFolder, pattern);


                                // If file exists
                                if (File.Exists(destFileName + ext))
                                {
                                    if (haveDigit)
                                    {
                                        while (File.Exists(destFileName + ext))
                                        {
                                            pattern = patternWithDigit.Replace("{digit}", string.Format("{0:0000}", digit));
                                            pattern = CommonPlayniteShared.Common.Paths.GetSafePathName(pattern);
                                            destFileName = Path.Combine(pathFolder, pattern);
                                            digit++;
                                        }
                                    }
                                    else
                                    {
                                        while (File.Exists(destFileName + ext))
                                        {
                                            destFileName += $"({DateTime.Now.AddSeconds(digit).ToString("yyyy-MM-dd HH_mm_ss")})";
                                            digit++;
                                        }
                                    }
                                }

                                try
                                {
                                    File.Move(screenshot.FileName, destFileName + ext);
                                }
                                catch (Exception ex)
                                {
                                    Common.LogError(ex, false, true, PluginName);
                                    break;
                                }
                            }
                        }

                        // Refresh data
                        if (gameSettings != null)
                        {
                            SetDataFromSettings(gameSettings);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, PluginName);
                }
            }
        }

        #endregion

        #region Convert data

        /// <summary>
        /// Converts the specified screenshot image file to JPEG format using the configured JPEG quality.
        /// If the screenshot is not a video, the method attempts the conversion and preserves the original file's last write time.
        /// The original file is deleted after a successful conversion.
        /// Any errors encountered during the process are logged.
        /// </summary>
        /// <param name="screenshot">The screenshot to convert to JPEG format.</param>
        /// <returns>True if the conversion was successful; otherwise, false.</returns>
        private bool ConvertToJpg(Screenshot screenshot)
        {
            try
            {
                if (!screenshot.IsVideo)
                {
                    string oldFile = screenshot.FileName;
                    string newFile = ImageTools.ConvertToJpg(oldFile, PluginSettings.Settings.JpgQuality);

                    if (!newFile.IsNullOrEmpty())
                    {
                        DateTime dt = File.GetLastWriteTime(oldFile);
                        File.SetLastWriteTime(newFile, dt);
                        FileSystem.DeleteFileSafe(oldFile);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, false, PluginName);
            }

            return false;
        }

        /// <summary>
        /// Converts all screenshots associated with the specified game ID to JPEG format.
        /// This method retrieves the game by its ID and delegates the conversion logic to <see cref="ConvertGameSsvToJpg(Game)"/>.
        /// </summary>
        /// <param name="id">The unique identifier of the game whose screenshots will be converted.</param>
        /// <returns>True if at least one screenshot was successfully converted; otherwise, false.</returns>
        private bool ConvertGameSsvToJpg(Guid id)
        {
            return ConvertGameSsvToJpg(API.Instance.Database.Games.Get(id));
        }

        /// <summary>
        /// Converts all screenshots for the specified list of game IDs to JPEG format.
        /// This operation is performed within a global progress dialog, which allows the user to cancel the process.
        /// For each game ID, the method attempts to convert the screenshots using <see cref="ConvertGameSsvToJpg(Guid)"/>.
        /// After a successful conversion, the game data is refreshed from settings.
        /// Any exceptions encountered are logged. The total operation time and the number of processed items are also logged.
        /// </summary>
        /// <param name="ids">The list of game IDs whose screenshots will be converted to JPEG format.</param>
        public void ConvertGameSsvToJpg(List<Guid> ids)
        {
            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions($"{PluginName} - {ResourceProvider.GetString("LOCCommonConverting")}")
            {
                Cancelable = true,
                IsIndeterminate = ids.Count == 1
            };

            _ = API.Instance.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
            {
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();

                string cancelText = string.Empty;
                activateGlobalProgress.IsIndeterminate = true;
                if (ids.Count > 1)
                {
                    activateGlobalProgress.IsIndeterminate = false;
                    activateGlobalProgress.ProgressMaxValue = ids.Count;
                }

                Database.BeginBufferUpdate();

                try
                {
                    ids.ForEach(y =>
                    {
                        if (activateGlobalProgress.CancelToken.IsCancellationRequested)
                        {
                            cancelText = " canceled";
                            return;
                        }

                        activateGlobalProgress.Text = API.Instance.Database.Games.Get(y)?.Name;
                        if (ConvertGameSsvToJpg(y))
                        {
                            GameSettings gameSettings = GetGameSettings(y);
                            if (gameSettings != null)
                            {
                                SetDataFromSettings(gameSettings);
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, PluginName);
                }

                Database.EndBufferUpdate();

                stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed;
                Logger.Info($"Task ConvertGameSsvToJpg(){cancelText} - {string.Format("{0:00}:{1:00}.{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10)} for {ids.Count} items");
            }, globalProgressOptions);
        }

        /// <summary>
        /// Converts all screenshots associated with the specified game to JPEG format.
        /// For each screenshot that is not a video, the method attempts the conversion using <see cref="ConvertToJpg(Screenshot)"/>.
        /// Returns true if at least one screenshot was successfully converted; otherwise, false.
        /// </summary>
        /// <param name="game">The game whose screenshots will be converted to JPEG format.</param>
        /// <returns>True if at least one screenshot was converted; otherwise, false.</returns>
        public bool ConvertGameSsvToJpg(Game game)
        {
            bool hasConverted = false;
            if (game != null)
            {
                GameScreenshots data = Get(game);
                if (data.HasData)
                {
                    data.Items.ForEach(x =>
                    {
                        if (ConvertToJpg(x))
                        {
                            hasConverted = true;
                        }
                    });
                }
            }
            return hasConverted;
        }

        #endregion

        /// <summary>
        /// Retrieves the <see cref="GameSettings"/> object for the specified game ID.
        /// If no specific settings exist for the game, a new <see cref="GameSettings"/> instance is created using global folder settings.
        /// Ensures that all relevant global folder settings are present in the returned object.
        /// </summary>
        /// <param name="id">The unique identifier of the game.</param>
        /// <returns>The <see cref="GameSettings"/> for the specified game.</returns>
        public GameSettings GetGameSettings(Guid id)
        {
            List<FolderSettings> folderSettingsGlobal = new List<FolderSettings>();

            if (PluginSettings.Settings.EnableFolderToSave && !PluginSettings.Settings.FolderToSave.IsNullOrEmpty())
            {
                folderSettingsGlobal.Add(new FolderSettings
                {
                    ScreenshotsFolder = PluginSettings.Settings.FolderToSave,
                    UsedFilePattern = true,
                    FilePattern = PluginSettings.Settings.FileSavePattern
                });
            }

            if (!PluginSettings.Settings.GlobalScreenshootsPath.IsNullOrEmpty())
            {
                folderSettingsGlobal.Add(new FolderSettings
                {
                    ScreenshotsFolder = PluginSettings.Settings.GlobalScreenshootsPath
                });
            }


            GameSettings gameSettings = PluginSettings.Settings.gameSettings.Find(x => x.Id == id);
            if (gameSettings == null)
            {
                gameSettings = new GameSettings
                {
                    Id = id,
                    ScreenshotsFolders = folderSettingsGlobal
                };
            }
            else
            {
                foreach (FolderSettings folderSettings in folderSettingsGlobal)
                {
                    FolderSettings finded = gameSettings.ScreenshotsFolders
                        .Find(x => x.ScreenshotsFolder.IsEqual(folderSettings.ScreenshotsFolder)
                                    && x.UsedFilePattern == folderSettings.UsedFilePattern
                                    && x.FilePattern.IsEqual(folderSettings.FilePattern));

                    if (finded == null)
                    {
                        _ = gameSettings.ScreenshotsFolders.AddMissing(folderSettings);
                    }
                }
            }
            return gameSettings;
        }

        public override GameScreenshots Get(Guid id, bool onlyCache = false, bool force = false)
        {
            GameScreenshots gameScreenshots = base.GetOnlyCache(id);

            if (gameScreenshots == null)
            {
                Game game = API.Instance.Database.Games.Get(id);
                if (game != null)
                {
                    gameScreenshots = GetDefault(game);
                    Add(gameScreenshots);
                }
            }

            return gameScreenshots;
        }

        /// <summary>
        /// Updates the screenshot data for the specified game based on the provided <see cref="GameSettings"/>.
        /// For each configured screenshots folder, the method scans for supported image and video files,
        /// applies file pattern matching if enabled, and updates the game's screenshot collection accordingly.
        /// Also ensures that video metadata (thumbnail, duration, size) is generated.
        /// Any errors encountered during the process are logged.
        /// </summary>
        /// <param name="item">The <see cref="GameSettings"/> containing folder and pattern information for the game.</param>
        public void SetDataFromSettings(GameSettings item)
        {
            _ = SpinWait.SpinUntil(() => API.Instance.Database.IsOpen, -1);

            Game game = API.Instance.Database.Games.Get(item.Id);
            if (game == null)
            {
                Logger.Warn($"Game not found for {item.Id}");
                return;
            }

            GameScreenshots gameScreenshots = GetDefault(game);
            try
            {
                gameScreenshots.ScreenshotsFolders = item.GetScreenshotsFolders();
                gameScreenshots.InSettings = true;

                foreach (FolderSettings screenshotsFolder in item.ScreenshotsFolders)
                {
                    try
                    {
                        if (screenshotsFolder?.ScreenshotsFolder == null || screenshotsFolder.ScreenshotsFolder.IsNullOrEmpty())
                        {
                            Logger.Warn($"Screenshots directory is empty for {game.Name}");
                            return;
                        }

                        string pathFolder = CommonPluginsStores.PlayniteTools.StringExpandWithStores(game, screenshotsFolder.ScreenshotsFolder);
                        pathFolder = CommonPluginsShared.Paths.GetSafePath(pathFolder, false);

                        // Get files
                        string[] extensions = { ".jpg", ".jpeg", ".webp", ".png", ".gif", ".bmp", ".jfif", ".tga", ".mp4", ".avi", ".mkv", ".webm" };
                        if (Directory.Exists(pathFolder))
                        {
                            SearchOption searchOption = SearchOption.TopDirectoryOnly;
                            if (screenshotsFolder.ScanSubFolders)
                            {
                                searchOption = SearchOption.AllDirectories;
                            }

                            Directory.EnumerateFiles(pathFolder, "*.*", searchOption)
                                .Where(s => extensions.Any(ext => ext == Path.GetExtension(s)))
                                .ForEach(objectFile =>
                                {
                                    try
                                    {
                                        DateTime Modified = File.GetLastWriteTime(objectFile);

                                        if (screenshotsFolder.UsedFilePattern)
                                        {
                                            string pattern = CommonPluginsStores.PlayniteTools.StringExpandWithStores(game, screenshotsFolder.FilePattern);
                                            pattern = EscapeRegexSpecialChars(pattern);
                                            pattern = pattern.Replace("\\{digit\\}", @"\d*");
                                            pattern = pattern.Replace("\\{DateModified\\}", @"[0-9]{4}[-_][0-9]{2}[-_][0-9]{2}");
                                            pattern = pattern.Replace("\\{DateTimeModified\\}", @"[0-9]{4}[-_][0-9]{2}[-_][0-9]{2}[ -_][0-9]{2}[-_][0-9]{2}[-_][0-9]{2}");

                                            string gameName = API.Instance.ExpandGameVariables(game, "{Name}");
                                            string goodName = CommonPluginsShared.Paths.GetSafePathName(gameName).Replace(" ", "[ ]*");
                                            pattern = pattern.Replace(gameName, goodName);

                                            string fileName = Path.GetFileNameWithoutExtension(objectFile);

                                            if (Regex.IsMatch(fileName, pattern, RegexOptions.IgnoreCase))
                                            {
                                                gameScreenshots.Items.Add(new Screenshot
                                                {
                                                    FileName = objectFile,
                                                    Modifed = Modified
                                                });
                                            }
                                        }
                                        else
                                        {
                                            gameScreenshots.Items.Add(new Screenshot
                                            {
                                                FileName = objectFile,
                                                Modifed = Modified,
                                            });
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Common.LogError(ex, false, true, PluginName);
                                    }
                                });
                        }
                        else
                        {
                            Logger.Warn($"Screenshots directory not found for {game.Name} - {pathFolder}");
                        }

                        IEnumerable<Screenshot> elements = gameScreenshots?.Items?.Where(x => x != null);
                        if (elements?.Count() > 0)
                        {
                            elements = elements?.GroupBy(x => x.FileName)?.Select(g => g.First());

                            gameScreenshots.DateLastRefresh = DateTime.Now;
                            gameScreenshots.Items = elements.ToList();

                            // Force generation of data from video
                            gameScreenshots.Items.Where(x => x.IsVideo).ForEach(x =>
                            {
                                string thumb = x.Thumbnail;
                                string duration = x.DurationString;
                                string size = x.SizeString;
                            });
                        }

                        AddOrUpdate(gameScreenshots);
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false, $"Error on {game.Name} for {screenshotsFolder.ScreenshotsFolder}", true, PluginName);
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginName);
            }
        }

        private static string EscapeRegexSpecialChars(string input)
        {
            string specialChars = @".^$*+?(){}[]|\";
            StringBuilder escapedString = new StringBuilder();

            foreach (char c in input)
            {
                if (specialChars.Contains(c))
                {
                    _ = escapedString.Append('\\');
                }
                _ = escapedString.Append(c);
            }

            return escapedString.ToString();
        }

        #region Tag

        public override void AddTag(Game game)
        {
            GameScreenshots item = Get(game, true);
            if (item.HasData)
            {
                try
                {
                    Guid? TagId = FindGoodPluginTags(ResourceProvider.GetString("LOCSsvTitle"));
                    if (TagId != null)
                    {
                        if (game.TagIds != null)
                        {
                            game.TagIds.Add((Guid)TagId);
                        }
                        else
                        {
                            game.TagIds = new List<Guid> { (Guid)TagId };
                        }
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, $"Tag insert error with {game.Name}", true, PluginName, string.Format(ResourceProvider.GetString("LOCCommonNotificationTagError"), game.Name));
                    return;
                }
            }
            else if (TagMissing)
            {
                if (game.TagIds != null)
                {
                    game.TagIds.Add((Guid)AddNoDataTag());
                }
                else
                {
                    game.TagIds = new List<Guid> { (Guid)AddNoDataTag() };
                }
            }

            API.Instance.MainView.UIDispatcher?.Invoke(() =>
            {
                API.Instance.Database.Games.Update(game);
                game.OnPropertyChanged();
            });
        }

        #endregion

        public override void SetThemesResources(Game game)
        {
            GameScreenshots gameScreenshots = Get(game, true);
            PluginSettings.Settings.HasData = gameScreenshots?.HasData ?? false;
            PluginSettings.Settings.ListScreenshots = gameScreenshots?.Items ?? new List<Screenshot>();
        }

        /// <summary>
        /// Handles the mouse left button down event on a ListBox item containing screenshots.
        /// If the selection or a double-click is detected (depending on settings), opens a window to display the selected screenshot in a viewer.
        /// The viewer allows navigation through all screenshots in the ListBox.
        /// </summary>
        /// <param name="sender">The ListBox control that triggered the event.</param>
        /// <param name="e">The mouse button event arguments.</param>
        public void ListBoxItem_MouseLeftButtonDownClick(object sender, MouseButtonEventArgs e)
        {
            ListBox listBox = (ListBox)sender;
            if (ItemsControl.ContainerFromElement(listBox, e.OriginalSource as DependencyObject) is ListBoxItem)
            {
                int index = listBox.SelectedIndex;
                if (index == -1)
                {
                    return;
                }

                Screenshot screenshot = (Screenshot)listBox.Items[index];

                bool isGood = false;

                if (PluginSettings.Settings.OpenViewerWithOnSelection)
                {
                    isGood = true;
                }
                else
                {
                    if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
                    {
                        isGood = true;
                    }
                }

                if (isGood)
                {
                    WindowOptions windowOptions = new WindowOptions
                    {
                        ShowMinimizeButton = false,
                        ShowMaximizeButton = true,
                        ShowCloseButton = true,
                        CanBeResizable = true,
                        Height = 720,
                        Width = 1280
                    };

                    SsvSinglePictureView viewExtension = new SsvSinglePictureView(screenshot, listBox.Items.Cast<Screenshot>().ToList());
                    Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(ResourceProvider.GetString("LOCSsv") + " - " + screenshot.FileNameOnly, viewExtension, windowOptions);
                    _ = windowExtension.ShowDialog();
                }
            }
        }
    }
}