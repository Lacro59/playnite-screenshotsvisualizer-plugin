using CommonPluginsShared.Extensions;
using ScreenshotsVisualizer.Models;
using System.Collections.Generic;
using System.Linq;

namespace ScreenshotsVisualizer.Services
{
    /// <summary>
    /// Static registry of built-in global folder presets (Steam, Ubisoft, RetroArch, ScummVM).
    /// </summary>
    public static class SsvFolderPresetCatalog
    {
        private static readonly IReadOnlyList<SsvFolderPreset> Presets = new List<SsvFolderPreset>
        {
            new SsvFolderPreset(SsvFolderPresetId.Steam, "LOCSsvAddSteamGame"),
            new SsvFolderPreset(SsvFolderPresetId.Ubisoft, string.Empty),
            new SsvFolderPreset(SsvFolderPresetId.ScummVM, string.Empty),
            new SsvFolderPreset(SsvFolderPresetId.RetroArch, string.Empty)
        };

        /// <summary>
        /// Gets all built-in folder presets in display order.
        /// </summary>
        /// <returns>Read-only preset list.</returns>
        public static IReadOnlyList<SsvFolderPreset> GetAll()
        {
            return Presets;
        }

        /// <summary>
        /// Tries to resolve a built-in preset by identifier.
        /// </summary>
        /// <param name="id">Preset identifier.</param>
        /// <param name="preset">Resolved preset when found.</param>
        /// <returns><c>true</c> when the identifier is registered.</returns>
        public static bool TryGet(SsvFolderPresetId id, out SsvFolderPreset preset)
        {
            preset = Presets.FirstOrDefault(x => x.Id == id);
            return preset != null;
        }

        /// <summary>
        /// Creates canonical global folder settings for the specified preset.
        /// </summary>
        /// <param name="id">Preset identifier.</param>
        /// <returns>Folder settings template, or an empty instance when unknown.</returns>
        public static FolderSettings CreateGlobalFolderSettings(SsvFolderPresetId id)
        {
            return TryGet(id, out SsvFolderPreset preset)
                ? preset.CreateGlobalFolderSettings()
                : new FolderSettings();
        }

        /// <summary>
        /// Returns whether <paramref name="folderSettings"/> matches the canonical global template for <paramref name="id"/>.
        /// </summary>
        /// <param name="folderSettings">Folder settings to compare.</param>
        /// <param name="id">Preset identifier.</param>
        /// <returns><c>true</c> when path, scan options, and applicability match the catalog entry.</returns>
        public static bool IsCanonicalGlobalFolderSettings(FolderSettings folderSettings, SsvFolderPresetId id)
        {
            if (folderSettings == null || !TryGet(id, out SsvFolderPreset preset))
            {
                return false;
            }

            FolderSettings canonical = preset.CreateGlobalFolderSettings();
            return SsvFolderPresetService.AreFolderTemplateStringsEqual(canonical.ScreenshotsFolder, folderSettings.ScreenshotsFolder)
                && canonical.UsedFilePattern == folderSettings.UsedFilePattern
                && SsvFolderPresetService.AreFolderTemplateStringsEqual(canonical.FilePattern, folderSettings.FilePattern)
                && canonical.ScanSubFolders == folderSettings.ScanSubFolders
                && SsvGlobalSourceApplicabilityHelper.AreApplicabilitySettingsEqual(canonical, folderSettings);
        }

        /// <summary>
        /// Returns whether <paramref name="folderSettings"/> matches any built-in canonical global preset.
        /// </summary>
        /// <param name="folderSettings">Folder settings to compare.</param>
        /// <param name="matchedPresetId">Matched preset identifier when found.</param>
        /// <returns><c>true</c> when a catalog preset matches.</returns>
        public static bool TryMatchCanonicalGlobalFolderSettings(
            FolderSettings folderSettings,
            out SsvFolderPresetId matchedPresetId)
        {
            foreach (SsvFolderPreset preset in Presets)
            {
                if (IsCanonicalGlobalFolderSettings(folderSettings, preset.Id))
                {
                    matchedPresetId = preset.Id;
                    return true;
                }
            }

            matchedPresetId = default(SsvFolderPresetId);
            return false;
        }
    }
}
