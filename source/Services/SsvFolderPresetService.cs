using CommonPluginsShared.Extensions;
using Playnite.SDK;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.ViewModels.Settings;
using System.Collections.Generic;
using System.Linq;

namespace ScreenshotsVisualizer.Services
{
    /// <summary>
    /// Applies built-in folder presets to <see cref="ScreenshotsVisualizerSettings.GlobalScreenshotSources"/>.
    /// </summary>
    public static class SsvFolderPresetService
    {
        /// <summary>
        /// Adds a catalog preset to global sources when not already present.
        /// </summary>
        /// <param name="globalSources">Editable global source collection in settings UI.</param>
        /// <param name="presetId">Preset to add.</param>
        /// <param name="pluginName">Plugin display name for notifications.</param>
        /// <param name="notifyIfAlreadyExists">When <c>true</c>, shows an info notification on duplicate.</param>
        /// <returns>Add result indicating whether the entry was created or skipped.</returns>
        public static SsvFolderPresetAddResult TryAddToGlobal(
            ICollection<FolderEntryItem> globalSources,
            SsvFolderPresetId presetId,
            string pluginName = "ScreenshotsVisualizer",
            bool notifyIfAlreadyExists = true)
        {
            if (globalSources == null)
            {
                return SsvFolderPresetAddResult.UnknownPreset;
            }

            if (!SsvFolderPresetCatalog.TryGet(presetId, out SsvFolderPreset preset))
            {
                return SsvFolderPresetAddResult.UnknownPreset;
            }

            FolderSettings canonical = preset.CreateGlobalFolderSettings();
            if (IsGlobalPresetPresent(globalSources, presetId))
            {
                if (notifyIfAlreadyExists)
                {
                    NotifyPresetAlreadyInGlobal(pluginName);
                }

                return SsvFolderPresetAddResult.AlreadyExists;
            }

            globalSources.Add(new FolderEntryItem(canonical));
            return SsvFolderPresetAddResult.Added;
        }

        /// <summary>
        /// Returns whether a canonical built-in preset is already present in global sources.
        /// </summary>
        /// <param name="globalSources">Global source collection in settings UI.</param>
        /// <param name="presetId">Preset to look for.</param>
        /// <returns><c>true</c> when an equivalent catalog entry exists.</returns>
        public static bool IsGlobalPresetPresent(
            IEnumerable<FolderEntryItem> globalSources,
            SsvFolderPresetId presetId)
        {
            if (globalSources == null || !SsvFolderPresetCatalog.TryGet(presetId, out SsvFolderPreset preset))
            {
                return false;
            }

            FolderSettings canonical = preset.CreateGlobalFolderSettings();
            return globalSources.Any(entry =>
                entry != null
                && !string.IsNullOrWhiteSpace(entry.ScreenshotsFolder)
                && IsEquivalentGlobalFolderSettings(entry.ToModel(), canonical));
        }

        /// <summary>
        /// Returns whether two global folder settings are equivalent for deduplication.
        /// </summary>
        /// <param name="left">First folder settings.</param>
        /// <param name="right">Second folder settings.</param>
        /// <returns><c>true</c> when path, scan options, and applicability match.</returns>
        public static bool IsEquivalentGlobalFolderSettings(FolderSettings left, FolderSettings right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            return AreFolderTemplateStringsEqual(left.ScreenshotsFolder, right.ScreenshotsFolder)
                && left.UsedFilePattern == right.UsedFilePattern
                && AreFolderTemplateStringsEqual(left.FilePattern, right.FilePattern)
                && left.ScanSubFolders == right.ScanSubFolders
                && SsvGlobalSourceApplicabilityHelper.AreApplicabilitySettingsEqual(left, right);
        }

        /// <summary>
        /// Compares optional folder template strings; two null or blank values are treated as equal.
        /// </summary>
        /// <param name="left">First string.</param>
        /// <param name="right">Second string.</param>
        /// <returns><c>true</c> when both are empty or equal per <see cref="StringExtensions.IsEqual"/>.</returns>
        public static bool AreFolderTemplateStringsEqual(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) && string.IsNullOrWhiteSpace(right))
            {
                return true;
            }

            return left.IsEqual(right);
        }

        private static void NotifyPresetAlreadyInGlobal(string pluginName)
        {
            if (string.IsNullOrEmpty(pluginName))
            {
                return;
            }

            API.Instance.Notifications.Add(new NotificationMessage(
                string.Format("{0}-PresetAlreadyInGlobal", pluginName),
                string.Format(
                    "{0}\r\n{1}",
                    pluginName,
                    ResourceProvider.GetString("LOCSsvPresetAlreadyInGlobal")),
                NotificationType.Info));
        }
    }
}
