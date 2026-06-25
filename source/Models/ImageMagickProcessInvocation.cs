namespace ScreenshotsVisualizer.Models
{
    /// <summary>
    /// Describes how to start ImageMagick for a single conversion.
    /// </summary>
    public class ImageMagickProcessInvocation
    {
        /// <summary>
        /// Gets or sets the absolute path to <c>magick.exe</c> or <c>convert.exe</c>.
        /// </summary>
        public string ExecutablePath { get; set; }

        /// <summary>
        /// Gets or sets the process arguments passed to ImageMagick.
        /// </summary>
        public string Arguments { get; set; }

        /// <summary>
        /// Gets or sets the source image path.
        /// </summary>
        public string InputPath { get; set; }

        /// <summary>
        /// Gets or sets the target output path written by ImageMagick.
        /// </summary>
        public string ProcessOutputPath { get; set; }

        /// <summary>
        /// Gets or sets the final output path after any temporary file replacement.
        /// </summary>
        public string FinalOutputPath { get; set; }

        /// <summary>
        /// Gets or sets whether ImageMagick writes to a temporary file before replacing the source.
        /// </summary>
        public bool UsesTemporaryOutput { get; set; }

        /// <summary>
        /// Gets or sets the temporary output path when <see cref="UsesTemporaryOutput"/> is true.
        /// </summary>
        public string TemporaryOutputPath { get; set; }

        /// <summary>
        /// Gets or sets the full command line preview for diagnostics.
        /// </summary>
        public string CommandLinePreview { get; set; }
    }
}
