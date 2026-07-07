using CommonPluginsShared;
using Playnite.SDK;
using ScreenshotsVisualizer.Models;
using System.Collections.Generic;

namespace ScreenshotsVisualizer.ViewModels.Settings
{
    /// <summary>
    /// View model row for a built-in quick-add preset button in global settings.
    /// </summary>
    public sealed class SsvFolderPresetQuickAddItem : ObservableObject
    {
        /// <summary>
        /// Initializes a quick-add item from a catalog preset definition.
        /// </summary>
        /// <param name="preset">Built-in preset metadata.</param>
        public SsvFolderPresetQuickAddItem(SsvFolderPreset preset)
        {
            PresetId = preset.Id;
            IconGlyph = preset.IconGlyph;
            ToolTip = ResolveToolTip(preset);
        }

        /// <summary>
        /// Gets the preset identifier passed to the apply command.
        /// </summary>
        public SsvFolderPresetId PresetId { get; }

        /// <summary>
        /// Gets the CommonFont glyph displayed on the button.
        /// </summary>
        public string IconGlyph { get; }

        /// <summary>
        /// Gets the resolved tooltip text for the button.
        /// </summary>
        public string ToolTip { get; }

        private bool _canApply = true;

        /// <summary>
        /// Gets or sets whether the preset can still be added to global sources.
        /// </summary>
        public bool CanApply
        {
            get => _canApply;
            set => SetValue(ref _canApply, value);
        }

        private static string ResolveToolTip(SsvFolderPreset preset)
        {
            if (!string.IsNullOrEmpty(preset.TooltipLocalizationKey))
            {
                return ResourceProvider.GetString(preset.TooltipLocalizationKey);
            }

            if (!string.IsNullOrEmpty(preset.DisplayNameLocalizationKey))
            {
                return ResourceProvider.GetString(preset.DisplayNameLocalizationKey);
            }

            return string.Empty;
        }
    }
}
