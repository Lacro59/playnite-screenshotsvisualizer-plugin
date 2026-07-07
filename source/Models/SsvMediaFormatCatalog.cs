using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ScreenshotsVisualizer.Models
{
    /// <summary>
    /// Central registry of screenshot media extensions and user-enabled scan filters.
    /// </summary>
    public static class SsvMediaFormatCatalog
    {
        /// <summary>
        /// Increment when built-in formats with <see cref="SsvMediaFormatDefinition.EnabledByDefault"/> are added
        /// so existing installations receive a one-time merge without overriding user removals.
        /// </summary>
        public const int CurrentMediaFormatCatalogRevision = 1;

        private const string WicNativeHintKey = "LOCSsvMediaFormatCodecWicNative";
        private const string WicExtensionHintKey = "LOCSsvMediaFormatCodecWicExtension";
        private const string CustomDecoderHintKey = "LOCSsvMediaFormatCodecCustomDecoder";
        private const string FfmpegHintKey = "LOCSsvMediaFormatCodecFfmpeg";
        private const string CustomUnknownHintKey = "LOCSsvMediaFormatCodecCustomUnknown";

        private static readonly Regex ExtensionPattern = new Regex(
            @"^\.[a-z0-9]{1,9}$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly IReadOnlyList<SsvMediaFormatDefinition> Definitions = new List<SsvMediaFormatDefinition>
        {
            new SsvMediaFormatDefinition(".jpg", SsvMediaFormatKind.Image, true, true, false, false, WicNativeHintKey),
            new SsvMediaFormatDefinition(".jpeg", SsvMediaFormatKind.Image, true, true, false, false, WicNativeHintKey),
            new SsvMediaFormatDefinition(".jfif", SsvMediaFormatKind.Image, true, false, false, false, WicNativeHintKey),
            new SsvMediaFormatDefinition(".png", SsvMediaFormatKind.Image, true, true, false, false, WicNativeHintKey),
            new SsvMediaFormatDefinition(".gif", SsvMediaFormatKind.Image, true, false, false, false, WicNativeHintKey),
            new SsvMediaFormatDefinition(".bmp", SsvMediaFormatKind.Image, true, false, false, false, WicNativeHintKey),
            new SsvMediaFormatDefinition(".webp", SsvMediaFormatKind.Image, true, false, true, false, WicExtensionHintKey),
            new SsvMediaFormatDefinition(".tga", SsvMediaFormatKind.Image, true, false, false, true, CustomDecoderHintKey),
            new SsvMediaFormatDefinition(".avif", SsvMediaFormatKind.Image, true, false, true, false, WicExtensionHintKey),
            new SsvMediaFormatDefinition(".heic", SsvMediaFormatKind.Image, false, false, true, false, WicExtensionHintKey),
            new SsvMediaFormatDefinition(".mp4", SsvMediaFormatKind.Video, true, true, false, false, FfmpegHintKey),
            new SsvMediaFormatDefinition(".avi", SsvMediaFormatKind.Video, true, false, false, false, FfmpegHintKey),
            new SsvMediaFormatDefinition(".mkv", SsvMediaFormatKind.Video, true, false, false, false, FfmpegHintKey),
            new SsvMediaFormatDefinition(".webm", SsvMediaFormatKind.Video, true, false, false, false, FfmpegHintKey)
        };

        private static readonly HashSet<string> CatalogVideoExtensions = new HashSet<string>(
            Definitions.Where(x => x.Kind == SsvMediaFormatKind.Video).Select(x => x.Extension),
            StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> CatalogImageExtensions = new HashSet<string>(
            Definitions.Where(x => x.Kind == SsvMediaFormatKind.Image).Select(x => x.Extension),
            StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Localization key used for user-defined extension tooltips.
        /// </summary>
        public static string CustomUnknownHintResourceKey => CustomUnknownHintKey;

        /// <summary>
        /// Returns all known media format definitions.
        /// </summary>
        public static IReadOnlyList<SsvMediaFormatDefinition> GetAllDefinitions()
        {
            return Definitions;
        }

        /// <summary>
        /// Returns definitions for the requested media category.
        /// </summary>
        /// <param name="kind">Image or video category filter.</param>
        public static IEnumerable<SsvMediaFormatDefinition> GetDefinitions(SsvMediaFormatKind kind)
        {
            return Definitions.Where(x => x.Kind == kind);
        }

        /// <summary>
        /// Returns default enabled extensions for a media category.
        /// </summary>
        /// <param name="kind">Image or video category.</param>
        public static List<string> GetDefaultEnabledExtensions(SsvMediaFormatKind kind)
        {
            return Definitions
                .Where(x => x.Kind == kind && x.EnabledByDefault)
                .Select(x => x.Extension)
                .ToList();
        }

        /// <summary>
        /// Returns extensions that must stay enabled when settings are saved.
        /// </summary>
        /// <param name="kind">Image or video category.</param>
        public static IEnumerable<string> GetEssentialExtensions(SsvMediaFormatKind kind)
        {
            return Definitions
                .Where(x => x.Kind == kind && x.IsEssential)
                .Select(x => x.Extension);
        }

        /// <summary>
        /// Tries to get a built-in catalog definition for an extension.
        /// </summary>
        /// <param name="extension">Normalized or raw extension.</param>
        public static SsvMediaFormatDefinition TryGetDefinition(string extension)
        {
            string normalized = NormalizeExtension(extension);
            if (string.IsNullOrEmpty(normalized))
            {
                return null;
            }

            return Definitions.FirstOrDefault(x =>
                string.Equals(x.Extension, normalized, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Returns the merged active extension list for UI display (catalog enabled + custom).
        /// </summary>
        /// <param name="settings">Current plugin settings.</param>
        /// <param name="kind">Image or video category.</param>
        public static List<string> GetMergedActiveExtensions(ScreenshotsVisualizerSettings settings, SsvMediaFormatKind kind)
        {
            EnsureSettingsInitialized(settings);

            HashSet<string> merged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> enabled = kind == SsvMediaFormatKind.Image
                ? settings.EnabledImageExtensions
                : settings.EnabledVideoExtensions;
            List<string> custom = kind == SsvMediaFormatKind.Image
                ? settings.CustomImageExtensions
                : settings.CustomVideoExtensions;

            AddEnabledExtensions(merged, enabled);
            AddEnabledExtensions(merged, custom);
            return SortActiveExtensions(merged, kind);
        }

        /// <summary>
        /// Persists an active extension list from the settings UI into catalog and custom lists.
        /// </summary>
        /// <param name="settings">Plugin settings to update.</param>
        /// <param name="kind">Image or video category.</param>
        /// <param name="activeExtensions">Extensions currently shown in the list view.</param>
        public static void ApplyActiveExtensions(
            ScreenshotsVisualizerSettings settings,
            SsvMediaFormatKind kind,
            IEnumerable<string> activeExtensions)
        {
            if (settings == null)
            {
                return;
            }

            HashSet<string> catalog = kind == SsvMediaFormatKind.Video
                ? CatalogVideoExtensions
                : CatalogImageExtensions;

            List<string> catalogActive = new List<string>();
            List<string> customActive = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string rawExtension in activeExtensions ?? Enumerable.Empty<string>())
            {
                string normalized = NormalizeExtension(rawExtension);
                if (string.IsNullOrEmpty(normalized) || !seen.Add(normalized))
                {
                    continue;
                }

                if (catalog.Contains(normalized))
                {
                    catalogActive.Add(normalized);
                }
                else if (TryNormalizeCustomExtension(normalized, out string customExtension))
                {
                    customActive.Add(customExtension);
                }
            }

            if (kind == SsvMediaFormatKind.Image)
            {
                settings.EnabledImageExtensions = catalogActive;
                settings.CustomImageExtensions = customActive;
            }
            else
            {
                settings.EnabledVideoExtensions = catalogActive;
                settings.CustomVideoExtensions = customActive;
            }

            settings.MediaFormatsConfigured = true;
            EnsureEssentialExtensions(settings);
        }

        /// <summary>
        /// Returns whether an extension is in the active list for the given category.
        /// </summary>
        /// <param name="extension">Extension to check.</param>
        /// <param name="kind">Image or video category.</param>
        /// <param name="activeExtensions">Current active extensions.</param>
        public static bool IsInActiveList(string extension, SsvMediaFormatKind kind, IEnumerable<string> activeExtensions)
        {
            string normalized = NormalizeExtension(extension);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            return activeExtensions != null && activeExtensions.Any(x =>
                string.Equals(NormalizeExtension(x), normalized, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Tries to normalize and validate a user-entered file extension.
        /// </summary>
        /// <param name="input">Raw user input.</param>
        /// <param name="normalizedExtension">Normalized lower-case extension including the leading dot.</param>
        /// <returns><c>true</c> when the extension is valid.</returns>
        public static bool TryNormalizeCustomExtension(string input, out string normalizedExtension)
        {
            normalizedExtension = NormalizeExtension(input);
            return !string.IsNullOrEmpty(normalizedExtension) && ExtensionPattern.IsMatch(normalizedExtension);
        }

        /// <summary>
        /// Tries to normalize an extension for addition to the active list (catalog or custom).
        /// </summary>
        /// <param name="input">Raw user input.</param>
        /// <param name="kind">Image or video category.</param>
        /// <param name="normalizedExtension">Normalized extension.</param>
        /// <returns><c>true</c> when the extension can be added.</returns>
        public static bool TryNormalizeForActiveList(string input, SsvMediaFormatKind kind, out string normalizedExtension)
        {
            string candidate = NormalizeExtension(input);
            if (string.IsNullOrEmpty(candidate))
            {
                normalizedExtension = string.Empty;
                return false;
            }

            if (TryGetDefinition(candidate) != null)
            {
                normalizedExtension = candidate;
                return GetDefinitions(kind).Any(x =>
                    string.Equals(x.Extension, candidate, StringComparison.OrdinalIgnoreCase));
            }

            return TryNormalizeCustomExtension(candidate, out normalizedExtension);
        }

        /// <summary>
        /// Ensures extension lists exist, applies one-time catalog migrations, and enforces essential formats.
        /// </summary>
        /// <param name="settings">Plugin settings instance to initialize.</param>
        public static void EnsureSettingsInitialized(ScreenshotsVisualizerSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            EnsureExtensionListsExist(settings);

            bool needsInitialDefaults = !settings.MediaFormatsConfigured
                && settings.MediaFormatCatalogRevision < CurrentMediaFormatCatalogRevision;

            if (needsInitialDefaults && settings.EnabledImageExtensions.Count == 0)
            {
                settings.EnabledImageExtensions = GetDefaultEnabledExtensions(SsvMediaFormatKind.Image);
            }

            if (needsInitialDefaults && settings.EnabledVideoExtensions.Count == 0)
            {
                settings.EnabledVideoExtensions = GetDefaultEnabledExtensions(SsvMediaFormatKind.Video);
            }

            if (settings.MediaFormatCatalogRevision < CurrentMediaFormatCatalogRevision)
            {
                MergeMissingDefaults(settings.EnabledImageExtensions, SsvMediaFormatKind.Image);
                MergeMissingDefaults(settings.EnabledVideoExtensions, SsvMediaFormatKind.Video);
                settings.MediaFormatCatalogRevision = CurrentMediaFormatCatalogRevision;
            }

            NormalizeExtensionList(settings.EnabledImageExtensions);
            NormalizeExtensionList(settings.EnabledVideoExtensions);
            NormalizeCustomExtensionList(settings.CustomImageExtensions);
            NormalizeCustomExtensionList(settings.CustomVideoExtensions);
            EnsureEssentialExtensions(settings);
        }

        /// <summary>
        /// Returns enabled extensions for filesystem scan, case-insensitive.
        /// </summary>
        /// <param name="settings">Current plugin settings.</param>
        public static HashSet<string> GetEnabledScanExtensions(ScreenshotsVisualizerSettings settings)
        {
            EnsureSettingsInitialized(settings);

            HashSet<string> enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddEnabledExtensions(enabled, settings.EnabledImageExtensions);
            AddEnabledExtensions(enabled, settings.EnabledVideoExtensions);
            AddEnabledExtensions(enabled, settings.CustomImageExtensions);
            AddEnabledExtensions(enabled, settings.CustomVideoExtensions);
            return enabled;
        }

        /// <summary>
        /// Returns whether the file path has a known video extension.
        /// </summary>
        /// <param name="path">Absolute or relative file path.</param>
        /// <param name="settings">Optional plugin settings for custom video extensions.</param>
        public static bool IsVideoExtension(string path, ScreenshotsVisualizerSettings settings = null)
        {
            string extension = NormalizeExtension(Path.GetExtension(path));
            if (string.IsNullOrEmpty(extension))
            {
                return false;
            }

            if (CatalogVideoExtensions.Contains(extension))
            {
                return true;
            }

            if (settings?.CustomVideoExtensions != null)
            {
                return settings.CustomVideoExtensions.Any(x =>
                    string.Equals(NormalizeExtension(x), extension, StringComparison.OrdinalIgnoreCase));
            }

            return false;
        }

        /// <summary>
        /// Returns whether the file path has a known image extension.
        /// </summary>
        /// <param name="path">Absolute or relative file path.</param>
        /// <param name="settings">Optional plugin settings for custom image extensions.</param>
        public static bool IsImageExtension(string path, ScreenshotsVisualizerSettings settings = null)
        {
            string extension = NormalizeExtension(Path.GetExtension(path));
            if (string.IsNullOrEmpty(extension))
            {
                return false;
            }

            if (CatalogImageExtensions.Contains(extension))
            {
                return true;
            }

            if (settings?.CustomImageExtensions != null)
            {
                return settings.CustomImageExtensions.Any(x =>
                    string.Equals(NormalizeExtension(x), extension, StringComparison.OrdinalIgnoreCase));
            }

            return false;
        }

        /// <summary>
        /// Returns whether the extension already exists in the catalog or custom lists.
        /// </summary>
        /// <param name="extension">Normalized extension.</param>
        /// <param name="kind">Image or video category.</param>
        /// <param name="settings">Current plugin settings.</param>
        public static bool IsKnownExtension(string extension, SsvMediaFormatKind kind, ScreenshotsVisualizerSettings settings)
        {
            string normalized = NormalizeExtension(extension);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            HashSet<string> catalog = kind == SsvMediaFormatKind.Video
                ? CatalogVideoExtensions
                : CatalogImageExtensions;

            if (catalog.Contains(normalized))
            {
                return true;
            }

            List<string> customList = kind == SsvMediaFormatKind.Video
                ? settings?.CustomVideoExtensions
                : settings?.CustomImageExtensions;

            return customList != null && customList.Any(x =>
                string.Equals(NormalizeExtension(x), normalized, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Normalizes persisted extension lists and enforces essential built-in formats.
        /// </summary>
        /// <param name="settings">Plugin settings to normalize.</param>
        public static void NormalizeForPersistence(ScreenshotsVisualizerSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            settings.MediaFormatsConfigured = true;
            EnsureExtensionListsExist(settings);
            settings.EnabledImageExtensions = FilterKnownCatalogExtensions(settings.EnabledImageExtensions, SsvMediaFormatKind.Image);
            settings.EnabledVideoExtensions = FilterKnownCatalogExtensions(settings.EnabledVideoExtensions, SsvMediaFormatKind.Video);
            settings.CustomImageExtensions = FilterValidCustomExtensions(settings.CustomImageExtensions, SsvMediaFormatKind.Image, settings);
            settings.CustomVideoExtensions = FilterValidCustomExtensions(settings.CustomVideoExtensions, SsvMediaFormatKind.Video, settings);
            EnsureEssentialExtensions(settings);
            NormalizeExtensionList(settings.EnabledImageExtensions);
            NormalizeExtensionList(settings.EnabledVideoExtensions);
            NormalizeCustomExtensionList(settings.CustomImageExtensions);
            NormalizeCustomExtensionList(settings.CustomVideoExtensions);
        }

        private static void EnsureExtensionListsExist(ScreenshotsVisualizerSettings settings)
        {
            if (settings.CustomImageExtensions == null)
            {
                settings.CustomImageExtensions = new List<string>();
            }

            if (settings.CustomVideoExtensions == null)
            {
                settings.CustomVideoExtensions = new List<string>();
            }

            if (settings.EnabledImageExtensions == null)
            {
                settings.EnabledImageExtensions = new List<string>();
            }

            if (settings.EnabledVideoExtensions == null)
            {
                settings.EnabledVideoExtensions = new List<string>();
            }
        }

        private static void EnsureEssentialExtensions(ScreenshotsVisualizerSettings settings)
        {
            MergeRequiredExtensions(settings.EnabledImageExtensions, SsvMediaFormatKind.Image);
            MergeRequiredExtensions(settings.EnabledVideoExtensions, SsvMediaFormatKind.Video);
        }

        private static void MergeRequiredExtensions(List<string> enabledList, SsvMediaFormatKind kind)
        {
            HashSet<string> existing = new HashSet<string>(
                enabledList.Select(NormalizeExtension).Where(x => !string.IsNullOrEmpty(x)),
                StringComparer.OrdinalIgnoreCase);

            foreach (string extension in GetEssentialExtensions(kind))
            {
                if (!existing.Contains(extension))
                {
                    enabledList.Add(extension);
                }
            }
        }

        private static void MergeMissingDefaults(List<string> enabledList, SsvMediaFormatKind kind)
        {
            HashSet<string> existing = new HashSet<string>(
                enabledList.Select(NormalizeExtension).Where(x => !string.IsNullOrEmpty(x)),
                StringComparer.OrdinalIgnoreCase);

            foreach (SsvMediaFormatDefinition definition in GetDefinitions(kind).Where(x => x.EnabledByDefault))
            {
                if (!existing.Contains(definition.Extension))
                {
                    enabledList.Add(definition.Extension);
                }
            }
        }

        private static List<string> FilterKnownCatalogExtensions(List<string> extensions, SsvMediaFormatKind kind)
        {
            HashSet<string> known = new HashSet<string>(
                GetDefinitions(kind).Select(x => x.Extension),
                StringComparer.OrdinalIgnoreCase);

            return extensions
                .Select(NormalizeExtension)
                .Where(x => !string.IsNullOrEmpty(x) && known.Contains(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> FilterValidCustomExtensions(
            List<string> extensions,
            SsvMediaFormatKind kind,
            ScreenshotsVisualizerSettings settings)
        {
            if (extensions == null)
            {
                return new List<string>();
            }

            HashSet<string> catalog = kind == SsvMediaFormatKind.Video
                ? CatalogVideoExtensions
                : CatalogImageExtensions;

            List<string> filtered = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string extension in extensions)
            {
                if (!TryNormalizeCustomExtension(extension, out string normalized))
                {
                    continue;
                }

                if (catalog.Contains(normalized) || seen.Contains(normalized))
                {
                    continue;
                }

                seen.Add(normalized);
                filtered.Add(normalized);
            }

            return filtered;
        }

        private static void NormalizeCustomExtensionList(List<string> extensions)
        {
            if (extensions == null)
            {
                return;
            }

            for (int index = extensions.Count - 1; index >= 0; index--)
            {
                if (!TryNormalizeCustomExtension(extensions[index], out string normalized))
                {
                    extensions.RemoveAt(index);
                    continue;
                }

                extensions[index] = normalized;
            }
        }

        private static List<string> SortActiveExtensions(HashSet<string> extensions, SsvMediaFormatKind kind)
        {
            if (extensions == null || extensions.Count == 0)
            {
                return new List<string>();
            }

            List<string> ordered = new List<string>();
            HashSet<string> remaining = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);

            foreach (SsvMediaFormatDefinition definition in GetDefinitions(kind))
            {
                if (remaining.Remove(definition.Extension))
                {
                    ordered.Add(definition.Extension);
                }
            }

            foreach (string extension in remaining.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                ordered.Add(extension);
            }

            return ordered;
        }

        private static void AddEnabledExtensions(HashSet<string> target, List<string> enabledList)
        {
            if (enabledList == null)
            {
                return;
            }

            foreach (string extension in enabledList)
            {
                string normalized = NormalizeExtension(extension);
                if (!string.IsNullOrEmpty(normalized))
                {
                    target.Add(normalized);
                }
            }
        }

        private static void NormalizeExtensionList(List<string> extensions)
        {
            if (extensions == null)
            {
                return;
            }

            for (int index = 0; index < extensions.Count; index++)
            {
                extensions[index] = NormalizeExtension(extensions[index]);
            }
        }

        private static string NormalizeExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return string.Empty;
            }

            string trimmed = extension.Trim().ToLowerInvariant();
            if (trimmed.Length == 0)
            {
                return string.Empty;
            }

            if (!trimmed.StartsWith(".", StringComparison.Ordinal))
            {
                trimmed = "." + trimmed;
            }

            return trimmed;
        }
    }
}
