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
                    ImageProperty imageProperty = ImageTools.GetImapeProperty(FileName);
                    if (imageProperty != null)
                    {
                        return imageProperty.Width + "x" + imageProperty.Height;
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
    }
}
