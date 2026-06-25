using System;
using System.Collections.Generic;

namespace ScreenshotsVisualizer.Models
{
    /// <summary>
    /// Per-game summary of persisted archive folder entries that duplicate the global archive configuration.
    /// </summary>
    public class SsvGameArchiveDuplicateEntry
    {
        /// <summary>
        /// Gets or sets the Playnite game identifier.
        /// </summary>
        public Guid GameId { get; set; }

        /// <summary>
        /// Gets or sets how many <see cref="FolderSettings"/> list entries match the global archive strictly.
        /// </summary>
        public int ListDuplicateCount { get; set; }

        /// <summary>
        /// Gets or sets whether legacy game-level folder fields match the global archive strictly.
        /// </summary>
        public bool LegacyDuplicate { get; set; }

        /// <summary>
        /// Gets the total number of removable duplicates for this game.
        /// </summary>
        public int TotalRemovable => ListDuplicateCount + (LegacyDuplicate ? 1 : 0);
    }

    /// <summary>
    /// Result of scanning persisted <c>gameSettings</c> for archive folder duplicates.
    /// </summary>
    public class SsvArchiveDuplicateAnalysis
    {
        /// <summary>
        /// Gets or sets whether a global archive reference was available for comparison.
        /// </summary>
        public bool HasGlobalArchiveReference { get; set; }

        /// <summary>
        /// Gets or sets how many configured games have at least one removable archive duplicate.
        /// </summary>
        public int GamesWithDuplicates { get; set; }

        /// <summary>
        /// Gets or sets the total removable entries in <see cref="GameSettings.ScreenshotsFolders"/> lists.
        /// </summary>
        public int RemovableListEntries { get; set; }

        /// <summary>
        /// Gets or sets the total removable legacy game-level archive duplicates.
        /// </summary>
        public int RemovableLegacyEntries { get; set; }

        /// <summary>
        /// Gets the total number of removable duplicates across all games.
        /// </summary>
        public int TotalRemovable => RemovableListEntries + RemovableLegacyEntries;

        /// <summary>
        /// Gets or sets per-game duplicate details (games with zero duplicates are omitted).
        /// </summary>
        public List<SsvGameArchiveDuplicateEntry> Games { get; set; } = new List<SsvGameArchiveDuplicateEntry>();
    }
}
