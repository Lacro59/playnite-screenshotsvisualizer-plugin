namespace ScreenshotsVisualizer.Models
{
    /// <summary>
    /// Result of creating a ZIP backup of plugin settings before archive configuration migration.
    /// </summary>
    public class SsvArchiveSettingsBackupResult
    {
        /// <summary>
        /// Gets or sets whether the backup completed successfully.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the full path of the created ZIP archive when <see cref="Success"/> is true.
        /// </summary>
        public string ArchivePath { get; set; }

        /// <summary>
        /// Gets or sets how many files were included in the archive.
        /// </summary>
        public int ArchivedFileCount { get; set; }

        /// <summary>
        /// Gets or sets an error description when <see cref="Success"/> is false.
        /// </summary>
        public string ErrorMessage { get; set; }
    }
}
