using CommonPluginsShared.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Playnite.SDK.Data;

namespace ScreenshotsVisualizer.Models
{
    public class GameScreenshots : PluginDataBaseGame<Screenshot>
    {
        private List<Screenshot> _Items = new List<Screenshot>();
        public override List<Screenshot> Items { get => _Items; set => SetValue(ref _Items, value); }

        public List<string> ScreenshotsFolders { get; set; }

        [DontSerialize]
        public bool FoldersExist
        {
            get
            {
                foreach(string Folder in ScreenshotsFolders)
                {
                    if (Directory.Exists(Folder))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        [DontSerialize]
        public bool InSettings { get; set; }
    }
}
