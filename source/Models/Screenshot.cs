using CommonPlayniteShared.Common;
using CommonPluginsShared;
using MediaToolkit;
using MediaToolkit.Model;
using MediaToolkit.Options;
using Playnite.SDK.Data;
using ScreenshotsVisualizer.Services;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace ScreenshotsVisualizer.Models
{
    public class Screenshot
    {
        [DontSerialize]
        private readonly ScreenshotsVisualizerDatabase PluginDatabase = ScreenshotsVisualizer.PluginDatabase;

        /// <summary>
        /// Complete path file
        /// </summary>
        public string FileName { get; set; }
        public DateTime Modifed { get; set; }

        [DontSerialize]
        public string SizeString
        {
            get
            {
                if (File.Exists(FileName))
                {
                    if (IsVideo)
                    {
                        ImageProperties imageProperties = Images.GetImageProperties(Thumbnail);
                        return imageProperties.Width + "x" + imageProperties.Height;
                    }
                    else
                    {
                        ImageProperties imageProperties = Images.GetImageProperties(FileName);
                        return imageProperties.Width + "x" + imageProperties.Height;
                    }
                }

                return string.Empty;
            }
        }

        [DontSerialize]
        public string FileSizeString => File.Exists(FileName) ? Tools.SizeSuffix(new FileInfo(FileName).Length) : string.Empty;

        [DontSerialize]
        public long FileSize => File.Exists(FileName) ? new FileInfo(FileName).Length : 0;

        [DontSerialize]
        public string FileNameOnly => Path.GetFileName(FileName);

        [DontSerialize]
        public bool IsVideo => File.Exists(FileName) 
            && (Path.GetExtension(FileName).ToLower().Contains("mp4") || Path.GetExtension(FileName).ToLower().Contains("avi") || Path.GetExtension(FileName).ToLower().Contains("mkv") || Path.GetExtension(FileName).ToLower().Contains("webm"));

        [DontSerialize]
        public string ImageThumbnail
        {
            get
            {
                if (PluginDatabase.PluginSettings.Settings.UsedThumbnails)
                {
                    string FileNameWithoutExt = Path.GetFileNameWithoutExtension(FileNameOnly);
                    string PathThumbnail = Path.Combine(PluginDatabase.Paths.PluginCachePath, "Thumbnails");
                    string FileThumbnail = Path.Combine(PathThumbnail, FileNameWithoutExt + $"_{FileNameWithoutExt}_Thumbnail.jpg");

                    if (File.Exists(FileThumbnail))
                    {
                        return FileThumbnail;
                    }

                    try
                    {
                        ImageTools.Resize(FileName, 200, FileThumbnail);
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false, true, "ScreenshootsVisualizer");
                    }

                    if (File.Exists(FileThumbnail))
                    {
                        return FileThumbnail;
                    }
                }

                return FileName;
            }
        }

        #region Video
        [DontSerialize]
        public string Thumbnail
        {
            get
            {
                if (IsVideo)
                {
                    string FileNameWithoutExt = Path.GetFileNameWithoutExtension(FileNameOnly);
                    string PathThumbnail = Path.Combine(PluginDatabase.Paths.PluginCachePath, "Thumbnails");
                    string FileThumbnail = Path.Combine(PathThumbnail, FileNameWithoutExt + $"_{FileSize}_{Duration.TotalSeconds}_Thumbnail.jpg");

                    if (File.Exists(FileThumbnail))
                    {
                        return FileThumbnail;
                    }

                    if (!Directory.Exists(PathThumbnail))
                    {
                        Directory.CreateDirectory(PathThumbnail);
                    }

                    try
                    {
                        MediaFile inputFile = new MediaFile { Filename = FileName };
                        MediaFile outputFile = new MediaFile { Filename = FileThumbnail };

                        using (var engine = new Engine())
                        {
                            engine.GetMetadata(inputFile);

                            ConversionOptions options = new ConversionOptions { Seek = TimeSpan.FromSeconds(Duration.TotalSeconds / 2) };
                            engine.GetThumbnail(inputFile, outputFile, options);
                        }

                        return FileThumbnail;
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false, true, PluginDatabase.PluginName);
                    }
                }

                return string.Empty;
            }
        }

        [DontSerialize]
        public string DurationString => IsVideo ? Duration.ToString(@"hh\:mm\:ss") : string.Empty;

        public TimeSpan _Duration = default;
        [DontSerialize]
        public TimeSpan Duration
        {
            get
            {
                if (IsVideo)
                {
                    if (_Duration != default)
                    {
                        return _Duration;
                    }

                    try
                    {
                        MediaFile inputFile = new MediaFile { Filename = FileName };
                        using (Engine engine = new Engine())
                        {
                            engine.GetMetadata(inputFile);
                        }

                        _Duration = inputFile.Metadata.Duration;
                        return _Duration;
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false, true, PluginDatabase.PluginName);
                    }
                }

                return default;
            }
        }
        #endregion
    }
}
