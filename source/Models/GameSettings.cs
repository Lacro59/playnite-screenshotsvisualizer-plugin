using System;
using System.Collections.Generic;
using System.Linq;
using CommonPluginsStores;
using Playnite.SDK;

namespace ScreenshotsVisualizer.Models
{
    public class GameSettings
    {
        public Guid Id { get; set; }
        public List<FolderSettings> ScreenshotsFolders { get; set; }

        // TODO TEMP
        public bool ScanSubFolders { get; set; }
        public bool UsedFilePattern { get; set; }
        public string FilePattern { get; set; }
        public string ScreenshotsFolder { get; set; }

        public List<string> GetScreenshotsFolders() => ScreenshotsFolders
                .Select(x => PlayniteTools.StringExpandWithStores(API.Instance.Database.Games.Get(Id), x.ScreenshotsFolder))
                .ToList();
    }


    public class FolderSettings
    {
        public bool ScanSubFolders { get; set; }
        public bool UsedFilePattern { get; set; }
        public string FilePattern { get; set; }
        public string ScreenshotsFolder { get; set; }
    }
}