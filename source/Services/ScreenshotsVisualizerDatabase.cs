using CommonPluginsShared;
using CommonPluginsShared.Collections;
using CommonPluginsShared.Extensions;
using Playnite.SDK;
using Playnite.SDK.Data;
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
using CommonPluginsShared.IO;
using CommonPlayniteShared.Common;

namespace ScreenshotsVisualizer.Services
{
    public class ScreenshotsVisualizerDatabase : PluginDatabaseObject<ScreenshotsVisualizerSettings, GameScreenshots, Screenshot>
    {
        private readonly SsvPathResolver _pathResolver;
        private readonly ImageMagickConversionService _imageMagickConversionService;
        private readonly SsvThumbnailService _thumbnailService;
        private readonly SsvVideoMetadataService _videoMetadataService;

        /// <summary>
        /// Gets the thumbnail generation service used for image and video cache paths.
        /// </summary>
        public SsvThumbnailService ThumbnailService => _thumbnailService;

        /// <summary>
        /// Gets the ffprobe metadata cache service for video duration and resolution.
        /// </summary>
        public SsvVideoMetadataService VideoMetadataService => _videoMetadataService;

        public ScreenshotsVisualizerDatabase(ScreenshotsVisualizerSettings pluginSettings, string pluginUserDataPath) : base(pluginSettings, "ScreenshotsVisualizer", pluginUserDataPath)
        {
            TagBefore = "[SSV]";
            PluginWindows = new ScreenshotsVisualizerWindows(PluginName, this);
            PluginExportCsv = new ScreenshotsVisualizerExport();
            _pathResolver = new SsvPathResolver();
            _imageMagickConversionService = new ImageMagickConversionService(PluginName);
            _thumbnailService = new SsvThumbnailService(PluginName);
            _videoMetadataService = new SsvVideoMetadataService(PluginName, _thumbnailService);
        }

        #region Logging

        private void LogError(Exception ex, string context = null, bool showNotification = true, string notificationMessage = null)
        {
            if (notificationMessage != null)
            {
                Common.LogError(ex, false, context ?? string.Empty, showNotification, PluginName, notificationMessage);
            }
            else
            {
                Common.LogError(ex, false, context ?? string.Empty, showNotification, PluginName);
            }
        }

        private static void LogScanDebug(string message)
        {
            Common.LogDebug(true, string.Format("[SsvDatabase] {0}", message));
        }

        private static void LogDeleteDebug(string message)
        {
            Common.LogDebug(true, string.Format("[SsvDelete] {0}", message));
        }

        private static void LogDeleteInfo(string message)
        {
            Logger.Info(string.Format("[SsvDelete] {0}", message));
        }

        private static void LogDeleteWarn(string message)
        {
            Logger.Warn(string.Format("[SsvDelete] {0}", message));
        }

        private static void LogScanWarn(string message)
        {
            Logger.Warn(string.Format("[SsvDatabase] {0}", message));
        }

        private static string GetConversionProfileLogLabel(SsvImageConversionCustomCmd command)
        {
            if (command == null)
            {
                return "null";
            }

            if (!string.IsNullOrWhiteSpace(command.Name))
            {
                return command.Name;
            }

            return string.IsNullOrWhiteSpace(command.OutputFormat) ? "unnamed" : command.OutputFormat;
        }

        private static int CountImageScreenshots(GameScreenshots data)
        {
            if (data?.Items == null)
            {
                return 0;
            }

            return data.Items.Count(x => !x.IsVideo);
        }

        private int CountImageScreenshots(IEnumerable<Guid> ids)
        {
            if (ids == null)
            {
                return 0;
            }

            int total = 0;
            foreach (Guid id in ids)
            {
                GameScreenshots data = Get(id);
                if (data.HasData)
                {
                    total += CountImageScreenshots(data);
                }
            }

            return total;
        }

        private static void AdvanceConversionFileProgress(
            GlobalProgressActionArgs progress,
            Game game,
            Screenshot screenshot)
        {
            if (progress == null)
            {
                return;
            }

            string fileName = Path.GetFileName(screenshot?.FileName);
            string gameName = game?.Name ?? string.Empty;
            progress.Text = string.IsNullOrEmpty(fileName)
                ? gameName
                : string.Format("{0} — {1}", gameName, fileName);
            progress.CurrentProgressValue++;
        }

        #endregion

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
                List<GameScreenshots> allItems = GetAllCache().ToList();
                activateGlobalProgress.ProgressMaxValue = allItems.Count;

                foreach (GameScreenshots item in allItems)
                {
                    if (activateGlobalProgress.CancelToken.IsCancellationRequested)
                    {
                        cancelText = " canceled";
                        break;
                    }

                    try
                    {
                        MoveToFolderToSaveWithNoLoader(item.Id, activateGlobalProgress);
                    }
                    catch (Exception ex)
                    {
                        LogError(ex);
                    }
                    activateGlobalProgress.CurrentProgressValue++;
                }

                stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed;
                LogScanDebug(string.Format(
                    "MoveToFolderToSaveAll{0} - {1:00}:{2:00}.{3:00}",
                    cancelText,
                    ts.Minutes,
                    ts.Seconds,
                    ts.Milliseconds / 10));
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

                _database.BeginBufferUpdate();

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
                    LogError(ex);
                }

                _database.EndBufferUpdate();

                stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed;
                LogScanDebug(string.Format(
                    "Task MoveToFolderToSave(){0} - {1:00}:{2:00}.{3:00} for {4}/{5} items",
                    cancelText,
                    ts.Minutes,
                    ts.Seconds,
                    ts.Milliseconds / 10,
                    activateGlobalProgress.CurrentProgressValue,
                    ids.Count));
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
            if (PluginSettings.EnableFolderToSave)
            {
                try
                {
                    if (PluginSettings.FolderToSave.IsNullOrEmpty() || PluginSettings.FileSavePattern.IsNullOrEmpty())
                    {
                        LogScanWarn("No settings to use folder to save (global FolderToSave / FileSavePattern)");
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

                        string pathFolder = PluginSettings.FolderToSave;
                        if (!PluginSettings.FolderToSave.Contains("{Name}"))
                        {
                            pathFolder = Path.Combine(pathFolder, "{Name}");
                        }
                        pathFolder = CommonPluginsStores.PlayniteTools.StringExpandWithStores(game, pathFolder);
                        pathFolder = PathValidator.GetSafePath(pathFolder, false);

                        LogScanDebug(string.Format(
                            "MoveToFolderToSave started for '{0}' — destination '{1}' (global FolderToSave / FileSavePattern)",
                            game.Name,
                            pathFolder));

                        GameScreenshots gameScreenshots = Get(game);
                        int digit = 1;
                        int movedCount = 0;

                        FileSystem.CreateDirectory(pathFolder);

                        bool haveDigit = false;
                        foreach (Screenshot screenshot in gameScreenshots.Items)
                        {
                            string pattern = CommonPluginsStores.PlayniteTools.StringExpandWithStores(game, PluginSettings.FileSavePattern);
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
                                    movedCount++;
                                }
                                catch (Exception ex)
                                {
                                    LogError(ex);
                                    break;
                                }
                            }
                        }

                        LogScanDebug(string.Format(
                            "MoveToFolderToSave completed for '{0}' — {1} file(s) moved to '{2}'",
                            game.Name,
                            movedCount,
                            pathFolder));

                        // Refresh data
                        if (gameSettings != null)
                        {
                            SetDataFromSettings(gameSettings);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogError(ex);
                }
            }
        }

        #endregion

        #region Convert data

        /// <summary>
        /// Converts a single screenshot image using ImageMagick and the supplied profile.
        /// Videos are ignored.
        /// </summary>
        /// <param name="screenshot">Screenshot to convert.</param>
        /// <param name="command">Conversion profile.</param>
        /// <returns><c>true</c> when conversion succeeded.</returns>
        private bool ConvertScreenshot(Screenshot screenshot, SsvImageConversionCustomCmd command)
        {
            if (screenshot == null || screenshot.IsVideo || command == null)
            {
                return false;
            }

            try
            {
                SsvImageConversionResult result = _imageMagickConversionService.TryConvert(
                    PluginSettings.ImageMagickPath,
                    command,
                    screenshot.FileName);

                if (!result.Success)
                {
                    if (!result.ImageMagickNotFound && !string.IsNullOrEmpty(result.ErrorMessage))
                    {
                        LogScanWarn(string.Format(
                            "Image conversion failed for '{0}': {1}",
                            screenshot.FileName,
                            result.ErrorMessage));
                    }

                    return false;
                }

                LogScanDebug(string.Format(
                    "Image converted — profile '{0}': '{1}' -> '{2}'",
                    GetConversionProfileLogLabel(command),
                    result.InputPath ?? screenshot.FileName,
                    result.OutputPath ?? screenshot.FileName));

                if (!string.IsNullOrEmpty(result.OutputPath)
                    && !string.Equals(result.OutputPath, screenshot.FileName, StringComparison.OrdinalIgnoreCase))
                {
                    screenshot.FileName = result.OutputPath;
                }

                return true;
            }
            catch (Exception ex)
            {
                LogError(ex, null, false);
                return false;
            }
        }

        /// <summary>
        /// Converts all screenshots for the specified game using the supplied ImageMagick profile.
        /// </summary>
        /// <param name="game">Game whose screenshots will be converted.</param>
        /// <param name="command">Conversion profile.</param>
        /// <param name="progress">Optional progress dialog updated once per image file.</param>
        /// <returns><c>true</c> when at least one screenshot was converted.</returns>
        public bool ConvertGameScreenshots(
            Game game,
            SsvImageConversionCustomCmd command,
            GlobalProgressActionArgs progress = null)
        {
            if (game == null || command == null)
            {
                return false;
            }

            bool hasConverted = false;
            GameScreenshots data = Get(game);
            if (!data.HasData)
            {
                LogScanDebug(string.Format(
                    "ConvertGameScreenshots skipped for '{0}' — no screenshots in cache",
                    game.Name));
                return false;
            }

            int videoCount = data.Items.Count(x => x.IsVideo);
            int imageCount = data.Items.Count - videoCount;
            LogScanDebug(string.Format(
                "ConvertGameScreenshots started for '{0}' — profile '{1}' ({2}), {3} image(s), {4} video(s) skipped",
                game.Name,
                GetConversionProfileLogLabel(command),
                command.OutputFormat ?? string.Empty,
                imageCount,
                videoCount));

            int convertedCount = 0;
            int skippedOutputFormatCount = 0;
            foreach (Screenshot screenshot in data.Items)
            {
                if (progress?.CancelToken.IsCancellationRequested == true)
                {
                    break;
                }

                if (screenshot.IsVideo)
                {
                    continue;
                }

                if (command.IsAlreadyOutputFormat(screenshot.FileName))
                {
                    skippedOutputFormatCount++;
                    LogScanDebug(string.Format(
                        "Image conversion skipped — profile '{0}': '{1}' already matches output format '{2}'",
                        GetConversionProfileLogLabel(command),
                        screenshot.FileName,
                        command.OutputFormat ?? string.Empty));
                    AdvanceConversionFileProgress(progress, game, screenshot);
                    continue;
                }

                if (ConvertScreenshot(screenshot, command))
                {
                    hasConverted = true;
                    convertedCount++;
                }

                AdvanceConversionFileProgress(progress, game, screenshot);
            }

            LogScanDebug(string.Format(
                "ConvertGameScreenshots completed for '{0}' — profile '{1}', {2} file(s) converted, {3} already in target format",
                game.Name,
                GetConversionProfileLogLabel(command),
                convertedCount,
                skippedOutputFormatCount));

            return hasConverted;
        }

        /// <summary>
        /// Converts all screenshots for the specified game identifier using the supplied profile.
        /// </summary>
        /// <param name="id">Playnite game identifier.</param>
        /// <param name="command">Conversion profile.</param>
        /// <param name="progress">Optional progress dialog updated once per image file.</param>
        /// <returns><c>true</c> when at least one screenshot was converted.</returns>
        private bool ConvertGameScreenshots(
            Guid id,
            SsvImageConversionCustomCmd command,
            GlobalProgressActionArgs progress = null)
        {
            return ConvertGameScreenshots(API.Instance.Database.Games.Get(id), command, progress);
        }

        /// <summary>
        /// Converts screenshots for the specified games using ImageMagick.
        /// Shows a cancellable progress dialog and refreshes game data after each successful conversion.
        /// </summary>
        /// <param name="ids">Game identifiers to process.</param>
        /// <param name="command">Conversion profile.</param>
        public void ConvertGameScreenshots(List<Guid> ids, SsvImageConversionCustomCmd command)
        {
            if (ids == null || ids.Count == 0 || command == null)
            {
                return;
            }

            string imageMagickPath = PluginSettings.ImageMagickPath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(imageMagickPath) || !File.Exists(imageMagickPath))
            {
                LogScanWarn(string.Format(
                    "ConvertGameScreenshots — ImageMagick executable not found (path: '{0}')",
                    imageMagickPath));
            }

            int totalImageFiles = CountImageScreenshots(ids);
            LogScanDebug(string.Format(
                "Task ConvertGameScreenshots started — profile '{0}' ({1}), {2} game(s), {3} image file(s), ImageMagick path '{4}'",
                GetConversionProfileLogLabel(command),
                command.OutputFormat ?? string.Empty,
                ids.Count,
                totalImageFiles,
                imageMagickPath));

            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions($"{PluginName} - {ResourceProvider.GetString("LOCCommonConverting")}")
            {
                Cancelable = true,
                IsIndeterminate = totalImageFiles <= 0
            };

            _ = API.Instance.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
            {
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();

                string cancelText = string.Empty;
                activateGlobalProgress.IsIndeterminate = totalImageFiles <= 0;
                if (totalImageFiles > 0)
                {
                    activateGlobalProgress.ProgressMaxValue = totalImageFiles;
                }

                _database.BeginBufferUpdate();

                try
                {
                    foreach (Guid gameId in ids)
                    {
                        if (activateGlobalProgress.CancelToken.IsCancellationRequested)
                        {
                            cancelText = " canceled";
                            break;
                        }

                        if (ConvertGameScreenshots(gameId, command, activateGlobalProgress))
                        {
                            GameSettings gameSettings = GetGameSettings(gameId);
                            if (gameSettings != null)
                            {
                                SetDataFromSettings(gameSettings);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogError(ex);
                }

                _database.EndBufferUpdate();

                stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed;
                LogScanDebug(string.Format(
                    "Task ConvertGameScreenshots(){0} - {1:00}:{2:00}.{3:00} for {4} image file(s)",
                    cancelText,
                    ts.Minutes,
                    ts.Seconds,
                    ts.Milliseconds / 10,
                    totalImageFiles));
            }, globalProgressOptions);
        }

        #endregion

        /// <summary>
        /// Retrieves effective <see cref="GameSettings"/> for the specified game ID.
        /// Merges persisted per-game folders with applicable global screenshot sources (when allowed)
        /// and the global archive folder at runtime only.
        /// Global sources are included only when <see cref="SsvGlobalSourceApplicabilityHelper.MatchesGame"/> passes.
        /// The global archive configuration is never injected into persisted <c>gameSettings</c>.
        /// </summary>
        /// <param name="id">The unique identifier of the game.</param>
        /// <returns>Effective game settings for scan and refresh operations.</returns>
        public GameSettings GetGameSettings(Guid id)
        {
            FolderSettings globalArchiveFolder = SsvArchiveFolderHelper.TryCreateGlobalArchiveFolderSettings(PluginSettings);
            List<FolderSettings> globalSourcesToMerge = new List<FolderSettings>();
            Game game = API.Instance.Database.Games.Get(id);

            GameSettings gameSettings = PluginSettings.gameSettings.Find(x => x.Id == id);
            bool overrideGlobalConfigs = gameSettings?.OverrideGlobalConfigs ?? false;
            if (!overrideGlobalConfigs)
            {
                foreach (FolderSettings globalSource in PluginSettings.GetEffectiveGlobalScreenshotSources())
                {
                    if (globalSource == null || globalSource.ScreenshotsFolder.IsNullOrEmpty())
                    {
                        continue;
                    }

                    if (game == null || !SsvGlobalSourceApplicabilityHelper.MatchesGame(game, globalSource))
                    {
                        continue;
                    }

                    globalSourcesToMerge.Add(globalSource.Clone());
                }
            }

            if (gameSettings == null)
            {
                gameSettings = new GameSettings
                {
                    Id = id,
                    OverrideGlobalConfigs = false,
                    ScreenshotsFolders = globalSourcesToMerge
                };
            }
            else
            {
                if (gameSettings.ScreenshotsFolders == null)
                {
                    gameSettings.ScreenshotsFolders = new List<FolderSettings>();
                }

                foreach (FolderSettings folderSettings in globalSourcesToMerge)
                {
                    FolderSettings finded = gameSettings.ScreenshotsFolders
                        .Find(x => x.ScreenshotsFolder.IsEqual(folderSettings.ScreenshotsFolder)
                                    && x.UsedFilePattern == folderSettings.UsedFilePattern
                                    && x.FilePattern.IsEqual(folderSettings.FilePattern));

                    if (finded == null)
                    {
                        gameSettings.ScreenshotsFolders.Add(folderSettings);
                    }
                }
            }

            return AppendGlobalArchiveFolderForRuntime(gameSettings, globalArchiveFolder);
        }

        /// <summary>
        /// Returns a copy of <paramref name="settings"/> that includes the global archive folder for runtime scan when needed.
        /// Does not mutate persisted game settings.
        /// </summary>
        /// <param name="settings">Base game settings.</param>
        /// <param name="globalArchiveFolder">Global archive folder settings.</param>
        /// <returns>Effective settings for scan, including archive when applicable.</returns>
        private static GameSettings AppendGlobalArchiveFolderForRuntime(GameSettings settings, FolderSettings globalArchiveFolder)
        {
            if (settings == null || globalArchiveFolder == null)
            {
                return settings;
            }

            if (settings.ScreenshotsFolders != null
                && settings.ScreenshotsFolders.Exists(x => SsvArchiveFolderHelper.IsStrictArchiveFolderMatch(x, globalArchiveFolder)))
            {
                return settings;
            }

            GameSettings effectiveSettings = Serialization.GetClone(settings);
            if (effectiveSettings.ScreenshotsFolders == null)
            {
                effectiveSettings.ScreenshotsFolders = new List<FolderSettings>();
            }

            effectiveSettings.ScreenshotsFolders.Insert(0, globalArchiveFolder);
            return effectiveSettings;
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

        /// <inheritdoc />
        /// <remarks>
        /// Scans configured screenshot folders and updates the plugin database entry.
        /// Invoked by <c>RefreshNoLoader</c> after menu or batch refresh actions.
        /// </remarks>
        public override void ActionAfterRefresh(GameScreenshots item)
        {
            if (item == null)
            {
                return;
            }

            GameSettings gameSettings = GetGameSettings(item.Id);
            if (gameSettings?.ScreenshotsFolders == null || gameSettings.ScreenshotsFolders.Count == 0)
            {
                LogScanDebug(string.Format(
                    "ActionAfterRefresh skipped for '{0}' — no screenshot folders configured",
                    item.Name));
                return;
            }

            LogScanDebug(string.Format("ActionAfterRefresh — scanning '{0}'", item.Name));
            SetDataFromSettings(gameSettings);
        }

        /// <summary>
        /// Updates the screenshot data for the specified game based on the provided <see cref="GameSettings"/>.
        /// For each configured screenshots folder, the method scans for supported image and video files,
        /// applies file pattern matching if enabled, and updates the game's screenshot collection accordingly.
        /// Also ensures that image and video thumbnails are generated, and video metadata (duration, size) is resolved.
        /// Any errors encountered during the process are logged.
        /// </summary>
        /// <param name="item">The <see cref="GameSettings"/> containing folder and pattern information for the game.</param>
        public void SetDataFromSettings(GameSettings item)
        {
            _ = SpinWait.SpinUntil(() => API.Instance.Database.IsOpen, -1);
            Stopwatch scanStopwatch = Stopwatch.StartNew();

            Game game = API.Instance.Database.Games.Get(item.Id);
            if (game == null)
            {
                LogScanWarn(string.Format("Game not found for {0}", item.Id));
                return;
            }

            LogScanDebug(string.Format(
                "SetDataFromSettings started for '{0}' ({1} folder(s))",
                game.Name,
                item.ScreenshotsFolders?.Count ?? 0));

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
                            LogScanWarn(string.Format(
                                "Screenshots directory is not configured for '{0}' (empty folder entry)",
                                game.Name));
                            continue;
                        }

                        string pathFolder = _pathResolver.ResolvePath(game, screenshotsFolder);

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
                                            string pattern = _pathResolver.ResolveFilePatternRegex(game, screenshotsFolder);

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
                                        LogError(ex);
                                    }
                                });
                        }
                        else
                        {
                            LogScanWarn(string.Format(
                                "Screenshots directory not found for '{0}' — configured '{1}', resolved '{2}'",
                                game.Name,
                                screenshotsFolder.ScreenshotsFolder,
                                pathFolder));
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError(ex, string.Format("Error on {0} for {1}", game.Name, screenshotsFolder.ScreenshotsFolder));
                    }
                }

                IEnumerable<Screenshot> elements = gameScreenshots?.Items?.Where(x => x != null);
                if (elements?.Count() > 0)
                {
                    elements = elements.GroupBy(x => x.FileName).Select(g => g.First());
                    gameScreenshots.DateLastRefresh = DateTime.Now;
                    gameScreenshots.Items = elements.ToList();

                    int imageCount = gameScreenshots.Items.Count(x => !x.IsVideo);
                    int videoCount = gameScreenshots.Items.Count(x => x.IsVideo);

                    // Force generation of data from image
                    Stopwatch imageThumbnailStopwatch = Stopwatch.StartNew();
                    gameScreenshots.Items.Where(x => !x.IsVideo).ForEach(x =>
                    {
                        string imageThumb = x.ImageThumbnail;
                    });
                    imageThumbnailStopwatch.Stop();
                    Common.LogDebug(true, string.Format(
                        "[SsvThumbnail] Image pre-generation for '{0}' completed ({1} image(s), {2} ms)",
                        game.Name,
                        imageCount,
                        imageThumbnailStopwatch.ElapsedMilliseconds));

                    // Force generation of data from video
                    Stopwatch videoThumbnailStopwatch = Stopwatch.StartNew();
                    gameScreenshots.Items.Where(x => x.IsVideo).ForEach(x =>
                    {
                        string thumb = x.Thumbnail;
                        string duration = x.DurationString;
                        string size = x.SizeString;
                    });
                    videoThumbnailStopwatch.Stop();
                    Common.LogDebug(true, string.Format(
                        "[SsvThumbnail] Video pre-generation for '{0}' completed ({1} video(s), {2} ms)",
                        game.Name,
                        videoCount,
                        videoThumbnailStopwatch.ElapsedMilliseconds));
                }

                AddOrUpdate(gameScreenshots);

                if (GameContext?.Id == game.Id)
                {
                    API.Instance.MainView.UIDispatcher?.BeginInvoke((Action)(() => SetThemesResources(game)));
                }

                LogScanDebug(string.Format(
                    "SetDataFromSettings completed for '{0}' ({1} item(s))",
                    game.Name,
                    Get(game, true)?.Items?.Count ?? 0));
                scanStopwatch.Stop();
                Common.LogDebug(true, string.Format(
                    "[SsvThumbnail] Scan duration for '{0}' completed ({1} ms)",
                    game.Name,
                    scanStopwatch.ElapsedMilliseconds));
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        #region Tag

        public new void AddTag(Game game)
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
                    LogError(ex, string.Format("Tag insert error with {0}", game.Name), true, string.Format(ResourceProvider.GetString("LOCCommonNotificationTagError"), game.Name));
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

        #region Screenshot delete

        /// <summary>
        /// Removes a screenshot from the plugin database and sends the source file to the recycle bin when present.
        /// The confirmation dialog is the caller's responsibility.
        /// </summary>
        /// <param name="gameId">Playnite game identifier owning the screenshot.</param>
        /// <param name="screenshot">Screenshot entry to remove.</param>
        /// <returns>Outcome of the operation.</returns>
        public SsvScreenshotDeleteResult TryDeleteScreenshot(Guid gameId, Screenshot screenshot)
        {
            if (screenshot == null)
            {
                LogDeleteWarn("TryDeleteScreenshot skipped — screenshot argument is null");
                return SsvScreenshotDeleteResult.ScreenshotNotInCollection;
            }

            LogDeleteInfo(string.Format(
                "TryDeleteScreenshot started for game {0}, file '{1}'",
                gameId,
                screenshot.FileNameOnly ?? screenshot.FileName ?? "(unknown)"));

            GameScreenshots gameScreenshots = GetOnlyCache(gameId);
            if (gameScreenshots == null)
            {
                LogDeleteWarn(string.Format("TryDeleteScreenshot aborted — no cache entry for game {0}", gameId));
                return SsvScreenshotDeleteResult.GameNotFound;
            }

            Screenshot itemToRemove = FindScreenshotInCollection(gameScreenshots, screenshot);
            if (itemToRemove == null)
            {
                LogDeleteWarn(string.Format(
                    "TryDeleteScreenshot aborted — screenshot not found in collection for game {0}",
                    gameId));
                return SsvScreenshotDeleteResult.ScreenshotNotInCollection;
            }

            string filePath = itemToRemove.FileName;
            bool physicalFileExists = !string.IsNullOrEmpty(filePath) && File.Exists(filePath);

            _ = gameScreenshots.Items.Remove(itemToRemove);
            Update(gameScreenshots);

            LogDeleteInfo(string.Format(
                "Database updated for game {0} — removed '{1}', {2} item(s) remaining",
                gameId,
                Path.GetFileName(filePath ?? string.Empty),
                gameScreenshots.Items?.Count ?? 0));

            if (Paths != null && !string.IsNullOrEmpty(Paths.PluginCachePath))
            {
                _thumbnailService.TryPurgeCachedThumbnailsForScreenshot(itemToRemove, Paths.PluginCachePath);
            }
            else
            {
                LogDeleteWarn("Thumbnail cache purge skipped — PluginCachePath is not available");
            }

            if (!physicalFileExists)
            {
                LogDeleteWarn(string.Format(
                    "Physical file missing — database entry removed only: '{0}'",
                    filePath ?? string.Empty));
                return SsvScreenshotDeleteResult.SkippedMissingPhysicalFile;
            }

            LogDeleteDebug(string.Format(
                "Scheduling recycle-bin delete for '{0}'",
                filePath));

            _ = Task.Run(() =>
            {
                if (WaitAndDeleteToRecycleBin(filePath))
                {
                    LogDeleteInfo(string.Format("Recycle-bin delete completed for '{0}'", filePath));
                }
                else
                {
                    LogError(
                        new IOException(string.Format("Failed to delete screenshot file '{0}'", filePath)),
                        "TryDeleteScreenshot");
                }
            });

            return SsvScreenshotDeleteResult.Success;
        }

        private static Screenshot FindScreenshotInCollection(GameScreenshots gameScreenshots, Screenshot screenshot)
        {
            if (gameScreenshots?.Items == null)
            {
                return null;
            }

            Screenshot itemToRemove = gameScreenshots.Items.FirstOrDefault(x => ReferenceEquals(x, screenshot));
            if (itemToRemove != null)
            {
                LogDeleteDebug("Screenshot matched in collection by object reference");
                return itemToRemove;
            }

            if (string.IsNullOrEmpty(screenshot.FileName))
            {
                return null;
            }

            itemToRemove = gameScreenshots.Items.FirstOrDefault(x =>
                x != null
                && string.Equals(x.FileName, screenshot.FileName, StringComparison.OrdinalIgnoreCase));

            if (itemToRemove != null)
            {
                LogDeleteDebug(string.Format(
                    "Screenshot matched in collection by FileName '{0}'",
                    screenshot.FileName));
            }

            return itemToRemove;
        }

        private static bool WaitAndDeleteToRecycleBin(string filePath, int maxAttempts = 30, int delayMs = 200)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                LogDeleteDebug(string.Format(
                    "WaitAndDeleteToRecycleBin skipped — file not present: '{0}'",
                    filePath ?? string.Empty));
                return true;
            }

            LogDeleteDebug(string.Format(
                "WaitAndDeleteToRecycleBin started for '{0}' (max {1} attempts, {2} ms delay)",
                filePath,
                maxAttempts,
                delayMs));

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (!IsScreenshotFileLocked(new FileInfo(filePath)))
                {
                    try
                    {
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                            filePath,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin,
                            Microsoft.VisualBasic.FileIO.UICancelOption.ThrowException);
                        LogDeleteDebug(string.Format(
                            "File sent to recycle bin on attempt {0}/{1}: '{2}'",
                            attempt + 1,
                            maxAttempts,
                            filePath));
                        return true;
                    }
                    catch (Exception ex)
                    {
                        if (attempt >= maxAttempts - 1)
                        {
                            Common.LogError(ex, false, true, "ScreenshotsVisualizer");
                            return false;
                        }

                        LogDeleteWarn(string.Format(
                            "Delete attempt {0}/{1} failed for '{2}' — retrying",
                            attempt + 1,
                            maxAttempts,
                            filePath));
                    }
                }
                else if (attempt == 0)
                {
                    LogDeleteWarn(string.Format(
                        "File locked on attempt {0}/{1} — waiting {2} ms: '{3}'",
                        attempt + 1,
                        maxAttempts,
                        delayMs,
                        filePath));
                }
                else if (attempt == maxAttempts - 1 || (attempt + 1) % 5 == 0)
                {
                    LogDeleteDebug(string.Format(
                        "File locked on attempt {0}/{1} — waiting {2} ms: '{3}'",
                        attempt + 1,
                        maxAttempts,
                        delayMs,
                        filePath));
                }

                Thread.Sleep(delayMs);
            }

            LogDeleteWarn(string.Format(
                "WaitAndDeleteToRecycleBin exhausted all attempts for '{0}'",
                filePath));
            return false;
        }

        private static bool IsScreenshotFileLocked(FileInfo file)
        {
            FileStream stream = null;

            try
            {
                stream = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                return true;
            }
            finally
            {
                stream?.Close();
            }

            return false;
        }

        #endregion

        public override void SetThemesResources(Game game)
        {
            GameScreenshots gameScreenshots = Get(game, true);
            PluginSettings.HasData = gameScreenshots?.HasData ?? false;
            PluginSettings.ListScreenshots = gameScreenshots?.Items ?? new List<Screenshot>();
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

                if (PluginSettings.OpenViewerWithOnSelection)
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
                    if (PluginSettings.UseExternalViewer)
                    {
                        ScreenshotsVisualizerWindows.OpenWithExternalViewer(screenshot.FileName);
                    }
                    else
                    {
                        ScreenshotsVisualizerWindows windows = PluginWindows as ScreenshotsVisualizerWindows;
                        if (windows != null)
                        {
                            windows.ShowSinglePictureWindow(screenshot, listBox.Items.Cast<Screenshot>().ToList());
                        }
                    }
                }
            }
        }
    }
}