namespace ScreenshotsVisualizer.Models
{
    /// <summary>
    /// Result of the one-shot global preset folder migration.
    /// </summary>
    public class SsvPresetMigrationResult
    {
        /// <summary>
        /// Gets or sets whether the migration completed successfully.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets how many canonical global preset rows were added.
        /// </summary>
        public int GlobalPresetsAdded { get; set; }

        /// <summary>
        /// Gets or sets how many legacy global preset rows were removed.
        /// </summary>
        public int LegacyGlobalRemoved { get; set; }

        /// <summary>
        /// Gets or sets how many migratable per-game preset duplicates were removed.
        /// </summary>
        public int PerGameDuplicatesRemoved { get; set; }

        /// <summary>
        /// Gets or sets how many empty <see cref="GameSettings"/> entries were removed.
        /// </summary>
        public int EmptyGameSettingsRemoved { get; set; }

        /// <summary>
        /// Gets the total number of persisted settings changes applied.
        /// </summary>
        public int TotalChanges =>
            GlobalPresetsAdded + LegacyGlobalRemoved + PerGameDuplicatesRemoved + EmptyGameSettingsRemoved;

        /// <summary>
        /// Gets or sets the backup ZIP path when a backup was created.
        /// </summary>
        public string ArchivePath { get; set; }

        /// <summary>
        /// Gets or sets an error description when <see cref="Success"/> is false.
        /// </summary>
        public string ErrorMessage { get; set; }
    }
}
