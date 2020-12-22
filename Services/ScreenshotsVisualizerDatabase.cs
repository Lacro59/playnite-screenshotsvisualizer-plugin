using Newtonsoft.Json;
using Playnite.SDK;
using Playnite.SDK.Models;
using PluginCommon;
using PluginCommon.Collections;
using ScreenshotsVisualizer.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenshotsVisualizer.Services
{
    public class ScreenshotsVisualizerDatabase : PluginDatabaseObject<ScreenshotsVisualizerSettings, ScreeshotsVisualizeCollection, GameScreenshots>
    {
        public ScreenshotsVisualizerDatabase(IPlayniteAPI PlayniteApi, ScreenshotsVisualizerSettings PluginSettings, string PluginUserDataPath) : base(PlayniteApi, PluginSettings, PluginUserDataPath)
        {
            PluginName = "ScreenshotsVisualizer";

            ControlAndCreateDirectory(PluginUserDataPath, "ScreenshotsVisualizer");
        }


        protected override bool LoadDatabase()
        {
            IsLoaded = false;

            if (Directory.Exists(Path.Combine(PluginUserDataPath, "ScreenshotsVisualizer")))
            {
                string[] files = Directory.GetFiles(Path.Combine(PluginUserDataPath, "ScreenshotsVisualizer"));
                foreach (string file in files)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                    }
                }
            }

            Database = new ScreeshotsVisualizeCollection(PluginDatabaseDirectory);
            Database.SetGameInfo<Screenshot>(_PlayniteApi);

            GetFromSettings();

            GameSelectedData = new GameScreenshots();
            GetPluginTags();

            IsLoaded = true;
            return true;
        }

        public void RefreshData(Game game)
        {
            GameSettings gameSettings = PluginSettings.gameSettings.Find(x => x.Id == game.Id);

            if (gameSettings != null)
            {
                SetDataFromSettings(gameSettings);
            }
        }


        public override GameScreenshots Get(Guid Id, bool OnlyCache = false)
        {
            GameIsLoaded = false;
            GameScreenshots gameScreenshots = base.GetOnlyCache(Id);
#if DEBUG
            logger.Debug($"{PluginName} - GetFromDb({Id.ToString()}) - gameScreenshots: {JsonConvert.SerializeObject(gameScreenshots)}");
#endif

            if (gameScreenshots == null)
            {
                Game game = _PlayniteApi.Database.Games.Get(Id);
                gameScreenshots = GetDefault(game);
                Add(gameScreenshots);
            }

            GameIsLoaded = true;
            return gameScreenshots;
        }


        public void GetFromSettings()
        {
            foreach (var item in PluginSettings.gameSettings)
            {
                SetDataFromSettings(item);
            }
        }

        private void SetDataFromSettings(GameSettings item)
        {
            Game game = _PlayniteApi.Database.Games.Get(item.Id);
            GameScreenshots gameScreenshots = GetDefault(game);

            try
            {
                gameScreenshots.ScreenshotsFolder = item.ScreenshotsFolder;
                gameScreenshots.InSettings = true;

                // Get files
                string[] extensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".jfif", ".tga" };
                if (Directory.Exists(gameScreenshots.ScreenshotsFolder))
                {
                    Parallel.ForEach(Directory.EnumerateFiles(gameScreenshots.ScreenshotsFolder, "*.*")
                        .Where(s => extensions.Any(ext => ext == Path.GetExtension(s))), (objectFile) =>
                        {
                            try
                            {
                                DateTime Modified = File.GetLastWriteTime(objectFile);

                                gameScreenshots.Items.Add(new Screenshot
                                {
                                    FileName = objectFile,
                                    Modifed = Modified
                                });
                            }
                            catch
                            {

                            }
                        });
                }
                else
                {
                    //logger.Warn($"ScreenshotsVisualizer - Screenshots directory not found for {game.Name}");
                }

                gameScreenshots.Items = gameScreenshots.Items.Where(x => x != null).ToList();

                if (Database.Get(game.Id) != null)
                {
                    Database.Remove(game.Id);
                }

                Database.Add(gameScreenshots);
            }
            catch (Exception ex)
            {
                Common.LogError(ex, PluginName, $"Error on File load for {game.Name} on {gameScreenshots.ScreenshotsFolder}");
            }
        }
    }
}
