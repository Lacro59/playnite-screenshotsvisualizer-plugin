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
        public string SizeString { get; set; }

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
