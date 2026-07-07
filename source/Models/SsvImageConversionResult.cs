namespace ScreenshotsVisualizer.Models
{
    /// <summary>
    /// Result of a single ImageMagick image conversion attempt.
    /// </summary>
    public class SsvImageConversionResult
    {
        /// <summary>
        /// Gets or sets whether the conversion completed successfully.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the source image path.
        /// </summary>
        public string InputPath { get; set; }

        /// <summary>
        /// Gets or sets the final converted image path when <see cref="Success"/> is true.
        /// </summary>
        public string OutputPath { get; set; }

        /// <summary>
        /// Gets or sets the ImageMagick process exit code.
        /// </summary>
        public int ExitCode { get; set; }

        /// <summary>
        /// Gets or sets whether the failure is due to a missing ImageMagick executable.
        /// </summary>
        public bool ImageMagickNotFound { get; set; }

        /// <summary>
        /// Gets or sets an error description when <see cref="Success"/> is false.
        /// </summary>
        public string ErrorMessage { get; set; }
    }
}
