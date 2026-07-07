using CommonPluginsShared.Extensions;
using CommonPluginsShared.IO;
using CommonPluginsStores;
using Playnite.SDK;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace ScreenshotsVisualizer.Services
{
    /// <summary>
    /// Shared helpers for global archive folder settings, strict duplicate detection, and migration cleanup.
    /// </summary>
    public static class SsvArchiveFolderHelper
    {
        /// <summary>
        /// Builds the global archive folder settings when archiving is enabled and configured.
        /// </summary>
        /// <param name="settings">Plugin settings containing archive fields.</param>
        /// <returns>Archive folder settings, or <c>null</c> when archiving is disabled or incomplete.</returns>
        public static FolderSettings TryCreateGlobalArchiveFolderSettings(ScreenshotsVisualizerSettings settings)
        {
            return ResolveGlobalArchiveReference(settings, requireEnabled: true);
        }

        /// <summary>
        /// Builds the global archive reference used during one-shot migration cleanup.
        /// </summary>
        /// <param name="settings">Plugin settings containing archive fields.</param>
        /// <returns>Archive reference settings, or <c>null</c> when no folder path is configured.</returns>
        public static FolderSettings TryGetGlobalArchiveReferenceForMigration(ScreenshotsVisualizerSettings settings)
        {
            return ResolveGlobalArchiveReference(settings, requireEnabled: false);
        }

        private static FolderSettings ResolveGlobalArchiveReference(ScreenshotsVisualizerSettings settings, bool requireEnabled)
        {
            if (settings == null || settings.FolderToSave.IsNullOrEmpty())
            {
                return null;
            }

            if (requireEnabled && !settings.EnableFolderToSave)
            {
                return null;
            }

            return new FolderSettings
            {
                ScreenshotsFolder = settings.FolderToSave,
                UsedFilePattern = true,
                FilePattern = settings.FileSavePattern ?? string.Empty
            };
        }

        /// <summary>
        /// Returns whether a folder entry strictly matches the global archive configuration.
        /// </summary>
        /// <param name="candidate">Folder entry to test.</param>
        /// <param name="globalArchiveFolder">Global archive folder settings.</param>
        /// <returns><c>true</c> when path, pattern flag, and pattern text match strictly.</returns>
        public static bool IsStrictArchiveFolderMatch(FolderSettings candidate, FolderSettings globalArchiveFolder)
        {
            if (candidate == null || globalArchiveFolder == null)
            {
                return false;
            }

            return candidate.ScreenshotsFolder.IsEqual(globalArchiveFolder.ScreenshotsFolder)
                && candidate.UsedFilePattern == globalArchiveFolder.UsedFilePattern
                && candidate.FilePattern.IsEqual(globalArchiveFolder.FilePattern);
        }

        /// <summary>
        /// Returns whether legacy game-level folder fields strictly duplicate the global archive configuration.
        /// </summary>
        /// <param name="gameSettings">Persisted per-game settings.</param>
        /// <param name="globalArchiveFolder">Global archive folder settings.</param>
        /// <returns><c>true</c> when legacy fields match the global archive strictly.</returns>
        public static bool IsLegacyArchiveDuplicate(GameSettings gameSettings, FolderSettings globalArchiveFolder)
        {
            if (gameSettings == null || globalArchiveFolder == null || gameSettings.ScreenshotsFolder.IsNullOrEmpty())
            {
                return false;
            }

            FolderSettings legacyEntry = new FolderSettings
            {
                ScreenshotsFolder = gameSettings.ScreenshotsFolder,
                UsedFilePattern = gameSettings.UsedFilePattern,
                FilePattern = gameSettings.FilePattern ?? string.Empty
            };

            return IsStrictArchiveFolderMatch(legacyEntry, globalArchiveFolder);
        }

        /// <summary>
        /// Counts persisted list entries that strictly duplicate the global archive configuration.
        /// </summary>
        /// <param name="screenshotsFolders">Persisted folder list for one game.</param>
        /// <param name="globalArchiveFolder">Global archive folder settings.</param>
        /// <returns>Number of strict duplicate entries in the list.</returns>
        public static int CountListArchiveDuplicates(IList<FolderSettings> screenshotsFolders, FolderSettings globalArchiveFolder)
        {
            if (screenshotsFolders == null || screenshotsFolders.Count == 0 || globalArchiveFolder == null)
            {
                return 0;
            }

            int count = 0;
            foreach (FolderSettings folder in screenshotsFolders)
            {
                if (IsStrictArchiveFolderMatch(folder, globalArchiveFolder))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Scans persisted per-game settings for archive folder entries that duplicate the global archive configuration.
        /// Divergent per-game archive entries are not reported (decision #7).
        /// </summary>
        /// <param name="settings">Plugin settings to analyze.</param>
        /// <returns>Duplicate analysis summary.</returns>
        public static SsvArchiveDuplicateAnalysis AnalyzePersistedArchiveDuplicates(ScreenshotsVisualizerSettings settings)
        {
            return AnalyzePersistedArchiveDuplicatesInternal(settings, TryCreateGlobalArchiveFolderSettings(settings));
        }

        /// <summary>
        /// Scans persisted settings for archive duplicates using the migration reference
        /// (<see cref="ScreenshotsVisualizerSettings.FolderToSave"/> even when archiving is disabled).
        /// Matches both the global template and per-game expanded archive destinations.
        /// </summary>
        /// <param name="settings">Plugin settings to analyze.</param>
        /// <returns>Duplicate analysis summary.</returns>
        public static SsvArchiveDuplicateAnalysis AnalyzePersistedArchiveDuplicatesForMigration(ScreenshotsVisualizerSettings settings)
        {
            SsvArchiveDuplicateAnalysis analysis = new SsvArchiveDuplicateAnalysis();
            FolderSettings globalArchiveFolder = TryGetGlobalArchiveReferenceForMigration(settings);
            if (settings?.gameSettings == null || globalArchiveFolder == null)
            {
                return analysis;
            }

            analysis.HasGlobalArchiveReference = true;

            foreach (GameSettings gameSettings in settings.gameSettings)
            {
                if (gameSettings == null)
                {
                    continue;
                }

                Game game = API.Instance.Database.Games.Get(gameSettings.Id);
                string expandedArchivePath = TryResolveExpandedArchivePath(settings, game);

                int listDuplicates = CountMigratableListArchiveDuplicates(
                    gameSettings.ScreenshotsFolders,
                    globalArchiveFolder,
                    expandedArchivePath,
                    game);
                bool legacyDuplicate = IsMigratableLegacyArchiveDuplicate(
                    gameSettings,
                    globalArchiveFolder,
                    expandedArchivePath,
                    game);
                if (listDuplicates == 0 && !legacyDuplicate)
                {
                    continue;
                }

                analysis.Games.Add(new SsvGameArchiveDuplicateEntry
                {
                    GameId = gameSettings.Id,
                    ListDuplicateCount = listDuplicates,
                    LegacyDuplicate = legacyDuplicate
                });
                analysis.RemovableListEntries += listDuplicates;
                if (legacyDuplicate)
                {
                    analysis.RemovableLegacyEntries++;
                }
            }

            analysis.GamesWithDuplicates = analysis.Games.Count;
            return analysis;
        }

        /// <summary>
        /// Removes persisted archive duplicates using the migration global reference.
        /// Removes template copies and per-game expanded archive destinations.
        /// </summary>
        /// <param name="settings">Plugin settings to mutate.</param>
        /// <returns>Total number of removed duplicates.</returns>
        public static int RemovePersistedArchiveDuplicatesForMigration(ScreenshotsVisualizerSettings settings)
        {
            if (settings?.gameSettings == null)
            {
                return 0;
            }

            FolderSettings globalArchiveFolder = TryGetGlobalArchiveReferenceForMigration(settings);
            if (globalArchiveFolder == null)
            {
                return 0;
            }

            int removed = 0;
            foreach (GameSettings gameSettings in settings.gameSettings)
            {
                if (gameSettings == null)
                {
                    continue;
                }

                Game game = API.Instance.Database.Games.Get(gameSettings.Id);
                string expandedArchivePath = TryResolveExpandedArchivePath(settings, game);

                removed += RemoveMigratableListArchiveDuplicates(
                    gameSettings.ScreenshotsFolders,
                    globalArchiveFolder,
                    expandedArchivePath,
                    game);
                removed += ClearMigratableLegacyArchiveDuplicate(
                    gameSettings,
                    globalArchiveFolder,
                    expandedArchivePath,
                    game);
            }

            return removed;
        }

        /// <summary>
        /// Resolves the archive destination folder for a game from global <see cref="ScreenshotsVisualizerSettings.FolderToSave"/>.
        /// </summary>
        /// <param name="settings">Plugin settings containing archive fields.</param>
        /// <param name="game">Playnite game used to expand path variables.</param>
        /// <returns>Expanded safe archive path, or <c>null</c> when unavailable.</returns>
        public static string TryResolveExpandedArchivePath(ScreenshotsVisualizerSettings settings, Game game)
        {
            if (settings == null || game == null || settings.FolderToSave.IsNullOrEmpty())
            {
                return null;
            }

            string pathFolder = settings.FolderToSave;
            if (!pathFolder.Contains("{Name}"))
            {
                pathFolder = Path.Combine(pathFolder, "{Name}");
            }

            pathFolder = PlayniteTools.StringExpandWithStores(game, pathFolder);
            return PathValidator.GetSafePath(pathFolder, false);
        }

        /// <summary>
        /// Returns whether a folder entry is a migratable archive duplicate for the given game.
        /// </summary>
        /// <param name="candidate">Folder entry to test.</param>
        /// <param name="globalArchiveFolder">Global archive folder template.</param>
        /// <param name="expandedArchivePath">Resolved archive path for the game.</param>
        /// <param name="game">Playnite game used to expand candidate paths.</param>
        /// <returns><c>true</c> when the entry matches the global archive for migration cleanup.</returns>
        public static bool IsMigratableArchiveFolderMatch(
            FolderSettings candidate,
            FolderSettings globalArchiveFolder,
            string expandedArchivePath,
            Game game)
        {
            if (candidate == null || globalArchiveFolder == null)
            {
                return false;
            }

            if (!candidate.UsedFilePattern || !candidate.FilePattern.IsEqual(globalArchiveFolder.FilePattern))
            {
                return false;
            }

            if (IsStrictArchiveFolderMatch(candidate, globalArchiveFolder))
            {
                return true;
            }

            if (expandedArchivePath.IsNullOrEmpty() || game == null || candidate.ScreenshotsFolder.IsNullOrEmpty())
            {
                return false;
            }

            string expandedCandidate = ExpandArchiveFolderPath(candidate.ScreenshotsFolder, game);
            return !expandedCandidate.IsNullOrEmpty() && expandedCandidate.IsEqual(expandedArchivePath);
        }

        /// <summary>
        /// Removes persisted archive duplicates that strictly match the enabled global archive configuration.
        /// </summary>
        /// <param name="settings">Plugin settings to mutate.</param>
        /// <returns>Total number of removed duplicates.</returns>
        public static int RemovePersistedArchiveDuplicates(ScreenshotsVisualizerSettings settings)
        {
            if (settings?.gameSettings == null)
            {
                return 0;
            }

            FolderSettings globalArchiveFolder = TryCreateGlobalArchiveFolderSettings(settings);
            if (globalArchiveFolder == null)
            {
                return 0;
            }

            int removed = 0;
            foreach (GameSettings gameSettings in settings.gameSettings)
            {
                if (gameSettings == null)
                {
                    continue;
                }

                removed += RemoveListArchiveDuplicates(gameSettings.ScreenshotsFolders, globalArchiveFolder);
                removed += ClearLegacyArchiveDuplicate(gameSettings, globalArchiveFolder);
            }

            return removed;
        }

        private static SsvArchiveDuplicateAnalysis AnalyzePersistedArchiveDuplicatesInternal(
            ScreenshotsVisualizerSettings settings,
            FolderSettings globalArchiveFolder)
        {
            SsvArchiveDuplicateAnalysis analysis = new SsvArchiveDuplicateAnalysis();
            if (settings?.gameSettings == null || globalArchiveFolder == null)
            {
                return analysis;
            }

            analysis.HasGlobalArchiveReference = true;

            foreach (GameSettings gameSettings in settings.gameSettings)
            {
                if (gameSettings == null)
                {
                    continue;
                }

                int listDuplicates = CountListArchiveDuplicates(gameSettings.ScreenshotsFolders, globalArchiveFolder);
                bool legacyDuplicate = IsLegacyArchiveDuplicate(gameSettings, globalArchiveFolder);
                if (listDuplicates == 0 && !legacyDuplicate)
                {
                    continue;
                }

                analysis.Games.Add(new SsvGameArchiveDuplicateEntry
                {
                    GameId = gameSettings.Id,
                    ListDuplicateCount = listDuplicates,
                    LegacyDuplicate = legacyDuplicate
                });
                analysis.RemovableListEntries += listDuplicates;
                if (legacyDuplicate)
                {
                    analysis.RemovableLegacyEntries++;
                }
            }

            analysis.GamesWithDuplicates = analysis.Games.Count;
            return analysis;
        }

        private static string ExpandArchiveFolderPath(string folderPath, Game game)
        {
            if (folderPath.IsNullOrEmpty() || game == null)
            {
                return null;
            }

            string expanded = PlayniteTools.StringExpandWithStores(game, folderPath);
            return PathValidator.GetSafePath(expanded, false);
        }

        private static int CountMigratableListArchiveDuplicates(
            IList<FolderSettings> screenshotsFolders,
            FolderSettings globalArchiveFolder,
            string expandedArchivePath,
            Game game)
        {
            if (screenshotsFolders == null || screenshotsFolders.Count == 0 || globalArchiveFolder == null)
            {
                return 0;
            }

            int count = 0;
            foreach (FolderSettings folder in screenshotsFolders)
            {
                if (IsMigratableArchiveFolderMatch(folder, globalArchiveFolder, expandedArchivePath, game))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsMigratableLegacyArchiveDuplicate(
            GameSettings gameSettings,
            FolderSettings globalArchiveFolder,
            string expandedArchivePath,
            Game game)
        {
            if (gameSettings == null || globalArchiveFolder == null || gameSettings.ScreenshotsFolder.IsNullOrEmpty())
            {
                return false;
            }

            FolderSettings legacyEntry = new FolderSettings
            {
                ScreenshotsFolder = gameSettings.ScreenshotsFolder,
                UsedFilePattern = gameSettings.UsedFilePattern,
                FilePattern = gameSettings.FilePattern ?? string.Empty
            };

            return IsMigratableArchiveFolderMatch(legacyEntry, globalArchiveFolder, expandedArchivePath, game);
        }

        private static int RemoveMigratableListArchiveDuplicates(
            IList<FolderSettings> screenshotsFolders,
            FolderSettings globalArchiveFolder,
            string expandedArchivePath,
            Game game)
        {
            if (screenshotsFolders == null || screenshotsFolders.Count == 0)
            {
                return 0;
            }

            int removed = 0;
            for (int index = screenshotsFolders.Count - 1; index >= 0; index--)
            {
                if (!IsMigratableArchiveFolderMatch(screenshotsFolders[index], globalArchiveFolder, expandedArchivePath, game))
                {
                    continue;
                }

                screenshotsFolders.RemoveAt(index);
                removed++;
            }

            return removed;
        }

        private static int ClearMigratableLegacyArchiveDuplicate(
            GameSettings gameSettings,
            FolderSettings globalArchiveFolder,
            string expandedArchivePath,
            Game game)
        {
            if (!IsMigratableLegacyArchiveDuplicate(gameSettings, globalArchiveFolder, expandedArchivePath, game))
            {
                return 0;
            }

            gameSettings.ScreenshotsFolder = string.Empty;
            gameSettings.UsedFilePattern = false;
            gameSettings.FilePattern = string.Empty;
            return 1;
        }

        private static int RemoveListArchiveDuplicates(IList<FolderSettings> screenshotsFolders, FolderSettings globalArchiveFolder)
        {
            if (screenshotsFolders == null || screenshotsFolders.Count == 0)
            {
                return 0;
            }

            int removed = 0;
            for (int index = screenshotsFolders.Count - 1; index >= 0; index--)
            {
                if (!IsStrictArchiveFolderMatch(screenshotsFolders[index], globalArchiveFolder))
                {
                    continue;
                }

                screenshotsFolders.RemoveAt(index);
                removed++;
            }

            return removed;
        }

        private static int ClearLegacyArchiveDuplicate(GameSettings gameSettings, FolderSettings globalArchiveFolder)
        {
            if (!IsLegacyArchiveDuplicate(gameSettings, globalArchiveFolder))
            {
                return 0;
            }

            gameSettings.ScreenshotsFolder = string.Empty;
            gameSettings.UsedFilePattern = false;
            gameSettings.FilePattern = string.Empty;
            return 1;
        }
    }
}
