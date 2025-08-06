using CommonPluginsShared.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Playnite.SDK.Data;

namespace ScreenshotsVisualizer.Models
{
    public class GameScreenshots : PluginDataBaseGame<Screenshot>
    {
        public List<string> ScreenshotsFolders { get; set; }

        [DontSerialize]
        public bool FoldersExist => ScreenshotsFolders != null && ScreenshotsFolders.Any(Directory.Exists);

        [DontSerialize]
        public bool InSettings { get; set; }
    }
}