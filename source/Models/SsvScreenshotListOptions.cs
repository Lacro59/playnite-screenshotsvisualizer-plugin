namespace ScreenshotsVisualizer.Models
{
    /// <summary>
    /// Sort criterion for screenshot lists in Manager and game view.
    /// </summary>
    public enum SsvScreenshotSortBy
    {
        /// <summary>
        /// Sort by file modification date (<see cref="Screenshot.Modifed"/>).
        /// </summary>
        Date = 0,

        /// <summary>
        /// Sort by file name (<see cref="Screenshot.FileNameOnly"/>).
        /// </summary>
        Name = 1,

        /// <summary>
        /// Sort by file size in bytes (<see cref="Screenshot.FileSize"/>).
        /// </summary>
        Size = 2,

        /// <summary>
        /// Sort by media type (<see cref="Screenshot.IsVideo"/>; images before videos when ascending).
        /// </summary>
        Type = 3
    }

    /// <summary>
    /// Sort direction for screenshot lists.
    /// </summary>
    public enum SsvScreenshotSortDirection
    {
        /// <summary>
        /// Ascending order.
        /// </summary>
        Ascending = 0,

        /// <summary>
        /// Descending order (default for date).
        /// </summary>
        Descending = 1
    }

    /// <summary>
    /// Media-type filter applied before sorting screenshot lists.
    /// </summary>
    public enum SsvScreenshotMediaFilter
    {
        /// <summary>
        /// Show images and videos.
        /// </summary>
        All = 0,

        /// <summary>
        /// Show images only.
        /// </summary>
        Images = 1,

        /// <summary>
        /// Show videos only.
        /// </summary>
        Videos = 2
    }
}
