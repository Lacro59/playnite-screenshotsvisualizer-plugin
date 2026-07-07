using System;
using System.Collections.Generic;
using ScreenshotsVisualizer.Models;

namespace ScreenshotsVisualizer.Models
{
    /// <summary>
    /// Per-game summary of persisted preset folder entries removable during global preset migration.
    /// </summary>
    public class SsvGamePresetDuplicateEntry
    {
        /// <summary>
        /// Gets or sets the Playnite game identifier.
        /// </summary>
        public Guid GameId { get; set; }

        /// <summary>
        /// Gets or sets how many <see cref="FolderSettings"/> list entries match a migratable preset.
        /// </summary>
        public int ListDuplicateCount { get; set; }

        /// <summary>
        /// Gets or sets preset identifiers detected among removable duplicates for this game.
        /// </summary>
        public List<SsvFolderPresetId> MatchedPresets { get; set; } = new List<SsvFolderPresetId>();
    }

    /// <summary>
    /// Result of scanning persisted settings for preset folder duplicates migratable to globals.
    /// </summary>
    public class SsvPresetDuplicateAnalysis
    {
        /// <summary>
        /// Gets or sets how many configured games have at least one removable preset duplicate.
        /// </summary>
        public int GamesWithDuplicates { get; set; }

        /// <summary>
        /// Gets or sets the total removable entries in per-game <see cref="GameSettings.ScreenshotsFolders"/> lists.
        /// </summary>
        public int RemovableListEntries { get; set; }

        /// <summary>
        /// Gets or sets how many legacy global source rows should be replaced by canonical presets.
        /// </summary>
        public int RemovableLegacyGlobalEntries { get; set; }

        /// <summary>
        /// Gets the total number of removable duplicates across globals and per-game lists.
        /// </summary>
        public int TotalRemovable => RemovableListEntries + RemovableLegacyGlobalEntries;

        /// <summary>
        /// Gets or sets preset identifiers missing from <see cref="ScreenshotsVisualizerSettings.GlobalScreenshotSources"/>.
        /// </summary>
        public List<SsvFolderPresetId> MissingGlobalPresets { get; set; } = new List<SsvFolderPresetId>();

        /// <summary>
        /// Gets or sets per-game duplicate details (games with zero duplicates are omitted).
        /// </summary>
        public List<SsvGamePresetDuplicateEntry> Games { get; set; } = new List<SsvGamePresetDuplicateEntry>();
    }
}
