using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommonPluginsStores;
using Playnite.SDK;
using Playnite.SDK.Data;
using ScreenshotsVisualizer.Services;

namespace ScreenshotsVisualizer.Models
{
    public class GameSettings
    {
        public Guid Id { get; set; }
        public List<FolderSettings> ScreenshotsFolders { get; set; }

        // TEMP
        public bool UsedFilePattern { get; set; }
        public string FilePattern { get; set; }
        public string ScreenshotsFolder { get; set; }

        public List<string> GetScreenshotsFolders(IPlayniteAPI PlayniteApi)
        {
            return ScreenshotsFolders.Select(x => PlayniteTools.StringExpandWithStores(PlayniteApi.Database.Games.Get(Id), x.ScreenshotsFolder)).ToList();
        }
    }


    public class FolderSettings
    {
        public bool UsedFilePattern { get; set; }
        public string FilePattern { get; set; }
        public string ScreenshotsFolder { get; set; }
    }
}
