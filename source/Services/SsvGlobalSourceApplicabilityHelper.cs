using CommonPluginsShared;
using CommonPluginsShared.Interfaces;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ScreenshotsVisualizer.Services
{
    /// <summary>
    /// Evaluates whether a global screenshot source entry applies to a Playnite game.
    /// Source and emulator constraints are combined with logical AND when active.
    /// </summary>
    public static class SsvGlobalSourceApplicabilityHelper
    {
        /// <summary>
        /// Returns whether <paramref name="folderSettings"/> applies to <paramref name="game"/>.
        /// Inactive filters (source mode <see cref="SourceFilterMode.All"/>, emulator
        /// <see cref="SsvApplicableEmulatorFilter.None"/>) are ignored.
        /// </summary>
        /// <param name="game">Playnite game to evaluate.</param>
        /// <param name="folderSettings">Global folder settings including applicability fields.</param>
        /// <returns><c>true</c> when all active applicability constraints pass.</returns>
        public static bool MatchesGame(Game game, FolderSettings folderSettings)
        {
            if (game == null || folderSettings == null)
            {
                return false;
            }

            return MatchesSourceFilter(game, folderSettings)
                && MatchesEmulatorFilter(game, folderSettings);
        }

        /// <summary>
        /// Returns whether the source filter on <paramref name="folderSettings"/> is active.
        /// </summary>
        /// <param name="folderSettings">Folder settings to inspect.</param>
        /// <returns><c>true</c> when whitelist or blacklist mode is selected.</returns>
        public static bool HasActiveSourceFilter(FolderSettings folderSettings)
        {
            if (folderSettings == null)
            {
                return false;
            }

            return folderSettings.ApplicableSourceFilterMode != SourceFilterMode.All;
        }

        /// <summary>
        /// Returns whether the emulator filter on <paramref name="folderSettings"/> is active.
        /// </summary>
        /// <param name="folderSettings">Folder settings to inspect.</param>
        /// <returns><c>true</c> when an emulator constraint other than <see cref="SsvApplicableEmulatorFilter.None"/> is set.</returns>
        public static bool HasActiveEmulatorFilter(FolderSettings folderSettings)
        {
            return folderSettings != null
                && folderSettings.ApplicableEmulatorFilter != SsvApplicableEmulatorFilter.None;
        }

        /// <summary>
        /// Returns whether applicability fields are equivalent on two folder settings instances.
        /// </summary>
        /// <param name="left">First folder settings.</param>
        /// <param name="right">Second folder settings.</param>
        /// <returns><c>true</c> when source and emulator applicability match.</returns>
        public static bool AreApplicabilitySettingsEqual(FolderSettings left, FolderSettings right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            if (left.ApplicableSourceFilterMode != right.ApplicableSourceFilterMode
                || left.ApplicableEmulatorFilter != right.ApplicableEmulatorFilter)
            {
                return false;
            }

            return AreSourceListsEquivalent(left.ApplicableSources, right.ApplicableSources);
        }

        private static bool MatchesSourceFilter(Game game, FolderSettings folderSettings)
        {
            switch (folderSettings.ApplicableSourceFilterMode)
            {
                case SourceFilterMode.Whitelist:
                    IList<string> whitelist = folderSettings.ApplicableSources;
                    if (whitelist == null || whitelist.Count == 0)
                    {
                        return false;
                    }

                    return whitelist.Any(entry => SourceMatchesGame(game, entry));

                case SourceFilterMode.Blacklist:
                    IList<string> blacklist = folderSettings.ApplicableSources;
                    if (blacklist == null || blacklist.Count == 0)
                    {
                        return true;
                    }

                    return !blacklist.Any(entry => SourceMatchesGame(game, entry));

                case SourceFilterMode.All:
                default:
                    return true;
            }
        }

        private static bool MatchesEmulatorFilter(Game game, FolderSettings folderSettings)
        {
            switch (folderSettings.ApplicableEmulatorFilter)
            {
                case SsvApplicableEmulatorFilter.RetroArch:
                    return PlayniteTools.GameUseRetroArch(game);

                case SsvApplicableEmulatorFilter.ScummVM:
                    return PlayniteTools.GameUseScummVM(game);

                case SsvApplicableEmulatorFilter.None:
                default:
                    return true;
            }
        }

        private static bool SourceMatchesGame(Game game, string applicableSourceEntry)
        {
            if (string.IsNullOrWhiteSpace(applicableSourceEntry))
            {
                return false;
            }

            string gameSourceName = PlayniteTools.GetSourceName(game);
            if (PlayniteTools.SourceNamesMatch(gameSourceName, applicableSourceEntry))
            {
                return true;
            }

            return IsUbisoftFamilySource(gameSourceName)
                && IsUbisoftFamilySource(applicableSourceEntry);
        }

        private static bool IsUbisoftFamilySource(string sourceName)
        {
            if (string.IsNullOrWhiteSpace(sourceName))
            {
                return false;
            }

            string normalized = sourceName.Trim();
            return normalized.Equals("Ubisoft Connect", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("Uplay", StringComparison.OrdinalIgnoreCase);
        }

        private static bool AreSourceListsEquivalent(IList<string> left, IList<string> right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return (left == null || left.Count == 0) && (right == null || right.Count == 0);
            }

            if (left.Count != right.Count)
            {
                return false;
            }

            List<string> leftNormalized = left
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            List<string> rightNormalized = right
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (leftNormalized.Count != rightNormalized.Count)
            {
                return false;
            }

            for (int i = 0; i < leftNormalized.Count; i++)
            {
                if (!leftNormalized[i].Equals(rightNormalized[i], StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
