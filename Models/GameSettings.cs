using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommonPluginsShared;
using Playnite.SDK;
using Playnite.SDK.Data;
using ScreenshotsVisualizer.Services;

namespace ScreenshotsVisualizer.Models
{
    public class GameSettings
    {
        public Guid Id { get; set; }
        public string ScreenshotsFolder { get; set; }
        public bool UsedFilePattern { get; set; }
        public string FilePattern { get; set; }

        public string GetScreenshotsFolder(IPlayniteAPI PlayniteApi)
        {
            return PlayniteTools.StringExpand(PlayniteApi.Database.Games.Get(Id), ScreenshotsFolder);
        }
    }
}
