namespace ScreenshotsVisualizer.Models
{
    /// <summary>
    /// Result of the one-shot archive configuration migration.
    /// </summary>
    public class SsvArchiveMigrationResult
    {
        /// <summary>
        /// Gets or sets whether the migration completed successfully.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets whether the migration was skipped (already done or nothing to clean).
        /// </summary>
        public bool Skipped { get; set; }

        /// <summary>
        /// Gets or sets how many duplicate archive entries were removed from persisted settings.
        /// </summary>
        public int RemovedDuplicates { get; set; }

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
