using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ScreenshotsVisualizer.Models
{
    /// <summary>
    /// Persisted ImageMagick conversion profile used by game and main menu actions.
    /// </summary>
    public class SsvImageConversionCustomCmd
    {
        /// <summary>
        /// Gets or sets the stable identifier for list editing and menu binding.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets or sets the display name shown in settings and menu entries.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the target image format extension without a leading dot (for example jpg, png, webp).
        /// </summary>
        public string OutputFormat { get; set; } = "jpg";

        /// <summary>
        /// Gets or sets the ImageMagick quality value when the output format supports it.
        /// </summary>
        public int? Quality { get; set; } = 98;

        /// <summary>
        /// Gets or sets whether metadata is stripped via the ImageMagick <c>-strip</c> option.
        /// </summary>
        public bool StripMetadata { get; set; } = true;

        /// <summary>
        /// Gets or sets additional ImageMagick arguments inserted before the output path.
        /// </summary>
        public string ExtraArguments { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the source file is deleted after a successful conversion.
        /// Always <c>true</c> in v1; kept for future evolution.
        /// </summary>
        public bool DeleteOriginal { get; set; } = true;

        /// <summary>
        /// Creates a default JPEG profile using the supplied quality value.
        /// </summary>
        /// <param name="quality">JPEG quality between 1 and 100.</param>
        /// <returns>A new custom command profile.</returns>
        public static SsvImageConversionCustomCmd CreateDefaultJpgProfile(int quality)
        {
            return new SsvImageConversionCustomCmd
            {
                Name = string.Format("JPG (quality {0})", quality),
                OutputFormat = "jpg",
                Quality = quality,
                StripMetadata = true,
                DeleteOriginal = true
            };
        }

        /// <summary>
        /// Builds the process arguments passed to ImageMagick for the given input and output paths.
        /// </summary>
        /// <param name="inputPath">Absolute path to the source image.</param>
        /// <param name="outputPath">Absolute path to the converted image.</param>
        /// <returns>ImageMagick argument string suitable for <c>ProcessStarter</c>.</returns>
        public string BuildArguments(string inputPath, string outputPath)
        {
            IEnumerable<string> parts = BuildArgumentParts(inputPath, outputPath);
            return string.Join(" ", parts);
        }

        /// <summary>
        /// Builds the full executable command line including the ImageMagick binary path.
        /// </summary>
        /// <param name="imageMagickPath">Absolute path to <c>magick.exe</c> or <c>convert.exe</c>.</param>
        /// <param name="inputPath">Absolute path to the source image.</param>
        /// <param name="outputPath">Absolute path to the converted image.</param>
        /// <returns>Full command line executed for the conversion.</returns>
        public string BuildCommandLine(string imageMagickPath, string inputPath, string outputPath)
        {
            return QuoteExecutablePath(imageMagickPath) + " " + BuildArguments(inputPath, outputPath);
        }

        /// <summary>
        /// Builds a settings preview of the command line with <c>{input}</c> and <c>{output}</c> placeholders.
        /// </summary>
        /// <param name="imageMagickPath">Absolute path to <c>magick.exe</c> or <c>convert.exe</c>.</param>
        /// <returns>Human-readable command preview for the settings UI.</returns>
        public string GetCommandPreview(string imageMagickPath)
        {
            IEnumerable<string> parts = BuildArgumentParts("{input}", "{output}");
            return QuoteExecutablePath(imageMagickPath) + " " + string.Join(" ", parts);
        }

        /// <summary>
        /// Resolves the output file path for a source image using this profile output format.
        /// </summary>
        /// <param name="inputPath">Absolute path to the source image.</param>
        /// <returns>Output path with the profile extension applied.</returns>
        public string GetOutputPath(string inputPath)
        {
            if (string.IsNullOrWhiteSpace(inputPath))
            {
                return string.Empty;
            }

            string directory = Path.GetDirectoryName(inputPath) ?? string.Empty;
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(inputPath);
            string extension = NormalizeOutputFormatExtension();
            return Path.Combine(directory, fileNameWithoutExtension + extension);
        }

        /// <summary>
        /// Returns whether <paramref name="inputPath"/> already uses this profile output format
        /// (for example <c>.jpg</c> and <c>.jpeg</c> are treated as equivalent for JPEG output).
        /// </summary>
        /// <param name="inputPath">Absolute path to the source image.</param>
        /// <returns><c>true</c> when conversion can be skipped.</returns>
        public bool IsAlreadyOutputFormat(string inputPath)
        {
            if (string.IsNullOrWhiteSpace(inputPath))
            {
                return false;
            }

            string inputToken = NormalizeExtensionToken(Path.GetExtension(inputPath));
            if (string.IsNullOrEmpty(inputToken))
            {
                return false;
            }

            string outputToken = NormalizeExtensionToken(NormalizeOutputFormatExtension());
            if (string.Equals(inputToken, outputToken, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return IsJpegExtensionToken(inputToken) && IsJpegExtensionToken(outputToken);
        }

        private IEnumerable<string> BuildArgumentParts(string inputPath, string outputPath)
        {
            var parts = new List<string> { QuotePathToken(inputPath) };

            if (!string.IsNullOrWhiteSpace(ExtraArguments))
            {
                parts.Add(ExtraArguments.Trim());
            }

            if (StripMetadata)
            {
                parts.Add("-strip");
            }

            if (Quality.HasValue)
            {
                parts.Add("-quality");
                parts.Add(Quality.Value.ToString());
            }

            parts.Add(QuotePathToken(outputPath));
            return parts;
        }

        private string NormalizeOutputFormatExtension()
        {
            string format = (OutputFormat ?? string.Empty).Trim().TrimStart('.');
            if (string.IsNullOrWhiteSpace(format))
            {
                format = "jpg";
            }

            return "." + format;
        }

        private static string NormalizeExtensionToken(string extension)
        {
            return (extension ?? string.Empty).Trim().TrimStart('.');
        }

        private static bool IsJpegExtensionToken(string extensionToken)
        {
            return string.Equals(extensionToken, "jpg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extensionToken, "jpeg", StringComparison.OrdinalIgnoreCase);
        }

        private static string QuoteExecutablePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return QuotePathToken(path);
        }

        private static string QuotePathToken(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "\"\"";
            }

            if (path == "{input}" || path == "{output}")
            {
                return path;
            }

            if (path.Any(c => char.IsWhiteSpace(c)))
            {
                return "\"" + path + "\"";
            }

            return path;
        }
    }
}
