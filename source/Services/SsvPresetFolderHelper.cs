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
    /// Detects migratable preset folder duplicates in persisted settings (per-game and legacy global rows).
    /// </summary>
    public static class SsvPresetFolderHelper
    {
        private static readonly SsvFolderPresetId[] AllPresetIds =
        {
            SsvFolderPresetId.Steam,
            SsvFolderPresetId.Gog,
            SsvFolderPresetId.Ubisoft,
            SsvFolderPresetId.RetroArch,
            SsvFolderPresetId.ScummVM,
            SsvFolderPresetId.XboxGameBar
        };

        private const string SteamScreenshotsDirToken = "{SteamScreenshotsDir}";
        private const string SteamPerGameSuffix = "\\screenshots";
        private const string SteamGlobalTemplatePath = "{SteamScreenshotsDir}\\{GameId}\\screenshots";
        private const string UbisoftScreenshotsDirToken = "{UbisoftScreenshotsDir}";
        private const string UbisoftGlobalTemplatePath = "{UbisoftScreenshotsDir}\\{Name}";

        /// <summary>
        /// Creates per-game folder settings for a built-in preset (mass-add / legacy per-game shape).
        /// </summary>
        /// <param name="presetId">Preset identifier.</param>
        /// <param name="game">Playnite game used to resolve path segments.</param>
        /// <returns>Preset folder settings for the game.</returns>
        public static FolderSettings CreatePerGamePresetFolderSettings(SsvFolderPresetId presetId, Game game)
        {
            if (game == null)
            {
                return new FolderSettings();
            }

            switch (presetId)
            {
                case SsvFolderPresetId.Steam:
                    return new FolderSettings
                    {
                        ScreenshotsFolder = "{SteamScreenshotsDir}\\" + game.GameId + "\\screenshots"
                    };

                case SsvFolderPresetId.Ubisoft:
                    return new FolderSettings
                    {
                        ScreenshotsFolder = "{UbisoftScreenshotsDir}\\" + game.Name
                    };

                case SsvFolderPresetId.Gog:
                    return new FolderSettings
                    {
                        ScreenshotsFolder = "{GogScreenshotDir}\\" + game.Name
                    };

                case SsvFolderPresetId.RetroArch:
                    return new FolderSettings
                    {
                        ScreenshotsFolder = "{RetroArchScreenshotsDir}",
                        UsedFilePattern = true,
                        FilePattern = "{ImageNameNoExt}-{digit}-{digit}"
                    };

                case SsvFolderPresetId.ScummVM:
                    return new FolderSettings
                    {
                        ScreenshotsFolder = "{UserProfile}\\Pictures\\ScummVM Screenshots",
                        UsedFilePattern = true,
                        FilePattern = "scummvm-{ImageNameNoExt}-{digit}"
                    };

                case SsvFolderPresetId.XboxGameBar:
                    return new FolderSettings
                    {
                        ScreenshotsFolder = "{XboxGamebarScreenshotsDir}",
                        UsedFilePattern = true,
                        FilePattern = game.Name + " *"
                    };

                default:
                    return new FolderSettings();
            }
        }

        /// <summary>
        /// Scans persisted settings for preset duplicates that can be consolidated into global sources.
        /// Games with <see cref="GameSettings.OverrideGlobalConfigs"/> are ignored.
        /// </summary>
        /// <param name="settings">Plugin settings to analyze.</param>
        /// <returns>Duplicate analysis summary.</returns>
        public static SsvPresetDuplicateAnalysis AnalyzePersistedPresetDuplicates(ScreenshotsVisualizerSettings settings)
        {
            SsvPresetDuplicateAnalysis analysis = new SsvPresetDuplicateAnalysis();
            if (settings == null)
            {
                return analysis;
            }

            settings.EnsureGlobalScreenshotSourcesMigrated();
            IList<FolderSettings> globalSources = settings.GetEffectiveGlobalScreenshotSources();
            analysis.RemovableLegacyGlobalEntries = CountLegacyGlobalPresetDuplicates(globalSources);

            HashSet<SsvFolderPresetId> presetsNeededInGlobal = GetPresetsReferencedByLegacyGlobals(globalSources);

            if (settings.gameSettings != null)
            {
                foreach (GameSettings gameSettings in settings.gameSettings)
                {
                    if (gameSettings == null || gameSettings.OverrideGlobalConfigs)
                    {
                        continue;
                    }

                    Game game = API.Instance.Database.Games.Get(gameSettings.Id);
                    List<SsvFolderPresetId> matchedPresets = new List<SsvFolderPresetId>();
                    int listDuplicates = CountMigratableListPresetDuplicates(
                        gameSettings.ScreenshotsFolders,
                        game,
                        matchedPresets);

                    if (listDuplicates == 0)
                    {
                        continue;
                    }

                    foreach (SsvFolderPresetId presetId in matchedPresets)
                    {
                        presetsNeededInGlobal.Add(presetId);
                    }

                    analysis.Games.Add(new SsvGamePresetDuplicateEntry
                    {
                        GameId = gameSettings.Id,
                        ListDuplicateCount = listDuplicates,
                        MatchedPresets = matchedPresets.Distinct().ToList()
                    });
                    analysis.RemovableListEntries += listDuplicates;
                }
            }

            analysis.MissingGlobalPresets = presetsNeededInGlobal
                .Where(presetId => globalSources == null
                    || !globalSources.Any(x => SsvFolderPresetCatalog.IsCanonicalGlobalFolderSettings(x, presetId)))
                .OrderBy(x => x)
                .ToList();
            analysis.GamesWithDuplicates = analysis.Games.Count;
            return analysis;
        }

        /// <summary>
        /// Returns whether a per-game folder entry is a migratable duplicate of a built-in preset.
        /// </summary>
        /// <param name="candidate">Folder entry to test.</param>
        /// <param name="presetId">Preset identifier.</param>
        /// <param name="game">Playnite game used to expand stored paths.</param>
        /// <param name="matchedPresetId">Matched preset when found.</param>
        /// <returns><c>true</c> when the entry can be removed during migration.</returns>
        public static bool TryMatchMigratablePerGamePresetFolder(
            FolderSettings candidate,
            Game game,
            out SsvFolderPresetId matchedPresetId)
        {
            foreach (SsvFolderPresetId presetId in AllPresetIds)
            {
                if (IsMigratablePerGamePresetFolder(candidate, presetId, game))
                {
                    matchedPresetId = presetId;
                    return true;
                }
            }

            matchedPresetId = default(SsvFolderPresetId);
            return false;
        }

        /// <summary>
        /// Returns whether a per-game folder entry matches a migratable preset for the given game.
        /// </summary>
        /// <param name="candidate">Folder entry to test.</param>
        /// <param name="presetId">Preset identifier.</param>
        /// <param name="game">Playnite game used to expand stored paths.</param>
        /// <returns><c>true</c> when the entry can be removed during migration.</returns>
        public static bool IsMigratablePerGamePresetFolder(
            FolderSettings candidate,
            SsvFolderPresetId presetId,
            Game game)
        {
            if (candidate == null)
            {
                return false;
            }

            switch (presetId)
            {
                case SsvFolderPresetId.Steam:
                    if (IsMigratableSteamPerGameFolderShape(candidate, game))
                    {
                        return true;
                    }

                    break;

                case SsvFolderPresetId.Ubisoft:
                    if (IsMigratableUbisoftPerGameFolderShape(candidate, game))
                    {
                        return true;
                    }

                    break;
            }

            FolderSettings perGameCanonical = CreatePerGamePresetFolderSettings(presetId, game);
            if (MatchesMigratableFolderShape(candidate, perGameCanonical, game))
            {
                return true;
            }

            FolderSettings globalCanonical = SsvFolderPresetCatalog.CreateGlobalFolderSettings(presetId);
            if (MatchesMigratableFolderShape(candidate, globalCanonical, game))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns whether a global source row is a legacy preset shape replaced by canonical globals.
        /// </summary>
        /// <param name="candidate">Global folder entry to test.</param>
        /// <param name="presetId">Preset identifier.</param>
        /// <returns><c>true</c> when the row should be replaced during migration.</returns>
        public static bool IsLegacyGlobalPresetFolder(FolderSettings candidate, SsvFolderPresetId presetId)
        {
            if (candidate == null)
            {
                return false;
            }

            if (SsvFolderPresetCatalog.IsCanonicalGlobalFolderSettings(candidate, presetId))
            {
                return false;
            }

            switch (presetId)
            {
                case SsvFolderPresetId.Steam:
                    return candidate.ScreenshotsFolder.IsEqual("{SteamScreenshotsDir}")
                        && candidate.ScanSubFolders
                        && !candidate.UsedFilePattern;

                case SsvFolderPresetId.Ubisoft:
                    return candidate.ScreenshotsFolder.IsEqual("{UbisoftScreenshotsDir}")
                        && candidate.ScanSubFolders
                        && !candidate.UsedFilePattern;

                case SsvFolderPresetId.RetroArch:
                case SsvFolderPresetId.ScummVM:
                    return MatchesMigratableFolderShape(
                        candidate,
                        SsvFolderPresetCatalog.CreateGlobalFolderSettings(presetId),
                        game: null);

                default:
                    return false;
            }
        }

        /// <summary>
        /// Ensures canonical global preset rows exist for presets referenced by migratable duplicates.
        /// </summary>
        /// <param name="settings">Plugin settings to mutate.</param>
        /// <param name="presetIds">Preset identifiers to ensure.</param>
        /// <returns>Number of global rows added.</returns>
        public static int EnsureGlobalCanonicalPresets(ScreenshotsVisualizerSettings settings, IEnumerable<SsvFolderPresetId> presetIds)
        {
            if (settings == null || presetIds == null)
            {
                return 0;
            }

            settings.EnsureGlobalScreenshotSourcesMigrated();
            if (settings.GlobalScreenshotSources == null)
            {
                settings.GlobalScreenshotSources = new List<FolderSettings>();
            }

            int added = 0;
            foreach (SsvFolderPresetId presetId in presetIds.Distinct())
            {
                if (settings.GlobalScreenshotSources.Any(x => SsvFolderPresetCatalog.IsCanonicalGlobalFolderSettings(x, presetId)))
                {
                    continue;
                }

                settings.GlobalScreenshotSources.Add(SsvFolderPresetCatalog.CreateGlobalFolderSettings(presetId));
                added++;
            }

            return added;
        }

        /// <summary>
        /// Removes migratable preset duplicates from per-game folder lists.
        /// </summary>
        /// <param name="settings">Plugin settings to mutate.</param>
        /// <returns>Total number of removed list entries.</returns>
        public static int RemoveMigratablePerGamePresetDuplicates(ScreenshotsVisualizerSettings settings)
        {
            if (settings?.gameSettings == null)
            {
                return 0;
            }

            int removed = 0;
            foreach (GameSettings gameSettings in settings.gameSettings)
            {
                if (gameSettings == null || gameSettings.OverrideGlobalConfigs)
                {
                    continue;
                }

                Game game = API.Instance.Database.Games.Get(gameSettings.Id);
                removed += RemoveMigratableListPresetDuplicates(gameSettings.ScreenshotsFolders, game);
            }

            return removed;
        }

        /// <summary>
        /// Removes legacy global preset rows and keeps a single canonical row per preset.
        /// </summary>
        /// <param name="settings">Plugin settings to mutate.</param>
        /// <returns>Number of removed legacy global rows.</returns>
        public static int RemoveLegacyGlobalPresetDuplicates(ScreenshotsVisualizerSettings settings)
        {
            if (settings?.GlobalScreenshotSources == null || settings.GlobalScreenshotSources.Count == 0)
            {
                return 0;
            }

            int removed = 0;
            for (int index = settings.GlobalScreenshotSources.Count - 1; index >= 0; index--)
            {
                FolderSettings folder = settings.GlobalScreenshotSources[index];
                if (!AllPresetIds.Any(presetId => IsLegacyGlobalPresetFolder(folder, presetId)))
                {
                    continue;
                }

                settings.GlobalScreenshotSources.RemoveAt(index);
                removed++;
            }

            return removed;
        }

        /// <summary>
        /// Removes configured games that no longer have folder sources and do not override globals.
        /// </summary>
        /// <param name="settings">Plugin settings to mutate.</param>
        /// <returns>Number of removed <see cref="GameSettings"/> entries.</returns>
        public static int RemoveEmptyGameSettings(ScreenshotsVisualizerSettings settings)
        {
            if (settings?.gameSettings == null)
            {
                return 0;
            }

            int removed = 0;
            for (int index = settings.gameSettings.Count - 1; index >= 0; index--)
            {
                GameSettings gameSettings = settings.gameSettings[index];
                if (gameSettings == null
                    || gameSettings.OverrideGlobalConfigs
                    || !IsGameSettingsEmpty(gameSettings))
                {
                    continue;
                }

                settings.gameSettings.RemoveAt(index);
                removed++;
            }

            return removed;
        }

        private static bool IsGameSettingsEmpty(GameSettings gameSettings)
        {
            bool hasListFolders = gameSettings.ScreenshotsFolders != null
                && gameSettings.ScreenshotsFolders.Exists(x =>
                    x != null && !x.ScreenshotsFolder.IsNullOrEmpty());

            if (hasListFolders)
            {
                return false;
            }

            return gameSettings.ScreenshotsFolder.IsNullOrEmpty();
        }

        private static bool MatchesMigratableFolderShape(FolderSettings candidate, FolderSettings reference, Game game)
        {
            if (candidate == null || reference == null)
            {
                return false;
            }

            if (!candidate.UsedFilePattern.Equals(reference.UsedFilePattern)
                || !candidate.FilePattern.IsEqual(reference.FilePattern)
                || candidate.ScanSubFolders != reference.ScanSubFolders)
            {
                return false;
            }

            if (candidate.ScreenshotsFolder.IsEqual(reference.ScreenshotsFolder))
            {
                return true;
            }

            if (game == null || candidate.ScreenshotsFolder.IsNullOrEmpty() || reference.ScreenshotsFolder.IsNullOrEmpty())
            {
                return false;
            }

            string expandedCandidate = ExpandFolderPath(candidate.ScreenshotsFolder, game);
            string expandedReference = ExpandFolderPath(reference.ScreenshotsFolder, game);
            return !expandedCandidate.IsNullOrEmpty()
                && expandedCandidate.IsEqual(expandedReference);
        }

        private static string ExpandFolderPath(string folderPath, Game game)
        {
            if (folderPath.IsNullOrEmpty() || game == null)
            {
                return null;
            }

            string expanded = PlayniteTools.StringExpandWithStores(game, folderPath);
            return PathValidator.GetSafePath(expanded, false);
        }

        private static bool IsMigratableSteamPerGameFolderShape(FolderSettings candidate, Game game)
        {
            if (candidate.UsedFilePattern || candidate.ScanSubFolders)
            {
                return false;
            }

            string path = candidate.ScreenshotsFolder;
            if (path.IsNullOrEmpty())
            {
                return false;
            }

            if (path.IsEqual(SteamGlobalTemplatePath))
            {
                return true;
            }

            string prefix = SteamScreenshotsDirToken + "\\";
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || !path.EndsWith(SteamPerGameSuffix, StringComparison.OrdinalIgnoreCase)
                || path.Length <= prefix.Length + SteamPerGameSuffix.Length)
            {
                return false;
            }

            int storeIdLength = path.Length - prefix.Length - SteamPerGameSuffix.Length;
            string storeId = path.Substring(prefix.Length, storeIdLength);
            if (!IsNumericStoreId(storeId))
            {
                return false;
            }

            if (game != null && !storeId.IsEqual(game.GameId))
            {
                return false;
            }

            return true;
        }

        private static bool IsMigratableUbisoftPerGameFolderShape(FolderSettings candidate, Game game)
        {
            if (candidate.UsedFilePattern || candidate.ScanSubFolders)
            {
                return false;
            }

            string path = candidate.ScreenshotsFolder;
            if (path.IsNullOrEmpty())
            {
                return false;
            }

            if (path.IsEqual(UbisoftGlobalTemplatePath))
            {
                return true;
            }

            string prefix = UbisoftScreenshotsDirToken + "\\";
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || path.Length <= prefix.Length)
            {
                return false;
            }

            if (game == null)
            {
                return true;
            }

            FolderSettings perGameCanonical = CreatePerGamePresetFolderSettings(SsvFolderPresetId.Ubisoft, game);
            if (path.IsEqual(perGameCanonical.ScreenshotsFolder))
            {
                return true;
            }

            string expandedCandidate = ExpandFolderPath(path, game);
            string expandedCanonical = ExpandFolderPath(perGameCanonical.ScreenshotsFolder, game);
            return !expandedCandidate.IsNullOrEmpty()
                && expandedCandidate.IsEqual(expandedCanonical);
        }

        private static bool IsNumericStoreId(string value)
        {
            if (value.IsNullOrEmpty())
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (character < '0' || character > '9')
                {
                    return false;
                }
            }

            return true;
        }

        private static int CountMigratableListPresetDuplicates(
            IList<FolderSettings> screenshotsFolders,
            Game game,
            ICollection<SsvFolderPresetId> matchedPresets)
        {
            if (screenshotsFolders == null || screenshotsFolders.Count == 0)
            {
                return 0;
            }

            int count = 0;
            foreach (FolderSettings folder in screenshotsFolders)
            {
                if (TryMatchMigratablePerGamePresetFolder(folder, game, out SsvFolderPresetId presetId))
                {
                    count++;
                    matchedPresets?.Add(presetId);
                }
            }

            return count;
        }

        private static int RemoveMigratableListPresetDuplicates(IList<FolderSettings> screenshotsFolders, Game game)
        {
            if (screenshotsFolders == null || screenshotsFolders.Count == 0)
            {
                return 0;
            }

            int removed = 0;
            for (int index = screenshotsFolders.Count - 1; index >= 0; index--)
            {
                if (!TryMatchMigratablePerGamePresetFolder(screenshotsFolders[index], game, out _))
                {
                    continue;
                }

                screenshotsFolders.RemoveAt(index);
                removed++;
            }

            return removed;
        }

        private static int CountLegacyGlobalPresetDuplicates(IList<FolderSettings> globalSources)
        {
            if (globalSources == null || globalSources.Count == 0)
            {
                return 0;
            }

            int count = 0;
            foreach (FolderSettings folder in globalSources)
            {
                if (AllPresetIds.Any(presetId => IsLegacyGlobalPresetFolder(folder, presetId)))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Applies global preset consolidation: ensure canonical globals, remove legacy globals and per-game duplicates, prune empty games.
        /// </summary>
        /// <param name="settings">Plugin settings to mutate.</param>
        /// <param name="analysis">Duplicate analysis from <see cref="AnalyzePersistedPresetDuplicates"/>.</param>
        /// <returns>Counts of each cleanup operation.</returns>
        public static SsvPresetMigrationResult ApplyPresetGlobalMigration(
            ScreenshotsVisualizerSettings settings,
            SsvPresetDuplicateAnalysis analysis)
        {
            SsvPresetMigrationResult result = new SsvPresetMigrationResult();
            if (settings == null)
            {
                return result;
            }

            settings.EnsureGlobalScreenshotSourcesMigrated();
            result.GlobalPresetsAdded = EnsureGlobalCanonicalPresets(settings, analysis?.MissingGlobalPresets);
            result.LegacyGlobalRemoved = RemoveLegacyGlobalPresetDuplicates(settings);
            result.PerGameDuplicatesRemoved = RemoveMigratablePerGamePresetDuplicates(settings);
            result.EmptyGameSettingsRemoved = RemoveEmptyGameSettings(settings);
            result.Success = result.TotalChanges > 0;
            return result;
        }

        private static HashSet<SsvFolderPresetId> GetPresetsReferencedByLegacyGlobals(IList<FolderSettings> globalSources)
        {
            HashSet<SsvFolderPresetId> presets = new HashSet<SsvFolderPresetId>();
            if (globalSources == null)
            {
                return presets;
            }

            foreach (FolderSettings folder in globalSources)
            {
                foreach (SsvFolderPresetId presetId in AllPresetIds)
                {
                    if (IsLegacyGlobalPresetFolder(folder, presetId))
                    {
                        presets.Add(presetId);
                    }
                }
            }

            return presets;
        }
    }
}
