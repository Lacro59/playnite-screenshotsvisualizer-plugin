using CommonPluginsShared.Extensions;
using CommonPluginsShared.IO;
using CommonPluginsStores;
using Playnite.SDK;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ScreenshotsVisualizer.Services
{
    /// <summary>
    /// Shared helpers to detect and remove persisted per-game folders that duplicate applicable global screenshot sources.
    /// </summary>
    public static class SsvGlobalSourceFolderHelper
    {
        private static readonly ILogger Logger = LogManager.GetLogger();

        /// <summary>
        /// Counts persisted per-game folder entries that duplicate applicable global screenshot sources.
        /// Games with <see cref="GameSettings.OverrideGlobalConfigs"/> are ignored.
        /// </summary>
        /// <param name="settings">Plugin settings to inspect.</param>
        /// <returns>Number of removable duplicate entries.</returns>
        public static int CountPersistedGlobalSourceDuplicatesForMigration(ScreenshotsVisualizerSettings settings)
        {
            return ProcessPersistedGlobalSourceDuplicates(settings, remove: false);
        }

        /// <summary>
        /// Removes persisted per-game folder entries that duplicate applicable global screenshot sources.
        /// Games with <see cref="GameSettings.OverrideGlobalConfigs"/> are ignored.
        /// </summary>
        /// <param name="settings">Plugin settings to mutate.</param>
        /// <returns>Number of removed duplicate entries.</returns>
        public static int RemovePersistedGlobalSourceDuplicatesForMigration(ScreenshotsVisualizerSettings settings)
        {
            return ProcessPersistedGlobalSourceDuplicates(settings, remove: true);
        }

        private static int ProcessPersistedGlobalSourceDuplicates(ScreenshotsVisualizerSettings settings, bool remove)
        {
            if (settings?.gameSettings == null)
            {
                return 0;
            }

            settings.EnsureGlobalScreenshotSourcesMigrated();
            IList<FolderSettings> globalSources = settings.GetEffectiveGlobalScreenshotSources();
            if (globalSources == null || globalSources.Count == 0)
            {
                return 0;
            }

            int duplicates = 0;
            int scannedGames = 0;
            foreach (GameSettings gameSettings in settings.gameSettings)
            {
                if (gameSettings == null
                    || gameSettings.OverrideGlobalConfigs
                    || gameSettings.ScreenshotsFolders == null
                    || gameSettings.ScreenshotsFolders.Count == 0)
                {
                    continue;
                }

                Game game = API.Instance.Database.Games.Get(gameSettings.Id);
                if (game == null)
                {
                    continue;
                }

                scannedGames++;
                List<FolderSettings> applicableGlobalSources = globalSources
                    .Where(x =>
                        x != null
                        && !x.ScreenshotsFolder.IsNullOrEmpty()
                        && SsvGlobalSourceApplicabilityHelper.MatchesGame(game, x))
                    .ToList();
                if (applicableGlobalSources.Count == 0)
                {
                    continue;
                }

                for (int index = gameSettings.ScreenshotsFolders.Count - 1; index >= 0; index--)
                {
                    FolderSettings candidate = gameSettings.ScreenshotsFolders[index];
                    if (!IsMigratablePerGameGlobalSourceDuplicate(candidate, applicableGlobalSources, game))
                    {
                        continue;
                    }

                    duplicates++;
                    if (remove)
                    {
                        gameSettings.ScreenshotsFolders.RemoveAt(index);
                        Logger.Info(string.Format(
                            "[SsvGlobalSourceMigration] Removed duplicate source for '{0}' — '{1}'.",
                            game.Name,
                            candidate?.ScreenshotsFolder ?? string.Empty));
                    }
                }
            }

            CommonPluginsShared.Common.LogDebug(true, string.Format(
                "[SsvGlobalSourceMigration] Duplicate scan completed — mode={0}, scanned games={1}, duplicates={2}.",
                remove ? "remove" : "count",
                scannedGames,
                duplicates));
            return duplicates;
        }

        private static bool IsMigratablePerGameGlobalSourceDuplicate(
            FolderSettings candidate,
            IList<FolderSettings> applicableGlobalSources,
            Game game)
        {
            if (candidate == null || applicableGlobalSources == null || applicableGlobalSources.Count == 0)
            {
                return false;
            }

            foreach (FolderSettings globalSource in applicableGlobalSources)
            {
                if (!AreComparableFolderScanAndPattern(candidate, globalSource))
                {
                    continue;
                }

                // Keep strict match first to avoid path expansion cost when templates already match.
                if (IsStrictGlobalSourceMatch(candidate, globalSource))
                {
                    return true;
                }

                string expandedCandidate = ExpandFolderPath(candidate.ScreenshotsFolder, game);
                string expandedGlobal = ExpandFolderPath(globalSource.ScreenshotsFolder, game);
                if (!expandedCandidate.IsNullOrEmpty() && expandedCandidate.IsEqual(expandedGlobal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool AreComparableFolderScanAndPattern(FolderSettings candidate, FolderSettings globalSource)
        {
            if (candidate == null || globalSource == null)
            {
                return false;
            }

            return candidate.ScanSubFolders == globalSource.ScanSubFolders
                && candidate.UsedFilePattern == globalSource.UsedFilePattern
                && SsvFolderPresetService.AreFolderTemplateStringsEqual(candidate.FilePattern, globalSource.FilePattern);
        }

        private static bool IsStrictGlobalSourceMatch(FolderSettings candidate, FolderSettings globalSource)
        {
            return SsvFolderPresetService.AreFolderTemplateStringsEqual(candidate?.ScreenshotsFolder, globalSource?.ScreenshotsFolder)
                && AreComparableFolderScanAndPattern(candidate, globalSource);
        }

        private static string ExpandFolderPath(string folderPath, Game game)
        {
            if (folderPath.IsNullOrEmpty() || game == null)
            {
                return null;
            }

            string expandedPath = PlayniteTools.StringExpandWithStores(game, folderPath);
            return PathValidator.GetSafePath(expandedPath, false);
        }
    }
}

