using CommonPlayniteShared.Common;
using CommonPluginsShared;
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
                                if (File.Exists(PluginDatabase.PluginSettings.Settings.FfprobePath))
                                {
                                    string sizeArgs = "-v error -show_entries stream=width,height -of csv=s=x:p=0 \"{0}\"";
                                    _ = ProcessStarter.StartProcessWait(PluginDatabase.PluginSettings.Settings.FfprobePath, string.Format(sizeArgs, FileName), Path.GetDirectoryName(PluginDatabase.PluginSettings.Settings.FfprobePath), true, out string stdOut, out string stdErr);

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
        public string FileSizeString => File.Exists(FileName) ? Tools.SizeSuffix(new FileInfo(FileName).Length) : string.Empty;

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
                if (PluginDatabase.PluginSettings.Settings.UsedThumbnails)
                {
                    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(FileNameOnly);
                    string pathThumbnail = Path.Combine(PluginDatabase.Paths.PluginCachePath, "Thumbnails");
                    string fileThumbnail = Path.Combine(pathThumbnail, fileNameWithoutExt + $"_{fileNameWithoutExt}_Thumbnail.jpg");

                    if (File.Exists(fileThumbnail))
                    {
                        return fileThumbnail;
                    }

                    try
                    {
                        _ = ImageTools.Resize(FileName, 320, fileThumbnail);
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false, true, PluginDatabase.PluginName);
                    }

                    if (File.Exists(fileThumbnail))
                    {
                        return fileThumbnail;
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
                if (_thumbnail.IsNullOrEmpty())
                {
                    if (IsVideo)
                    {
                        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(FileNameOnly);
                        string pathThumbnail = Path.Combine(PluginDatabase.Paths.PluginCachePath, "Thumbnails");
                        string fileThumbnail = Path.Combine(pathThumbnail, fileNameWithoutExt + $"_{FileSize}_{Duration.TotalSeconds}_Thumbnail.jpg");

                        if (File.Exists(fileThumbnail))
                        {
                            _thumbnail = fileThumbnail;
                            return fileThumbnail;
                        }
                        FileSystem.CreateDirectory(pathThumbnail);

                        try
                        {
                            if (File.Exists(PluginDatabase.PluginSettings.Settings.FfmpegPath))
                            {
                                string thumbArgs = "-i \"{0}\" -frames 1 -vf \"select=not(mod(n\\,1000)),scale=320:320:force_original_aspect_ratio=decrease\" \"{1}\"";
                                _ = ProcessStarter.StartProcessWait(PluginDatabase.PluginSettings.Settings.FfmpegPath, string.Format(thumbArgs, FileName, fileThumbnail), Path.GetDirectoryName(PluginDatabase.PluginSettings.Settings.FfmpegPath), true, out string stdOut, out string stdErr);
                            }
                            else
                            {
                                Logger.Warn("No ffmpeg executable");
                                API.Instance.Notifications.Add(new NotificationMessage(
                                    $"{PluginDatabase.PluginName}-FfmpegPath-Error",
                                    $"{PluginDatabase.PluginName}\r\n" + ResourceProvider.GetString("LOCSsvFfmpegNotFound"),
                                    NotificationType.Error
                                ));
                            }

                            _thumbnail = fileThumbnail;
                            return fileThumbnail;
                        }
                        catch (Exception ex)
                        {
                            Common.LogError(ex, false, true, PluginDatabase.PluginName);
                        }
                    }
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
                            if (File.Exists(PluginDatabase.PluginSettings.Settings.FfprobePath))
                            {
                                string durationArgs = "-v error -show_entries format=duration -sexagesimal -of default=noprint_wrappers=1:nokey=1 \"{0}\"";
                                _ = ProcessStarter.StartProcessWait(PluginDatabase.PluginSettings.Settings.FfprobePath, string.Format(durationArgs, FileName), Path.GetDirectoryName(PluginDatabase.PluginSettings.Settings.FfprobePath), true, out string stdOut, out string stdErr);
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