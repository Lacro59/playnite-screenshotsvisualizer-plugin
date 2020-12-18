using Playnite.SDK;
using PluginCommon.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenshotsVisualizer.Models
{
    public class ScreeshotsVisualizeCollection : PluginItemCollection<GameScreenshots>
    {
        public ScreeshotsVisualizeCollection(string path, GameDatabaseCollection type = GameDatabaseCollection.Uknown) : base(path, type)
        {
        }
    }
}
