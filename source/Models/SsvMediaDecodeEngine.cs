namespace ScreenshotsVisualizer.Models
{
    /// <summary>
    /// Primary decode / media pipeline used for a catalog format.
    /// </summary>
    public enum SsvMediaDecodeEngine
    {
        /// <summary>
        /// Built-in Windows WIC / WPF decode (JPEG, PNG, GIF, BMP, …).
        /// </summary>
        WicNative = 0,

        /// <summary>
        /// Optional Windows Store WIC codec (for example HEIF when Magick host does not detect the format).
        /// </summary>
        WicExtension = 1,

        /// <summary>
        /// Playnite Magick.NET host decode (WebP, AVIF, JPEG XL signatures).
        /// </summary>
        MagickHost = 2,

        /// <summary>
        /// Plugin TGASharpLib decoder for TGA.
        /// </summary>
        TgaSharp = 3,

        /// <summary>
        /// ffmpeg / ffprobe for video thumbnails and metadata.
        /// </summary>
        Ffmpeg = 4
    }
}
