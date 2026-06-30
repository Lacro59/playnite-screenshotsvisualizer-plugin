using CommonPluginsShared;
using CommonPluginsShared.Utilities;
using CommonPlayniteShared.Common;
using Playnite.SDK;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
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
        private bool _ffmpegNotFoundNotified;
        private bool _ffprobeNotFoundNotified;

        /// <summary>
        /// Initializes a new instance of the <see cref="SsvThumbnailService"/> class.
        /// </summary>
        /// <param name="pluginName">Plugin display name used for logging.</param>
        public SsvThumbnailService(string pluginName)
        {
            _pluginName = pluginName ?? string.Empty;
        }

        /// <summary>
        /// Returns whether ffprobe is configured and present; logs and notifies once per service instance when missing.
        /// </summary>
        /// <param name="ffprobePath">Absolute path to the ffprobe executable.</param>
        /// <returns><c>true</c> when ffprobe can be invoked.</returns>
        public bool EnsureFfprobeAvailable(string ffprobePath)
        {
            if (!string.IsNullOrWhiteSpace(ffprobePath) && File.Exists(ffprobePath))
            {
                return true;
            }

            NotifyFfprobeNotFoundOnce();
            return false;
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
        /// Returns whether <paramref name="path"/> points to a plugin-generated JPEG thumbnail in the cache folder.
        /// </summary>
        /// <param name="path">Absolute or relative file path to evaluate.</param>
        /// <returns><c>true</c> when the path is under <see cref="ThumbnailsFolderName"/> and ends with <c>_Thumbnail.jpg</c>.</returns>
        public static bool IsPluginThumbnailCachePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string fileName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(fileName)
                || !fileName.EndsWith("_Thumbnail.jpg", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string parentDirectoryName = Path.GetFileName(Path.GetDirectoryName(path));
            return string.Equals(parentDirectoryName, ThumbnailsFolderName, StringComparison.OrdinalIgnoreCase);
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
        /// Removes cached thumbnail files associated with a screenshot source path.
        /// </summary>
        /// <param name="screenshot">Screenshot whose cache entries should be purged.</param>
        /// <param name="cacheRootPath">Plugin cache root directory.</param>
        public void TryPurgeCachedThumbnailsForScreenshot(Screenshot screenshot, string cacheRootPath)
        {
            if (screenshot == null || string.IsNullOrWhiteSpace(cacheRootPath) || string.IsNullOrEmpty(screenshot.FileName))
            {
                LogThumbnailDebug("Purge skipped — screenshot or cache root path is missing");
                return;
            }

            string sourceFileName = Path.GetFileName(screenshot.FileName);
            LogThumbnailDebug(string.Format(
                "Purging thumbnail cache for '{0}'",
                sourceFileName));

            TryDeleteCacheEntry(GetImageThumbnailCachePath(cacheRootPath, sourceFileName));

            if (screenshot.IsVideo)
            {
                TryDeleteCacheEntry(GetVideoThumbnailCachePath(
                    cacheRootPath,
                    sourceFileName,
                    screenshot.FileSize,
                    screenshot.Duration.TotalSeconds));
            }
        }

        private void TryDeleteCacheEntry(string thumbnailPath)
        {
            if (string.IsNullOrEmpty(thumbnailPath))
            {
                return;
            }

            if (File.Exists(thumbnailPath))
            {
                FileSystem.DeleteFileSafe(thumbnailPath);
                LogThumbnailDebug(string.Format("Deleted thumbnail cache file '{0}'", thumbnailPath));
            }

            string failureMarkerPath = GetFailureMarkerPath(thumbnailPath);
            if (File.Exists(failureMarkerPath))
            {
                FileSystem.DeleteFileSafe(failureMarkerPath);
                LogThumbnailDebug(string.Format("Deleted thumbnail failure marker '{0}'", failureMarkerPath));
            }
        }

        /// <summary>
        /// Counts cached thumbnail files and their total size on disk.
        /// </summary>
        /// <param name="cacheRootPath">Plugin cache root directory.</param>
        /// <returns>File count and total byte size for thumbnail cache entries.</returns>
        public static ThumbnailCacheStats GetThumbnailCacheStats(string cacheRootPath)
        {
            string thumbnailsDirectory = Path.Combine(cacheRootPath ?? string.Empty, ThumbnailsFolderName);
            if (!Directory.Exists(thumbnailsDirectory))
            {
                return new ThumbnailCacheStats(0, 0);
            }

            int fileCount = 0;
            long totalBytes = 0;

            foreach (string filePath in Directory.GetFiles(thumbnailsDirectory, "*", SearchOption.TopDirectoryOnly))
            {
                if (filePath.EndsWith(FailedMarkerSuffix, StringComparison.OrdinalIgnoreCase)
                    || filePath.EndsWith(TemporaryFileSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                FileInfo fileInfo = new FileInfo(filePath);
                fileCount++;
                totalBytes += fileInfo.Length;
            }

            return new ThumbnailCacheStats(fileCount, totalBytes);
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
                NotifyFfmpegNotFoundOnce();
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

        private static void LogThumbnailWarn(string message)
        {
            LogManager.GetLogger().Warn(string.Format("{0} {1}", LogPrefix, message));
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

        private void NotifyFfprobeNotFoundOnce()
        {
            if (_ffprobeNotFoundNotified)
            {
                return;
            }

            _ffprobeNotFoundNotified = true;
            LogThumbnailWarn("ffprobe not found");
            API.Instance.Notifications.Add(new NotificationMessage(
                string.Format("{0}-FfprobePath-Error", _pluginName),
                string.Format("{0}\r\n{1}", _pluginName, ResourceProvider.GetString("LOCSsFfprobeNotFound")),
                NotificationType.Error));
        }

        private void NotifyFfmpegNotFoundOnce()
        {
            if (_ffmpegNotFoundNotified)
            {
                return;
            }

            _ffmpegNotFoundNotified = true;
            LogThumbnailWarn("ffmpeg not found");
            API.Instance.Notifications.Add(new NotificationMessage(
                string.Format("{0}-FfmpegPath-Error", _pluginName),
                string.Format("{0}\r\n{1}", _pluginName, ResourceProvider.GetString("LOCSsvFfmpegNotFound")),
                NotificationType.Error));
        }
    }

    /// <summary>
    /// Thumbnail cache folder statistics for settings display.
    /// </summary>
    public readonly struct ThumbnailCacheStats
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ThumbnailCacheStats"/> struct.
        /// </summary>
        /// <param name="fileCount">Number of cached thumbnail files.</param>
        /// <param name="totalBytes">Combined size of cached thumbnail files in bytes.</param>
        public ThumbnailCacheStats(int fileCount, long totalBytes)
        {
            FileCount = fileCount;
            TotalBytes = totalBytes;
        }

        /// <summary>
        /// Gets the number of cached thumbnail files.
        /// </summary>
        public int FileCount { get; }

        /// <summary>
        /// Gets the combined size of cached thumbnail files in bytes.
        /// </summary>
        public long TotalBytes { get; }
    }
}
