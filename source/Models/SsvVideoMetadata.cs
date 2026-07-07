using System;

namespace ScreenshotsVisualizer.Models
{
    /// <summary>
    /// Video dimensions and duration obtained from a single ffprobe read.
    /// </summary>
    public sealed class SsvVideoMetadata
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SsvVideoMetadata"/> class.
        /// </summary>
        /// <param name="duration">Video duration.</param>
        /// <param name="width">Frame width in pixels.</param>
        /// <param name="height">Frame height in pixels.</param>
        public SsvVideoMetadata(TimeSpan duration, int width, int height)
        {
            Duration = duration;
            Width = width;
            Height = height;
        }

        /// <summary>
        /// Gets the video duration.
        /// </summary>
        public TimeSpan Duration { get; }

        /// <summary>
        /// Gets the frame width in pixels.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the frame height in pixels.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Gets the display resolution string in the form <c>width x height</c>.
        /// </summary>
        public string SizeString
        {
            get
            {
                if (Width > 0 && Height > 0)
                {
                    return string.Format("{0}x{1}", Width, Height);
                }

                return string.Empty;
            }
        }
    }
}
