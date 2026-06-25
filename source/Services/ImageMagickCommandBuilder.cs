using ScreenshotsVisualizer.Models;
using System;
using System.IO;

namespace ScreenshotsVisualizer.Services
{
    /// <summary>
    /// Builds ImageMagick process invocations from persisted conversion profiles.
    /// </summary>
    public static class ImageMagickCommandBuilder
    {
        /// <summary>
        /// Temporary file suffix used when input and output paths are identical.
        /// </summary>
        public const string TemporaryOutputSuffix = ".ssv-im.tmp";

        /// <summary>
        /// Builds a settings preview for the supplied conversion profile.
        /// </summary>
        /// <param name="imageMagickPath">Absolute path to the ImageMagick executable.</param>
        /// <param name="command">Conversion profile.</param>
        /// <returns>Human-readable command preview with <c>{input}</c> and <c>{output}</c> placeholders.</returns>
        public static string GetCommandPreview(string imageMagickPath, SsvImageConversionCustomCmd command)
        {
            if (command == null)
            {
                return string.Empty;
            }

            return command.GetCommandPreview(imageMagickPath);
        }

        /// <summary>
        /// Builds the process invocation required to convert a single image file.
        /// </summary>
        /// <param name="imageMagickPath">Absolute path to the ImageMagick executable.</param>
        /// <param name="command">Conversion profile.</param>
        /// <param name="inputPath">Absolute path to the source image.</param>
        /// <returns>Process invocation details.</returns>
        public static ImageMagickProcessInvocation BuildProcessInvocation(
            string imageMagickPath,
            SsvImageConversionCustomCmd command,
            string inputPath)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (string.IsNullOrWhiteSpace(inputPath))
            {
                throw new ArgumentException("Input path is required.", nameof(inputPath));
            }

            string finalOutputPath = command.GetOutputPath(inputPath);
            string processOutputPath = finalOutputPath;
            string temporaryOutputPath = null;
            bool usesTemporaryOutput = IsSamePath(inputPath, finalOutputPath);

            if (usesTemporaryOutput)
            {
                temporaryOutputPath = inputPath + TemporaryOutputSuffix;
                processOutputPath = temporaryOutputPath;
            }

            return new ImageMagickProcessInvocation
            {
                ExecutablePath = imageMagickPath,
                Arguments = command.BuildArguments(inputPath, processOutputPath),
                InputPath = inputPath,
                ProcessOutputPath = processOutputPath,
                FinalOutputPath = finalOutputPath,
                UsesTemporaryOutput = usesTemporaryOutput,
                TemporaryOutputPath = temporaryOutputPath,
                CommandLinePreview = command.BuildCommandLine(imageMagickPath, inputPath, processOutputPath)
            };
        }

        private static bool IsSamePath(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
