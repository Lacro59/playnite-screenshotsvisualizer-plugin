using CommonPluginsShared;
using CommonPluginsShared.Collections;
using Newtonsoft.Json;
using Playnite.SDK;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ScreenshotsVisualizer.Services
{
    public class ScreenshotsVisualizerDatabase : PluginDatabaseObject<ScreenshotsVisualizerSettingsViewModel, ScreeshotsVisualizeCollection, GameScreenshots>
    {
        public ScreenshotsVisualizerDatabase(IPlayniteAPI PlayniteApi, ScreenshotsVisualizerSettingsViewModel PluginSettings, string PluginUserDataPath) : base(PlayniteApi, PluginSettings, "ScreenshotsVisualizer", PluginUserDataPath)
        {

        }


        protected override bool LoadDatabase()
        {
            IsLoaded = false;

            Database = new ScreeshotsVisualizeCollection(Paths.PluginDatabasePath);
            Database.SetGameInfo<Screenshot>(PlayniteApi);

            GetPluginTags();

            IsLoaded = true;
            return true;
        }


        /*
        public void RefreshDataAll()
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            foreach (var item in Database.Items)
            {
                Game game = PlayniteApi.Database.Games.Get(item.Key);
                if (game != null)
                {
                    RefreshData(game);
                }
            }

            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;
            logger.Info($"RefreshDataAll - {string.Format("{0:00}:{1:00}.{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10)}");
        }
        */

        public void RefreshData(Game game)
        {
            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(
                $"{PluginName} - {resources.GetString("LOCCommonRefreshGameData")}",
                false
            );
            globalProgressOptions.IsIndeterminate = true;

            PlayniteApi.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
            {
                try
                {
                    GameSettings gameSettings = PluginSettings.Settings.gameSettings.Find(x => x.Id == game.Id);

                    if (gameSettings != null)
                    {
                        SetDataFromSettings(gameSettings);
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false);
                }
            }, globalProgressOptions);
        }


        public override GameScreenshots Get(Guid Id, bool OnlyCache = false, bool Force = false)
        {
            GameScreenshots gameScreenshots = base.GetOnlyCache(Id);

            if (gameScreenshots == null)
            {
                try
                {
                    Game game = PlayniteApi.Database.Games.Get(Id);
                    gameScreenshots = GetDefault(game);
                    Add(gameScreenshots);
                }
                catch
                {
                }
            }
            
            return gameScreenshots;
        }


        public void GetFromSettings()
        {
            foreach (var item in PluginSettings.Settings.gameSettings)
            {
                SetDataFromSettings(item);
            }
        }

        private void SetDataFromSettings(GameSettings item)
        {
            System.Threading.SpinWait.SpinUntil(() => PlayniteApi.Database.IsOpen, -1);

            Game game = PlayniteApi.Database.Games.Get(item.Id);
            GameScreenshots gameScreenshots = GetDefault(game);

            try
            {
                gameScreenshots.ScreenshotsFolders = item.GetScreenshotsFolders(PlayniteApi);
                gameScreenshots.InSettings = true;

                foreach (var ScreenshotsFolder in item.ScreenshotsFolders)
                {
                    string PathFolder = PlayniteTools.StringExpand(game, ScreenshotsFolder.ScreenshotsFolder);

                    // Get files
                    string[] extensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".jfif", ".tga" };
                    if (Directory.Exists(PathFolder))
                    {
                        Parallel.ForEach(Directory.EnumerateFiles(PathFolder, "*.*")
                            .Where(s => extensions.Any(ext => ext == Path.GetExtension(s))), (objectFile) =>
                            {
                                try
                                {
                                    DateTime Modified = File.GetLastWriteTime(objectFile);

                                    if (ScreenshotsFolder.UsedFilePattern)
                                    {
                                        string Pattern = ScreenshotsFolder.FilePattern.Replace("{digit}", @"\d*");
                                        Pattern = PlayniteTools.StringExpand(game, Pattern);

                                        if (Regex.IsMatch(Path.GetFileNameWithoutExtension(objectFile), Pattern, RegexOptions.IgnoreCase))
                                        {
                                            gameScreenshots.Items.Add(new Screenshot
                                            {
                                                FileName = objectFile,
                                                Modifed = Modified
                                            });
                                        }
                                    }
                                    else
                                    {
                                        gameScreenshots.Items.Add(new Screenshot
                                        {
                                            FileName = objectFile,
                                            Modifed = Modified,
                                        });
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Common.LogError(ex, false);
                                }
                            });
                    }
                    else
                    {
                        Common.LogDebug(true, $"Screenshots directory not found for {game.Name}");
                    }

                    var elements = gameScreenshots.Items.Where(x => x != null);
                    if (elements.Count() > 0)
                    {
                        gameScreenshots.Items = elements.ToList();
                    }

                    AddOrUpdate(gameScreenshots);
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error on File load for {game.Name}");
            }
        }


        protected override void GetPluginTags()
        {
            Common.LogDebug(true, $"{PluginName} - GetPluginTags()");
            System.Threading.SpinWait.SpinUntil(() => PlayniteApi.Database.IsOpen, -1);

            try
            {
                // Get tags in playnite database
                PluginTags = new List<Tag>();
                foreach (Tag tag in PlayniteApi.Database.Tags)
                {
                    if (tag.Name.IndexOf("[SSV] ") > -1)
                    {
                        PluginTags.Add(tag);
                    }
                }

                // Add missing tags
                if (PluginTags.Count == 0)
                {
                    PlayniteApi.Database.Tags.Add(new Tag { Name = $"[SSV] {resources.GetString("LOCSsvTitle")}" });

                    foreach (Tag tag in PlayniteApi.Database.Tags)
                    {
                        if (tag.Name.IndexOf("[SSV] ") > -1)
                        {
                            PluginTags.Add(tag);
                        }
                    }
                }

                Common.LogDebug(true, $"PluginTags: {JsonConvert.SerializeObject(PluginTags)}");
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false);
            }
        }

        public override void AddTag(Game game, bool noUpdate = false)
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

                        PlayniteApi.Database.Games.Update(game);
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, true);
                    logger.Error($"Tag insert error with {game.Name}");

                    PlayniteApi.Notifications.Add(new NotificationMessage(
                        $"{PluginName}-Tag-Errors",
                        $"{PluginName}\r\n" + resources.GetString("LOCCommonNotificationTagError"),
                        NotificationType.Error
                    ));
                }
            }
        }


        public override void SetThemesResources(Game game)
        {
            GameScreenshots gameScreenshots = Get(game, true);

            PluginSettings.Settings.HasData = gameScreenshots.HasData;
            PluginSettings.Settings.ListScreenshots = gameScreenshots.Items;
        }

        public override void Games_ItemUpdated(object sender, ItemUpdatedEventArgs<Game> e)
        {
            foreach (var GameUpdated in e.UpdatedItems)
            {
                Database.SetGameInfo<Screenshot>(PlayniteApi, GameUpdated.NewData.Id);
            }
        }
    }
}
