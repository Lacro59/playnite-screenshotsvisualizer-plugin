using CommonPluginsShared;
using CommonPluginsShared.Utilities;
using CommonPlayniteShared.Common;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;

namespace ScreenshotsVisualizer.Services
{
    /// <summary>
    /// Generates and caches screenshot thumbnails for images (bounded WPF) and videos (ffmpeg).
    /// </summary>
    public class SsvThumbnailService
    {
        /// <summary>
        /// Maximum length of the longest thumbnail edge in pixels.
        /// </summary>
        public const int ThumbnailMaxDimension = 320;

        /// <summary>
        /// Maximum source file size accepted for image thumbnail generation (30 MB).
        /// </summary>
        public const long MaxSourceFileBytes = 30L * 1024 * 1024;

        /// <summary>
        /// Cache subfolder name under the plugin cache root.
        /// </summary>
        public const string ThumbnailsFolderName = "Thumbnails";

        private const string FailedMarkerSuffix = ".failed";
        private const string TemporaryFileSuffix = ".tmp";
        private const string LogPrefix = "[SsvThumbnail]";

        private readonly string _pluginName;

        /// <summary>
        /// Initializes a new instance of the <see cref="SsvThumbnailService"/> class.
        /// </summary>
        /// <param name="pluginName">Plugin display name used for logging.</param>
        public SsvThumbnailService(string pluginName)
        {
            _pluginName = pluginName ?? string.Empty;
        }

        /// <summary>
        /// Builds the legacy image thumbnail cache path for a source file name.
        /// </summary>
        /// <param name="cacheRootPath">Plugin cache root directory.</param>
        /// <param name="sourceFileName">Source file name with extension.</param>
        /// <returns>Absolute path to the cached JPEG thumbnail.</returns>
        public static string GetImageThumbnailCachePath(string cacheRootPath, string sourceFileName)
        {
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(sourceFileName);
            string thumbnailsDirectory = Path.Combine(cacheRootPath, ThumbnailsFolderName);
            return Path.Combine(thumbnailsDirectory, fileNameWithoutExt + "_" + fileNameWithoutExt + "_Thumbnail.jpg");
        }

        /// <summary>
        /// Builds the video thumbnail cache path for a source file name.
        /// </summary>
        /// <param name="cacheRootPath">Plugin cache root directory.</param>
        /// <param name="sourceFileName">Source file name with extension.</param>
        /// <param name="fileSize">Source file size in bytes.</param>
        /// <param name="durationSeconds">Video duration in seconds.</param>
        /// <returns>Absolute path to the cached JPEG thumbnail.</returns>
        public static string GetVideoThumbnailCachePath(string cacheRootPath, string sourceFileName, long fileSize, double durationSeconds)
        {
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(sourceFileName);
            string thumbnailsDirectory = Path.Combine(cacheRootPath, ThumbnailsFolderName);
            return Path.Combine(thumbnailsDirectory, fileNameWithoutExt + "_" + fileSize + "_" + durationSeconds + "_Thumbnail.jpg");
        }

        /// <summary>
        /// Returns the failure marker path associated with a thumbnail cache file.
        /// </summary>
        /// <param name="thumbnailPath">Thumbnail cache file path.</param>
        /// <returns>Absolute path to the failure marker file.</returns>
        public static string GetFailureMarkerPath(string thumbnailPath)
        {
            return thumbnailPath + FailedMarkerSuffix;
        }

        /// <summary>
        /// Ensures an image thumbnail exists in the cache and returns its path when available.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source image.</param>
        /// <param name="cacheRootPath">Plugin cache root directory.</param>
        /// <param name="sourceModified">Last known modification time of the source file.</param>
        /// <returns>The cached thumbnail path, or <c>null</c> when generation is skipped or fails.</returns>
        public string TryEnsureImageThumbnail(string sourcePath, string cacheRootPath, DateTime sourceModified)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                return null;
            }

            string thumbnailPath = GetImageThumbnailCachePath(cacheRootPath, Path.GetFileName(sourcePath));
            string failureMarkerPath = GetFailureMarkerPath(thumbnailPath);
            ClearFailureMarkerIfSourceChanged(sourcePath, sourceModified, failureMarkerPath);

            if (File.Exists(failureMarkerPath))
            {
                return null;
            }

            if (IsCacheEntryValid(sourcePath, sourceModified, thumbnailPath))
            {
                return thumbnailPath;
            }

            FileInfo sourceInfo = new FileInfo(sourcePath);
            if (File.Exists(thumbnailPath))
            {
                LogThumbnailDebug(string.Format(
                    "Cache stale, regenerating image: '{0}'",
                    sourcePath));
            }

            if (sourceInfo.Length > MaxSourceFileBytes)
            {
                LogThumbnailDebug(string.Format(
                    "Skipped image (> {0} MB): '{1}' ({2} bytes)",
                    MaxSourceFileBytes / (1024 * 1024),
                    sourcePath,
                    sourceInfo.Length));
                LogSkippedSource(string.Format(
                    "Thumbnail generation skipped: source file exceeds {0} MB: '{1}'",
                    MaxSourceFileBytes / (1024 * 1024),
                    sourcePath));
                WriteFailureMarker(failureMarkerPath);
                return null;
            }

            ImageProperties imageProperties = Images.GetImageProperties(sourcePath);
            LogThumbnailDebug(string.Format(
                "Generating image thumbnail: '{0}' ({1}x{2}, {3} bytes)",
                sourcePath,
                imageProperties.Width,
                imageProperties.Height,
                sourceInfo.Length));

            try
            {
                DeleteCacheEntry(thumbnailPath, failureMarkerPath);
                BitmapSource decodedImage = DecodeBoundedImage(sourcePath, imageProperties);
                if (decodedImage == null)
                {
                    LogThumbnailDebug(string.Format("Decode failed: '{0}'", sourcePath));
                    WriteFailureMarker(failureMarkerPath);
                    return null;
                }

                SaveJpeg(decodedImage, thumbnailPath);
                if (File.Exists(thumbnailPath))
                {
                    LogThumbnailDebug(string.Format("Created: '{0}'", thumbnailPath));
                    return thumbnailPath;
                }

                LogThumbnailDebug(string.Format("JPEG write failed: '{0}'", thumbnailPath));
                WriteFailureMarker(failureMarkerPath);
                return null;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, true, false, _pluginName);
                WriteFailureMarker(failureMarkerPath);
                return null;
            }
        }

        /// <summary>
        /// Ensures a video thumbnail exists in the cache and returns its path when available.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source video.</param>
        /// <param name="cacheRootPath">Plugin cache root directory.</param>
        /// <param name="sourceModified">Last known modification time of the source file.</param>
        /// <param name="fileSize">Source file size in bytes.</param>
        /// <param name="durationSeconds">Video duration in seconds.</param>
        /// <param name="ffmpegPath">Absolute path to the ffmpeg executable.</param>
        /// <returns>The cached thumbnail path, or <c>null</c> when generation is skipped or fails.</returns>
        public string TryEnsureVideoThumbnail(
            string sourcePath,
            string cacheRootPath,
            DateTime sourceModified,
            long fileSize,
            double durationSeconds,
            string ffmpegPath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                return null;
            }

            string thumbnailPath = GetVideoThumbnailCachePath(cacheRootPath, Path.GetFileName(sourcePath), fileSize, durationSeconds);
            string failureMarkerPath = GetFailureMarkerPath(thumbnailPath);
            ClearFailureMarkerIfSourceChanged(sourcePath, sourceModified, failureMarkerPath);

            if (File.Exists(failureMarkerPath))
            {
                return null;
            }

            if (IsCacheEntryValid(sourcePath, sourceModified, thumbnailPath))
            {
                return thumbnailPath;
            }

            if (File.Exists(thumbnailPath))
            {
                LogThumbnailDebug(string.Format(
                    "Cache stale, regenerating video: '{0}'",
                    sourcePath));
            }

            if (string.IsNullOrWhiteSpace(ffmpegPath) || !File.Exists(ffmpegPath))
            {
                LogThumbnailDebug(string.Format("ffmpeg not found, skipping video: '{0}'", sourcePath));
                NotifyFfmpegNotFound();
                WriteFailureMarker(failureMarkerPath);
                return null;
            }

            LogThumbnailDebug(string.Format(
                "Generating video thumbnail: '{0}' (size={1}, duration={2}s)",
                sourcePath,
                fileSize,
                durationSeconds));

            try
            {
                DeleteCacheEntry(thumbnailPath, failureMarkerPath);

                if (!GenerateVideoThumbnailWithFfmpeg(sourcePath, thumbnailPath, ffmpegPath))
                {
                    LogThumbnailDebug(string.Format("ffmpeg produced no file: '{0}'", thumbnailPath));
                    WriteFailureMarker(failureMarkerPath);
                    return null;
                }

                if (File.Exists(thumbnailPath))
                {
                    LogThumbnailDebug(string.Format("Created video thumbnail: '{0}'", thumbnailPath));
                    return thumbnailPath;
                }

                LogThumbnailDebug(string.Format("Video thumbnail missing after ffmpeg: '{0}'", thumbnailPath));
                WriteFailureMarker(failureMarkerPath);
                return null;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, true, false, _pluginName);
                WriteFailureMarker(failureMarkerPath);
                return null;
            }
        }

        /// <summary>
        /// Determines whether a cached thumbnail is still valid for the given source file.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <param name="sourceModified">Last known modification time of the source file.</param>
        /// <param name="thumbnailPath">Absolute path to the cached thumbnail.</param>
        /// <returns><c>true</c> when the cache entry can be reused.</returns>
        public static bool IsCacheEntryValid(string sourcePath, DateTime sourceModified, string thumbnailPath)
        {
            if (!File.Exists(thumbnailPath))
            {
                return false;
            }

            DateTime sourceTimestamp = GetSourceTimestamp(sourcePath, sourceModified);
            DateTime thumbnailTimestamp = File.GetLastWriteTimeUtc(thumbnailPath);
            return thumbnailTimestamp >= sourceTimestamp;
        }

        private static DateTime GetSourceTimestamp(string sourcePath, DateTime sourceModified)
        {
            if (sourceModified != default(DateTime))
            {
                return sourceModified.ToUniversalTime();
            }

            if (File.Exists(sourcePath))
            {
                return File.GetLastWriteTimeUtc(sourcePath);
            }

            return DateTime.MinValue;
        }

        private static void ClearFailureMarkerIfSourceChanged(string sourcePath, DateTime sourceModified, string failureMarkerPath)
        {
            if (!File.Exists(failureMarkerPath))
            {
                return;
            }

            DateTime sourceTimestamp = GetSourceTimestamp(sourcePath, sourceModified);
            DateTime failureTimestamp = File.GetLastWriteTimeUtc(failureMarkerPath);
            if (sourceTimestamp > failureTimestamp)
            {
                FileSystem.DeleteFileSafe(failureMarkerPath);
                LogThumbnailDebug(string.Format(
                    "Cleared failure marker (source changed): '{0}'",
                    sourcePath));
            }
        }

        private static void DeleteCacheEntry(string thumbnailPath, string failureMarkerPath)
        {
            FileSystem.DeleteFileSafe(thumbnailPath);
            FileSystem.DeleteFileSafe(failureMarkerPath);
        }

        private static void WriteFailureMarker(string failureMarkerPath)
        {
            try
            {
                string directory = Path.GetDirectoryName(failureMarkerPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    FileSystem.CreateDirectory(directory);
                }

                File.WriteAllText(failureMarkerPath, string.Empty);
            }
            catch (Exception ex)
            {
                LogManager.GetLogger().Error(ex, "Failed to write thumbnail failure marker.");
            }
        }

        private static BitmapSource DecodeBoundedImage(string sourcePath, ImageProperties properties)
        {
            if (properties == null || properties.Width <= 0 || properties.Height <= 0)
            {
                return null;
            }

            BitmapLoadProperties loadProperties = BuildLoadProperties(properties.Width, properties.Height);
            BitmapImage bitmap = BitmapExtensions.BitmapFromFile(sourcePath, loadProperties);
            return bitmap;
        }

        private static BitmapLoadProperties BuildLoadProperties(int width, int height)
        {
            if (width >= height)
            {
                return new BitmapLoadProperties(ThumbnailMaxDimension, 0);
            }

            return new BitmapLoadProperties(0, ThumbnailMaxDimension);
        }

        private static void SaveJpeg(BitmapSource bitmap, string destinationPath)
        {
            string directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(directory))
            {
                FileSystem.CreateDirectory(directory);
            }

            string temporaryPath = destinationPath + TemporaryFileSuffix;
            try
            {
                using (FileStream stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    encoder.Save(stream);
                }

                if (File.Exists(destinationPath))
                {
                    FileSystem.DeleteFileSafe(destinationPath);
                }

                File.Move(temporaryPath, destinationPath);
            }
            catch
            {
                FileSystem.DeleteFileSafe(temporaryPath);
                throw;
            }
        }

        private void LogSkippedSource(string message)
        {
            Common.LogError(null, true, message, false, _pluginName);
        }

        private static void LogThumbnailDebug(string message)
        {
            Common.LogDebug(true, string.Format("{0} {1}", LogPrefix, message));
        }

        private bool GenerateVideoThumbnailWithFfmpeg(string sourcePath, string thumbnailPath, string ffmpegPath)
        {
            string directory = Path.GetDirectoryName(thumbnailPath);
            if (!string.IsNullOrEmpty(directory))
            {
                FileSystem.CreateDirectory(directory);
            }

            string thumbArgs = "-i \"{0}\" -frames 1 -vf \"select=not(mod(n\\,1000)),scale=320:320:force_original_aspect_ratio=decrease\" \"{1}\"";
            string workingDirectory = Path.GetDirectoryName(ffmpegPath);
            _ = ProcessStarter.StartProcessWait(
                ffmpegPath,
                string.Format(thumbArgs, sourcePath, thumbnailPath),
                workingDirectory,
                true,
                out string stdOut,
                out string stdErr);

            return File.Exists(thumbnailPath);
        }

        private void NotifyFfmpegNotFound()
        {
            LogManager.GetLogger().Warn("No ffmpeg executable");
            API.Instance.Notifications.Add(new NotificationMessage(
                string.Format("{0}-FfmpegPath-Error", _pluginName),
                string.Format("{0}\r\n{1}", _pluginName, ResourceProvider.GetString("LOCSsvFfmpegNotFound")),
                NotificationType.Error));
        }
    }
}
