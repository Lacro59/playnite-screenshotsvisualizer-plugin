using CommonPlayniteShared.Common;
using CommonPluginsShared;
using Playnite.SDK;
using ScreenshotsVisualizer.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ScreenshotsVisualizer.Services
{
    /// <summary>
    /// Caches video duration and resolution per source file, using one combined ffprobe invocation per cache miss.
    /// </summary>
    public class SsvVideoMetadataService
    {
        private const string LogPrefix = "[SsvVideoMetadata]";
        private const int MaxLoggedStdoutChars = 256;
        private const string FfprobeArgumentsFormat =
            "-v error -select_streams v:0 -show_entries stream=width,height -show_entries format=duration -sexagesimal -of default=noprint_wrappers=1:nokey=1 \"{0}\"";

        private readonly string _pluginName;
        private readonly SsvThumbnailService _thumbnailService;
        private readonly object _cacheLock = new object();
        private readonly Dictionary<string, VideoMetadataCacheEntry> _cache =
            new Dictionary<string, VideoMetadataCacheEntry>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of the <see cref="SsvVideoMetadataService"/> class.
        /// </summary>
        /// <param name="pluginName">Plugin display name used for error logging.</param>
        /// <param name="thumbnailService">Thumbnail service used for ffprobe availability checks.</param>
        public SsvVideoMetadataService(string pluginName, SsvThumbnailService thumbnailService)
        {
            _pluginName = pluginName ?? string.Empty;
            _thumbnailService = thumbnailService;
        }

        /// <summary>
        /// Returns cached video metadata for the given source file, invoking ffprobe at most once per path and modification stamp.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source video file.</param>
        /// <param name="sourceModified">Last known modification time of the source file.</param>
        /// <param name="ffprobePath">Absolute path to the ffprobe executable.</param>
        /// <param name="metadata">Resolved metadata when the method returns <c>true</c>.</param>
        /// <returns><c>true</c> when duration and resolution were resolved.</returns>
        public bool TryGetVideoMetadata(
            string sourcePath,
            DateTime sourceModified,
            string ffprobePath,
            out SsvVideoMetadata metadata)
        {
            metadata = null;

            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                LogMetadataDebug("Skip metadata lookup: empty source path.");
                return false;
            }

            if (!File.Exists(sourcePath))
            {
                LogMetadataDebug(string.Format("Skip metadata lookup: file not found '{0}'.", sourcePath));
                return false;
            }

            string cacheKey = Path.GetFullPath(sourcePath);
            DateTime sourceTimestamp = ResolveSourceTimestamp(sourcePath, sourceModified);

            lock (_cacheLock)
            {
                if (_cache.TryGetValue(cacheKey, out VideoMetadataCacheEntry cachedEntry))
                {
                    if (cachedEntry.SourceTimestampUtc == sourceTimestamp)
                    {
                        metadata = cachedEntry.Metadata;
                        if (metadata != null)
                        {
                            LogMetadataDebug(string.Format(
                                "Cache hit for '{0}' ({1}, {2}, modified={3:u})",
                                sourcePath,
                                metadata.SizeString,
                                metadata.Duration,
                                sourceTimestamp));
                            return true;
                        }
                    }
                    else
                    {
                        LogMetadataDebug(string.Format(
                            "Cache stale for '{0}' (cached={1:u}, current={2:u})",
                            sourcePath,
                            cachedEntry.SourceTimestampUtc,
                            sourceTimestamp));
                    }
                }
            }

            if (!_thumbnailService.EnsureFfprobeAvailable(ffprobePath))
            {
                LogMetadataDebug(string.Format(
                    "ffprobe unavailable for '{0}' (path='{1}')",
                    sourcePath,
                    ffprobePath ?? string.Empty));
                return false;
            }

            try
            {
                string arguments = string.Format(CultureInfo.InvariantCulture, FfprobeArgumentsFormat, sourcePath);
                string workingDirectory = Path.GetDirectoryName(ffprobePath);
                LogMetadataDebug(string.Format(
                    "ffprobe start for '{0}' (modified={1:u})",
                    sourcePath,
                    sourceTimestamp));

                _ = ProcessStarter.StartProcessWait(
                    ffprobePath,
                    arguments,
                    workingDirectory,
                    true,
                    out string stdOut,
                    out string stdErr);

                LogMetadataDebug(string.Format(
                    "ffprobe stdout for '{0}': [{1}]",
                    sourcePath,
                    FormatLoggedProcessOutput(stdOut)));

                if (!string.IsNullOrWhiteSpace(stdErr))
                {
                    LogMetadataDebug(string.Format(
                        "ffprobe stderr for '{0}': [{1}]",
                        sourcePath,
                        FormatLoggedProcessOutput(stdErr)));
                }

                if (!TryParseFfprobeOutput(stdOut, out TimeSpan duration, out int width, out int height, out string parseDetail))
                {
                    LogMetadataDebug(string.Format(
                        "ffprobe parse failed for '{0}' ({1}, stdout length={2})",
                        sourcePath,
                        parseDetail,
                        stdOut?.Length ?? 0));
                    return false;
                }

                metadata = new SsvVideoMetadata(duration, width, height);

                lock (_cacheLock)
                {
                    _cache[cacheKey] = new VideoMetadataCacheEntry(sourceTimestamp, metadata);
                }

                LogMetadataDebug(string.Format(
                    "Cache store for '{0}' ({1}, {2}, modified={3:u})",
                    sourcePath,
                    metadata.SizeString,
                    metadata.Duration,
                    sourceTimestamp));
                return true;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, _pluginName);
                return false;
            }
        }

        private static DateTime ResolveSourceTimestamp(string sourcePath, DateTime sourceModified)
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

        private static bool TryParseFfprobeOutput(
            string stdOut,
            out TimeSpan duration,
            out int width,
            out int height,
            out string parseDetail)
        {
            duration = TimeSpan.Zero;
            width = 0;
            height = 0;
            parseDetail = "empty stdout";

            if (string.IsNullOrWhiteSpace(stdOut))
            {
                return false;
            }

            List<int> dimensions = new List<int>(2);
            string[] lines = stdOut.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed))
                {
                    continue;
                }

                // Width/height are plain integers; duration is sexagesimal and contains ':'.
                // Do not use TimeSpan.TryParse on bare integers (e.g. "1280" -> 1280 days).
                if (trimmed.IndexOf(':') >= 0)
                {
                    if (TimeSpan.TryParse(trimmed, CultureInfo.InvariantCulture, out TimeSpan parsedDuration)
                        && parsedDuration > TimeSpan.Zero)
                    {
                        duration = parsedDuration;
                    }

                    continue;
                }

                if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedDimension)
                    && parsedDimension > 0)
                {
                    dimensions.Add(parsedDimension);
                }
            }

            if (dimensions.Count >= 2)
            {
                width = dimensions[0];
                height = dimensions[1];
            }

            if (duration <= TimeSpan.Zero)
            {
                parseDetail = "duration missing or zero";
                return false;
            }

            if (width <= 0 || height <= 0)
            {
                parseDetail = string.Format(
                    "resolution missing (parsed {0} dimension line(s))",
                    dimensions.Count);
                return false;
            }

            parseDetail = string.Empty;
            return true;
        }

        private static string FormatLoggedProcessOutput(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            string normalized = value.Replace("\r", "\\r").Replace("\n", "\\n");
            if (normalized.Length <= MaxLoggedStdoutChars)
            {
                return normalized;
            }

            return normalized.Substring(0, MaxLoggedStdoutChars) + "...";
        }

        private static void LogMetadataDebug(string message)
        {
            Common.LogDebug(true, string.Format("{0} {1}", LogPrefix, message));
        }

        private sealed class VideoMetadataCacheEntry
        {
            public VideoMetadataCacheEntry(DateTime sourceTimestampUtc, SsvVideoMetadata metadata)
            {
                SourceTimestampUtc = sourceTimestampUtc;
                Metadata = metadata;
            }

            public DateTime SourceTimestampUtc { get; }

            public SsvVideoMetadata Metadata { get; }
        }
    }
}
