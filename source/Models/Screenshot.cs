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
        private static ILogger Logger => LogManager.GetLogger();

        [DontSerialize]
        private ScreenshotsVisualizerDatabase PluginDatabase => ScreenshotsVisualizer.PluginDatabase;

        [DontSerialize]
        public Guid GameId { get; set; }

        /// <summary>
        /// Complete path file
        /// </summary>
        public string FileName { get; set; }
        public DateTime Modifed { get; set; }

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
                            try
                            {
                                if (File.Exists(PluginDatabase.PluginSettings.FfprobePath))
                                {
                                    string sizeArgs = "-v error -show_entries stream=width,height -of csv=s=x:p=0 \"{0}\"";
                                    _ = ProcessStarter.StartProcessWait(PluginDatabase.PluginSettings.FfprobePath, string.Format(sizeArgs, FileName), Path.GetDirectoryName(PluginDatabase.PluginSettings.FfprobePath), true, out string stdOut, out string stdErr);

                                    _sizeString = stdOut.Trim();
                                    return _sizeString;
                                }
                                else
                                {
                                    Logger.Warn("No ffprobe executable");
                                    API.Instance.Notifications.Add(new NotificationMessage(
                                        $"{PluginDatabase.PluginName}-FfprobePath-Error",
                                        $"{PluginDatabase.PluginName}\r\n" + ResourceProvider.GetString("LOCSsFfprobeNotFound"),
                                        NotificationType.Error
                                    ));
                                }
                            }
                            catch (Exception ex)
                            {
                                Common.LogError(ex, false, true, PluginDatabase.PluginName);
                            }
                        }
                        else
                        {
                            ImageProperties imageProperties = Images.GetImageProperties(FileName);
                            return imageProperties.Width + "x" + imageProperties.Height;
                        }
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
                if (PluginDatabase.PluginSettings.UsedThumbnails && !IsVideo)
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
                if (_duration == default)
                {
                    if (IsVideo)
                    {
                        try
                        {
                            if (File.Exists(PluginDatabase.PluginSettings.FfprobePath))
                            {
                                string durationArgs = "-v error -show_entries format=duration -sexagesimal -of default=noprint_wrappers=1:nokey=1 \"{0}\"";
                                _ = ProcessStarter.StartProcessWait(PluginDatabase.PluginSettings.FfprobePath, string.Format(durationArgs, FileName), Path.GetDirectoryName(PluginDatabase.PluginSettings.FfprobePath), true, out string stdOut, out string stdErr);
                                _ = TimeSpan.TryParse(stdOut, out _duration);
                            }
                            else
                            {
                                Logger.Warn("No ffprobe executable");
                                API.Instance.Notifications.Add(new NotificationMessage(
                                    $"{PluginDatabase.PluginName}-FfprobePath-Error",
                                    $"{PluginDatabase.PluginName}\r\n" + ResourceProvider.GetString("LOCSsFfprobeNotFound"),
                                    NotificationType.Error
                                ));
                            }

                            return _duration == null ? default : _duration;
                        }
                        catch (Exception ex)
                        {
                            Common.LogError(ex, false, true, PluginDatabase.PluginName);
                        }
                    }
                }
                return _duration;
            }

            set => SetValue(ref _duration, value);
        }

        #endregion
    }
}