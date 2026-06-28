using CommonPluginsShared.Interfaces;

namespace ScreenshotsVisualizer.Models
{
    /// <summary>
    /// Describes a built-in global screenshot folder preset (path template, scan options, applicability).
    /// </summary>
    public sealed class SsvFolderPreset
    {
        /// <summary>
        /// Initializes a new folder preset definition.
        /// </summary>
        /// <param name="id">Preset identifier.</param>
        /// <param name="displayNameLocalizationKey">Settings UI display name resource key.</param>
        /// <param name="tooltipLocalizationKey">Optional settings UI tooltip resource key.</param>
        /// <param name="iconGlyph">CommonFont glyph for the quick-add button.</param>
        public SsvFolderPreset(
            SsvFolderPresetId id,
            string displayNameLocalizationKey,
            string tooltipLocalizationKey,
            string iconGlyph)
        {
            Id = id;
            DisplayNameLocalizationKey = displayNameLocalizationKey ?? string.Empty;
            TooltipLocalizationKey = tooltipLocalizationKey ?? string.Empty;
            IconGlyph = iconGlyph ?? string.Empty;
        }

        /// <summary>
        /// Gets the preset identifier.
        /// </summary>
        public SsvFolderPresetId Id { get; }

        /// <summary>
        /// Gets the localization key for preset display text in settings UI.
        /// </summary>
        public string DisplayNameLocalizationKey { get; }

        /// <summary>
        /// Gets the localization key for preset tooltip text in settings UI.
        /// </summary>
        public string TooltipLocalizationKey { get; }

        /// <summary>
        /// Gets the CommonFont icon glyph shown on the quick-add button.
        /// </summary>
        public string IconGlyph { get; }

        /// <summary>
        /// Creates canonical <see cref="FolderSettings"/> for <see cref="ScreenshotsVisualizerSettings.GlobalScreenshotSources"/>.
        /// </summary>
        /// <returns>A new folder settings instance with default applicability for this preset.</returns>
        public FolderSettings CreateGlobalFolderSettings()
        {
            switch (Id)
            {
                case SsvFolderPresetId.Steam:
                    return new FolderSettings
                    {
                        ScreenshotsFolder = "{SteamScreenshotsDir}\\{GameId}\\screenshots",
                        ApplicableSourceFilterMode = SourceFilterMode.Whitelist,
                        ApplicableSources = new System.Collections.Generic.List<string> { "Steam" }
                    };

                case SsvFolderPresetId.Gog:
                    return new FolderSettings
                    {
                        ScreenshotsFolder = "{GogScreenshotDir}\\{Name}",
                        ApplicableSourceFilterMode = SourceFilterMode.Whitelist,
                        ApplicableSources = new System.Collections.Generic.List<string> { "GOG" }
                    };

                case SsvFolderPresetId.Ubisoft:
                    return new FolderSettings
                    {
                        ScreenshotsFolder = "{UbisoftScreenshotsDir}\\{Name}",
                        ApplicableSourceFilterMode = SourceFilterMode.Whitelist,
                        ApplicableSources = new System.Collections.Generic.List<string> { "Ubisoft Connect", "Uplay" }
                    };

                case SsvFolderPresetId.RetroArch:
                    return new FolderSettings
                    {
                        ScreenshotsFolder = "{RetroArchScreenshotsDir}",
                        UsedFilePattern = true,
                        FilePattern = "{ImageNameNoExt}-{digit}-{digit}",
                        ApplicableEmulatorFilter = SsvApplicableEmulatorFilter.RetroArch
                    };

                case SsvFolderPresetId.ScummVM:
                    return new FolderSettings
                    {
                        ScreenshotsFolder = "{UserProfile}\\Pictures\\ScummVM Screenshots",
                        UsedFilePattern = true,
                        FilePattern = "scummvm-{ImageNameNoExt}-{digit}",
                        ApplicableEmulatorFilter = SsvApplicableEmulatorFilter.ScummVM
                    };

                case SsvFolderPresetId.XboxGameBar:
                    return new FolderSettings
                    {
                        ScreenshotsFolder = "{XboxGamebarScreenshotsDir}",
                        UsedFilePattern = true,
                        FilePattern = "{Name} *"
                    };

                default:
                    return new FolderSettings();
            }
        }
    }
}
