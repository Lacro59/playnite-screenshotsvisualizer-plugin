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


        protected override void GetPluginTags()
        {
#if DEBUG
            logger.Debug($"{PluginName} - GetPluginTags()");
#endif
            System.Threading.SpinWait.SpinUntil(() => _PlayniteApi.Database.IsOpen, -1);

            try
            {
                // Get tags in playnite database
                PluginTags = new List<Tag>();
                foreach (Tag tag in _PlayniteApi.Database.Tags)
                {
                    if (tag.Name.IndexOf("[SSV] ") > -1)
                    {
                        PluginTags.Add(tag);
                    }
                }

                // Add missing tags
                if (PluginTags.Count == 0)
                {
                    _PlayniteApi.Database.Tags.Add(new Tag { Name = $"[SSV] {resources.GetString("LOCSsvTitle")}" });

                    foreach (Tag tag in _PlayniteApi.Database.Tags)
                    {
                        if (tag.Name.IndexOf("[HLTB] ") > -1)
                        {
                            PluginTags.Add(tag);
                        }
                    }
                }

#if DEBUG
                logger.Debug($"{PluginName} - PluginTags: {JsonConvert.SerializeObject(PluginTags)}");
#endif
            }
            catch (Exception ex)
            {
                Common.LogError(ex, PluginName);
            }
        }

        public override void AddTag(Game game)
        {
            GameScreenshots gameScreenshots = Get(game, true);

            if (gameScreenshots.HasData)
            {
                try
                {
                    if (PluginTags.FirstOrDefault() != null)
                    {
                        Guid TagId = PluginTags.FirstOrDefault().Id;

                        if (game.TagIds != null)
                        {
                            game.TagIds.Add(TagId);
                        }
                        else
                        {
                            game.TagIds = new List<Guid> { TagId };
                        }

                        _PlayniteApi.Database.Games.Update(game);
                    }
                }
                catch (Exception ex)
                {
#if DEBUG
                    Common.LogError(ex, PluginName);
#endif
                    logger.Error($"{PluginName} - Tag insert error with {game.Name}");
                    _PlayniteApi.Notifications.Add(new NotificationMessage(
                        $"{PluginName}-Tag-Errors",
                        $"{PluginName}\r\n" + resources.GetString("LOCCommonNotificationTagError"),
                        NotificationType.Error
                    ));
                }
            }
        }
    }
}
