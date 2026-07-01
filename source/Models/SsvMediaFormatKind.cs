namespace ScreenshotsVisualizer.Models
{
    /// <summary>
    /// Media category for a supported screenshot file extension.
    /// </summary>
    public enum SsvMediaFormatKind
    {
        /// <summary>
        /// Still image file decoded via WPF/WIC or a plugin-specific decoder.
        /// </summary>
        Image = 0,

        /// <summary>
        /// Video file handled via ffmpeg/ffprobe.
        /// </summary>
        Video = 1
    }
}
