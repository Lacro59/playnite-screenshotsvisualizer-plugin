﻿using CommonPluginsShared;
using CommonPluginsShared.Collections;
using Newtonsoft.Json;
using Playnite.SDK;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
using System;
using System.Collections.Generic;
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

            // TODO Better way?
            if (Directory.Exists(Path.Combine(Paths.PluginDatabasePath, "ScreenshotsVisualizer")))
            {
                string[] files = Directory.GetFiles(Path.Combine(Paths.PluginDatabasePath, "ScreenshotsVisualizer"));
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

            Database = new ScreeshotsVisualizeCollection(Paths.PluginDatabasePath);
            Database.SetGameInfo<Screenshot>(PlayniteApi);

            GetFromSettings();
            GetPluginTags();

            IsLoaded = true;
            return true;
        }

        public void RefreshData(Game game)
        {
            GameSettings gameSettings = PluginSettings.Settings.gameSettings.Find(x => x.Id == game.Id);

            if (gameSettings != null)
            {
                SetDataFromSettings(gameSettings);
            }
        }


        public override GameScreenshots Get(Guid Id, bool OnlyCache = false)
        {
            GameScreenshots gameScreenshots = base.GetOnlyCache(Id);
#if DEBUG
            logger.Debug($"{PluginName} - GetFromDb({Id.ToString()}) - gameScreenshots: {JsonConvert.SerializeObject(gameScreenshots)}");
#endif

            if (gameScreenshots == null)
            {
                Game game = PlayniteApi.Database.Games.Get(Id);
                gameScreenshots = GetDefault(game);
                Add(gameScreenshots);
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
            Game game = PlayniteApi.Database.Games.Get(item.Id);
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
                                ImageProperty imageProperty = ImageTools.GetImapeProperty(objectFile);
                                string SizeString = imageProperty.Width + "x" + imageProperty.Height;

                                if (item.UsedFilePattern)
                                {
                                    string Pattern = item.FilePattern.Replace("{digit}", @"\d*");

                                    if (Regex.IsMatch(Path.GetFileNameWithoutExtension(objectFile), Pattern, RegexOptions.IgnoreCase))
                                    {
                                        gameScreenshots.Items.Add(new Screenshot
                                        {
                                            FileName = objectFile,
                                            Modifed = Modified,
                                            SizeString = SizeString
                                        });
                                    }
                                }
                                else
                                {
                                    gameScreenshots.Items.Add(new Screenshot
                                    {
                                        FileName = objectFile,
                                        Modifed = Modified,
                                        SizeString = SizeString
                                    });
                                }
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
                Common.LogError(ex, false, $"Error on File load for {game.Name} on {gameScreenshots.ScreenshotsFolder}");
            }
        }


        protected override void GetPluginTags()
        {
#if DEBUG
            logger.Debug($"{PluginName} - GetPluginTags()");
#endif
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
                    logger.Error($"{PluginName} - Tag insert error with {game.Name}");

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
