using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenshotsVisualizer.Models
{
    public class GameSettings
    {
        public Guid Id { get; set; }
        public string ScreenshotsFolder { get; set; }
        public bool UsedFilePattern { get; set; }
        public string FilePattern { get; set; }
    }
}
