using CommonPlayniteShared.Common;
using CommonPluginsShared;
using CommonPluginsShared.Utilities;
using Playnite.SDK;
using Playnite.SDK.Data;
using ScreenshotsVisualizer.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ScreenshotsVisualizer.Models
{
    public class Screenshot : ObservableObject
    {
        [DontSerialize]
        private ScreenshotsVisualizerDatabase PluginDatabase => ScreenshotsVisualizer.PluginDatabase;

        [DontSerialize]
        public Guid GameId { get; set; }

        /// <summary>
        /// Complete path file
        /// </summary>
        public string FileName { get; set; }
        public DateTime Modifed { get; set; }

        [DontSerialize]
        private bool _videoMetadataResolveAttempted;

        private string _sizeString;
        [DontSerialize]
        public string SizeString
        {
            get
            {
                if (_sizeString.IsNullOrEmpty())
                {
                    if (File.Exists(FileName))
                    {
                        if (IsVideo)
                        {
                            EnsureVideoMetadataResolved();
                            return _sizeString;
                        }

                        ImageProperties imageProperties = Images.GetImageProperties(FileName);
                        return imageProperties.Width + "x" + imageProperties.Height;
                    }
                }

                return _sizeString;
            }

            set => SetValue(ref _sizeString, value);
        }

        [DontSerialize]
        public string FileSizeString => File.Exists(FileName) ? UtilityTools.SizeSuffix(new FileInfo(FileName).Length) : string.Empty;

        [DontSerialize]
        public long FileSize => File.Exists(FileName) ? new FileInfo(FileName).Length : 0;

        [DontSerialize]
        public string FileNameOnly => Path.GetFileName(FileName);

        [DontSerialize]
        public bool IsVideo => new string[] { "mp4", "avi", "mkv", "webm" }.Any(s => Path.GetExtension(FileName).ToLower().Contains(s));

        [DontSerialize]
        public string ImageThumbnail
        {
            get
            {
                if (!IsVideo)
                {
                    string thumbnailPath = PluginDatabase.ThumbnailService.TryEnsureImageThumbnail(
                        FileName,
                        PluginDatabase.Paths.PluginCachePath,
                        Modifed);

                    if (!thumbnailPath.IsNullOrEmpty())
                    {
                        return thumbnailPath;
                    }
                }

                return FileName;
            }
        }

        #region Video

        private string _thumbnail;
        [DontSerialize]
        public string Thumbnail
        {
            get
            {
                if (_thumbnail.IsNullOrEmpty() && IsVideo)
                {
                    _thumbnail = PluginDatabase.ThumbnailService.TryEnsureVideoThumbnail(
                        FileName,
                        PluginDatabase.Paths.PluginCachePath,
                        Modifed,
                        FileSize,
                        Duration.TotalSeconds,
                        PluginDatabase.PluginSettings.FfmpegPath) ?? string.Empty;
                }

                return _thumbnail;
            }

            set => SetValue(ref _thumbnail, value);
        }

        [DontSerialize]
        public string DurationString => IsVideo ? Duration.ToString(@"hh\:mm\:ss") : string.Empty;

        private TimeSpan _duration = default;
        [DontSerialize]
        public TimeSpan Duration
        {
            get
            {
                if (IsVideo)
                {
                    EnsureVideoMetadataResolved();
                }

                return _duration;
            }

            set => SetValue(ref _duration, value);
        }

        private void EnsureVideoMetadataResolved()
        {
            if (_videoMetadataResolveAttempted || !IsVideo || !File.Exists(FileName))
            {
                return;
            }

            _videoMetadataResolveAttempted = true;

            try
            {
                Common.LogDebug(true, string.Format(
                    "[SsvVideoMetadata] Resolve start for '{0}' (modified={1:u})",
                    FileName,
                    Modifed.ToUniversalTime()));

                if (PluginDatabase.VideoMetadataService.TryGetVideoMetadata(
                    FileName,
                    Modifed,
                    PluginDatabase.PluginSettings.FfprobePath,
                    out SsvVideoMetadata metadata))
                {
                    _duration = metadata.Duration;
                    _sizeString = metadata.SizeString;
                    Common.LogDebug(true, string.Format(
                        "[SsvVideoMetadata] Resolve ok for '{0}' ({1}, {2})",
                        FileName,
                        _sizeString,
                        _duration));
                }
                else
                {
                    Common.LogDebug(true, string.Format(
                        "[SsvVideoMetadata] Resolve failed for '{0}'",
                        FileName));
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }
        }

        #endregion
    }
}
