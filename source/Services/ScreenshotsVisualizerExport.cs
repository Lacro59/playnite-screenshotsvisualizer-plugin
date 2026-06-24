using CommonPluginsShared;
using CommonPluginsShared.Plugins;
using Playnite.SDK;
using ScreenshotsVisualizer.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ScreenshotsVisualizer.Services
{
    /// <summary>
    /// Exports ScreenshotsVisualizer data to CSV with one row per game.
    /// </summary>
    public class ScreenshotsVisualizerExport : PluginExportCsv<GameScreenshots>
    {
        private const string FieldGameName = "GameName";
        private const string FieldGameId = "GameId";
        private const string FieldSource = "Source";
        private const string FieldImageCount = "ImageCount";
        private const string FieldImageTotalSizeBytes = "ImageTotalSizeBytes";
        private const string FieldImageTotalSize = "ImageTotalSize";
        private const string FieldVideoCount = "VideoCount";
        private const string FieldVideoTotalSizeBytes = "VideoTotalSizeBytes";
        private const string FieldVideoTotalSize = "VideoTotalSize";
        private const string FieldVideoTotalDuration = "VideoTotalDuration";
        private const string FieldTotalCount = "TotalCount";
        private const string FieldTotalSizeBytes = "TotalSizeBytes";
        private const string FieldTotalSize = "TotalSize";
        private const string FieldDateLastRefresh = "DateLastRefresh";

        /// <inheritdoc />
        protected override Dictionary<string, string> GetHeader()
        {
            return new Dictionary<string, string>
            {
                { FieldGameName, ResourceProvider.GetString("LOCGameNameTitle") },
                { FieldGameId, ResourceProvider.GetString("LOCGameId") },
                { FieldSource, ResourceProvider.GetString("LOCCommonGameSource") },
                { FieldImageCount, ResourceProvider.GetString("LOCSsvCsvImageCount") },
                { FieldImageTotalSizeBytes, ResourceProvider.GetString("LOCSsvCsvImageTotalSizeBytes") },
                { FieldImageTotalSize, ResourceProvider.GetString("LOCSsvCsvImageTotalSize") },
                { FieldVideoCount, ResourceProvider.GetString("LOCSsvCsvVideoCount") },
                { FieldVideoTotalSizeBytes, ResourceProvider.GetString("LOCSsvCsvVideoTotalSizeBytes") },
                { FieldVideoTotalSize, ResourceProvider.GetString("LOCSsvCsvVideoTotalSize") },
                { FieldVideoTotalDuration, ResourceProvider.GetString("LOCSsvCsvVideoTotalDuration") },
                { FieldTotalCount, ResourceProvider.GetString("LOCSsvCsvTotalCount") },
                { FieldTotalSizeBytes, ResourceProvider.GetString("LOCSsvCsvTotalSizeBytes") },
                { FieldTotalSize, ResourceProvider.GetString("LOCSsvCsvTotalSize") },
                { FieldDateLastRefresh, ResourceProvider.GetString("LOCCommonDateData") }
            };
        }

        /// <inheritdoc />
        protected override IEnumerable<Dictionary<string, string>> GetRows(GameScreenshots item)
        {
            if (item?.Items == null || item.Items.Count == 0 || !item.HasData)
            {
                yield break;
            }

            List<Screenshot> images = item.Items.Where(x => !x.IsVideo).ToList();
            List<Screenshot> videos = item.Items.Where(x => x.IsVideo).ToList();

            long imageTotalSizeBytes = images.Sum(x => x.FileSize);
            long videoTotalSizeBytes = videos.Sum(x => x.FileSize);
            long totalSizeBytes = imageTotalSizeBytes + videoTotalSizeBytes;

            TimeSpan videoDuration = GetVideoDuration(videos);

            yield return new Dictionary<string, string>
            {
                { FieldGameName, item.Game?.Name ?? string.Empty },
                { FieldGameId, item.Id.ToString() },
                { FieldSource, PlayniteTools.GetSourceName(item.Id) },
                { FieldImageCount, images.Count.ToString() },
                { FieldImageTotalSizeBytes, imageTotalSizeBytes.ToString() },
                { FieldImageTotalSize, FormatSize(imageTotalSizeBytes) },
                { FieldVideoCount, videos.Count.ToString() },
                { FieldVideoTotalSizeBytes, videoTotalSizeBytes.ToString() },
                { FieldVideoTotalSize, FormatSize(videoTotalSizeBytes) },
                { FieldVideoTotalDuration, videoDuration == TimeSpan.Zero ? string.Empty : videoDuration.ToString(@"hh\:mm\:ss") },
                { FieldTotalCount, (images.Count + videos.Count).ToString() },
                { FieldTotalSizeBytes, totalSizeBytes.ToString() },
                { FieldTotalSize, FormatSize(totalSizeBytes) },
                { FieldDateLastRefresh, FormatCsvUtcDateTime(item.DateLastRefresh) }
            };
        }

        private static TimeSpan GetVideoDuration(List<Screenshot> videos)
        {
            if (videos == null || videos.Count == 0)
            {
                return TimeSpan.Zero;
            }

            string ffprobePath = ScreenshotsVisualizer.PluginDatabase?.PluginSettings?.FfprobePath;
            if (string.IsNullOrWhiteSpace(ffprobePath) || !File.Exists(ffprobePath))
            {
                return TimeSpan.Zero;
            }

            TimeSpan total = TimeSpan.Zero;
            foreach (Screenshot screenshot in videos)
            {
                total += screenshot.Duration;
            }

            return total;
        }

        private static string FormatSize(long bytes)
        {
            if (bytes <= 0)
            {
                return "0 B";
            }

            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            double value = bytes;
            int index = 0;
            while (value >= 1024d && index < suffixes.Length - 1)
            {
                value /= 1024d;
                index++;
            }

            return string.Format("{0:0.##} {1}", value, suffixes[index]);
        }
    }
}
