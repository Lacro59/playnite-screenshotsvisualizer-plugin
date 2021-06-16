using CommonPluginsPlaynite.Common;
using CommonPluginsShared;
using Newtonsoft.Json;
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
                    if (Path.GetExtension(FileName).ToLower().Contains("mp4"))
                    {
                        return string.Empty;
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
    }
}
