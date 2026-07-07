namespace ScreenshotsVisualizer.Services
{
    /// <summary>
    /// Result of a screenshot delete operation performed by <see cref="ScreenshotsVisualizerDatabase.TryDeleteScreenshot"/>.
    /// </summary>
    public enum SsvScreenshotDeleteResult
    {
        /// <summary>Database entry removed; physical file delete scheduled or not required.</summary>
        Success,

        /// <summary>No <see cref="Models.GameScreenshots"/> entry found for the game identifier.</summary>
        GameNotFound,

        /// <summary>The screenshot is not present in the game collection.</summary>
        ScreenshotNotInCollection,

        /// <summary>Database updated but the physical file could not be sent to the recycle bin.</summary>
        PhysicalFileDeleteFailed,

        /// <summary>Database entry removed; the physical file was already missing.</summary>
        SkippedMissingPhysicalFile
    }
}
