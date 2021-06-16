using CommonPluginsPlaynite.Common;
using CommonPluginsShared;
using MediaToolkit;
using MediaToolkit.Model;
using MediaToolkit.Options;
using Newtonsoft.Json;
using ScreenshotsVisualizer.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenshotsVisualizer.Models
{
    public class Screenshot
    {
        [JsonIgnore]
        private ScreenshotsVisualizerDatabase PluginDatabase = ScreenshotsVisualizer.PluginDatabase;

        /// <summary>
        /// Complete path file
        /// </summary>
        public string FileName { get; set; }
        public DateTime Modifed { get; set; }

        [JsonIgnore]
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

        [JsonIgnore]
        public string FileSizeString
        {
            get
            {
                if (File.Exists(FileName))
                {
                    return Tools.SizeSuffix(new FileInfo(FileName).Length);
                }

                return string.Empty;
            }
        }
        [JsonIgnore]
        public long FileSize
        {
            get
            {
                if (File.Exists(FileName))
                {
                    return new FileInfo(FileName).Length;
                }

                return 0;
            }
        }

        [JsonIgnore]
        public string FileNameOnly
        {
            get
            {
                return Path.GetFileName(FileName);
            }
        }

        [JsonIgnore]
        public bool IsVideo
        {
            get
            {
                if (!File.Exists(FileName))
                {
                    return false;
                }

                return Path.GetExtension(FileName).ToLower().Contains("mp4") || Path.GetExtension(FileName).ToLower().Contains("avi");
            }
        }

        #region Video
        [JsonIgnore]
        public string Thumbnail
        {
            get
            {
                if (IsVideo)
                {
                    string ext = Path.GetExtension(FileName);
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
                        var inputFile = new MediaFile { Filename = FileName };
                        var outputFile = new MediaFile { Filename = FileThumbnail };

                        using (var engine = new Engine())
                        {
                            engine.GetMetadata(inputFile);

                            var options = new ConversionOptions { Seek = TimeSpan.FromSeconds(Duration.TotalSeconds / 2) };
                            engine.GetThumbnail(inputFile, outputFile, options);
                        }

                        return FileThumbnail;
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false);
                    }
                }

                return string.Empty;
            }
        }

        [JsonIgnore]
        private TimeSpan _Duration = default(TimeSpan);

        [JsonIgnore]
        public string DurationString
        {
            get
            {
                if (IsVideo)
                {
                    return Duration.ToString(@"hh\:mm\:ss");
                }

                return string.Empty;
            }
        }

        [JsonIgnore]
        public TimeSpan Duration
        {
            get
            {
                if (IsVideo)
                {
                    if (_Duration != default(TimeSpan))
                    {
                        return _Duration;
                    }

                    try
                    {
                        var inputFile = new MediaFile { Filename = FileName };

                        using (var engine = new Engine())
                        {
                            engine.GetMetadata(inputFile);
                        }

                        _Duration = inputFile.Metadata.Duration;
                        return _Duration;
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false);
                    }
                }

                return default(TimeSpan);
            }
        }
        #endregion
    }
}
