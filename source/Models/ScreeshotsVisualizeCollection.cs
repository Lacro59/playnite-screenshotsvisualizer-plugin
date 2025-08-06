using CommonPluginsShared.Collections;
using Playnite.SDK;

namespace ScreenshotsVisualizer.Models
{
    public class ScreeshotsVisualizeCollection : PluginItemCollection<GameScreenshots>
    {
        public ScreeshotsVisualizeCollection(string path, GameDatabaseCollection type = GameDatabaseCollection.Uknown) : base(path, type)
        {
        }
    }
}