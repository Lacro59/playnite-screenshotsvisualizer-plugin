using CommonPluginsShared.Collections;
using Playnite.SDK;
using System;
using System.Linq;
using System.Text;

namespace ScreenshotsVisualizer.Models
{
    public class ScreeshotsVisualizeCollection : PluginItemCollection<GameScreenshots>
    {
        public ScreeshotsVisualizeCollection(string path, GameDatabaseCollection type = GameDatabaseCollection.Uknown) : base(path, type)
        {
        }
    }
}
