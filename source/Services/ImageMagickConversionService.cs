using CommonPluginsShared;
using CommonPluginsShared.IO;
using CommonPlayniteShared.Common;
using Playnite.SDK;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
using System;
using System.IO;

namespace ScreenshotsVisualizer.Services
{
    /// <summary>
    /// Executes ImageMagick conversions for screenshot files.
    /// </summary>
    public class ImageMagickConversionService
    {
        private readonly string _pluginName;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageMagickConversionService"/> class.
        /// </summary>
        /// <param name="pluginName">Plugin display name used for logging and notifications.</param>
        public ImageMagickConversionService(string pluginName)
        {
            _pluginName = pluginName ?? string.Empty;
        }

        /// <summary>
        /// Converts a single image file using ImageMagick and the supplied profile.
        /// Preserves the original last write time and deletes the source file on success when configured.
        /// </summary>
        /// <param name="imageMagickPath">Absolute path to the ImageMagick executable.</param>
        /// <param name="command">Conversion profile.</param>
        /// <param name="inputPath">Absolute path to the source image.</param>
        /// <returns>Conversion result.</returns>
        public SsvImageConversionResult TryConvert(string imageMagickPath, SsvImageConversionCustomCmd command, string inputPath)
        {
            if (command == null)
            {
                return Failed(inputPath, null, "Conversion profile is not set.");
            }

            if (string.IsNullOrWhiteSpace(inputPath))
            {
                return Failed(null, null, "Input path is empty.");
            }

            if (!File.Exists(inputPath))
            {
                return Failed(inputPath, null, string.Format("Input file was not found: '{0}'.", inputPath));
            }

            if (string.IsNullOrWhiteSpace(imageMagickPath) || !File.Exists(imageMagickPath))
            {
                Common.LogDebug(true, string.Format(
                    "[SsvImageMagick] Executable not found (path: '{0}')",
                    imageMagickPath ?? string.Empty));
                NotifyImageMagickNotFound();
                return new SsvImageConversionResult
                {
                    Success = false,
                    InputPath = inputPath,
                    ImageMagickNotFound = true,
                    ErrorMessage = ResourceProvider.GetString("LOCSsvImageMagickNotFound")
                };
            }

            ImageMagickProcessInvocation invocation = null;
            try
            {
                invocation = ImageMagickCommandBuilder.BuildProcessInvocation(imageMagickPath, command, inputPath);
                DateTime originalWriteTime = File.GetLastWriteTime(inputPath);
                string workingDirectory = Path.GetDirectoryName(imageMagickPath);

                Common.LogDebug(true, string.Format("[SsvImageMagick] Executing: {0}", invocation.CommandLinePreview));

                int exitCode = ProcessStarter.StartProcessWait(
                    invocation.ExecutablePath,
                    invocation.Arguments,
                    workingDirectory,
                    true);

                if (exitCode != 0)
                {
                    CleanupTemporaryOutput(invocation);
                    return Failed(
                        inputPath,
                        invocation.ProcessOutputPath,
                        string.Format("ImageMagick exited with code {0}. Command: {1}", exitCode, invocation.CommandLinePreview),
                        exitCode);
                }

                if (!File.Exists(invocation.ProcessOutputPath))
                {
                    CleanupTemporaryOutput(invocation);
                    return Failed(
                        inputPath,
                        invocation.ProcessOutputPath,
                        string.Format("ImageMagick did not create the output file: '{0}'.", invocation.ProcessOutputPath),
                        exitCode);
                }

                string finalOutputPath = CompleteSuccessfulConversion(command, invocation, originalWriteTime);
                Common.LogDebug(true, string.Format(
                    "[SsvImageMagick] Success: '{0}' -> '{1}' (exit code {2})",
                    inputPath,
                    finalOutputPath,
                    exitCode));
                return new SsvImageConversionResult
                {
                    Success = true,
                    InputPath = inputPath,
                    OutputPath = finalOutputPath,
                    ExitCode = exitCode
                };
            }
            catch (Exception ex)
            {
                CleanupTemporaryOutput(invocation);
                Common.LogError(ex, false, true, _pluginName);
                return Failed(inputPath, invocation?.ProcessOutputPath, ex.Message);
            }
        }

        private string CompleteSuccessfulConversion(
            SsvImageConversionCustomCmd command,
            ImageMagickProcessInvocation invocation,
            DateTime originalWriteTime)
        {
            if (invocation.UsesTemporaryOutput)
            {
                File.SetLastWriteTime(invocation.TemporaryOutputPath, originalWriteTime);

                if (command.DeleteOriginal)
                {
                    FileSystem.DeleteFileSafe(invocation.InputPath);
                }

                if (File.Exists(invocation.FinalOutputPath))
                {
                    FileSystem.DeleteFileSafe(invocation.FinalOutputPath);
                }

                File.Move(invocation.TemporaryOutputPath, invocation.FinalOutputPath);
                return invocation.FinalOutputPath;
            }

            File.SetLastWriteTime(invocation.ProcessOutputPath, originalWriteTime);

            if (command.DeleteOriginal && !IsSamePath(invocation.InputPath, invocation.ProcessOutputPath))
            {
                FileSystem.DeleteFileSafe(invocation.InputPath);
            }

            return invocation.FinalOutputPath;
        }

        private static void CleanupTemporaryOutput(ImageMagickProcessInvocation invocation)
        {
            if (invocation == null || !invocation.UsesTemporaryOutput)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(invocation.TemporaryOutputPath))
            {
                FileSystem.DeleteFileSafe(invocation.TemporaryOutputPath);
            }
        }

        private void NotifyImageMagickNotFound()
        {
            API.Instance.Notifications.Add(new NotificationMessage(
                string.Format("{0}-ImageMagickPath-Error", _pluginName),
                string.Format("{0}\r\n{1}", _pluginName, ResourceProvider.GetString("LOCSsvImageMagickNotFound")),
                NotificationType.Error));
        }

        private static SsvImageConversionResult Failed(string inputPath, string outputPath, string errorMessage, int exitCode = -1)
        {
            return new SsvImageConversionResult
            {
                Success = false,
                InputPath = inputPath,
                OutputPath = outputPath,
                ExitCode = exitCode,
                ErrorMessage = errorMessage
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
