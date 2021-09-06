using CommonPluginsShared.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Playnite.SDK.Data;

namespace ScreenshotsVisualizer.Models
{
    public class GameScreenshots : PluginDataBaseGame<Screenshot>
    {
        private List<Screenshot> _Items = new List<Screenshot>();
        public override List<Screenshot> Items
        {
            get
            {
                return _Items;
            }

            set
            {
                _Items = value;
                OnPropertyChanged();
            }
        }

        // TEMP
        public string ScreenshotsFolder { get; set; }

        public List<string> ScreenshotsFolders { get; set; }

        [DontSerialize]
        public bool FoldersExist
        {
            get
            {
                foreach(string Folder in ScreenshotsFolders)
                {
                    if (!Directory.Exists(Folder))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        [DontSerialize]
        public bool InSettings { get; set; }
    }
}
