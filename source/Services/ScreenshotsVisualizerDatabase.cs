using CommonPluginsShared;
using CommonPluginsShared.Collections;
using Playnite.SDK;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows;
using ScreenshotsVisualizer.Views;
using System.Threading;

namespace ScreenshotsVisualizer.Services
{
    public class ScreenshotsVisualizerDatabase : PluginDatabaseObject<ScreenshotsVisualizerSettingsViewModel, ScreeshotsVisualizeCollection, GameScreenshots>
    {
        public ScreenshotsVisualizerDatabase(IPlayniteAPI PlayniteApi, ScreenshotsVisualizerSettingsViewModel PluginSettings, string PluginUserDataPath) : base(PlayniteApi, PluginSettings, "ScreenshotsVisualizer", PluginUserDataPath)
        {
            TagBefore = "[SSV]";
        }


        protected override bool LoadDatabase()
        {
            try
            {
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();

                Database = new ScreeshotsVisualizeCollection(Paths.PluginDatabasePath);
                Database.SetGameInfo<Screenshot>(PlayniteApi);

                stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed;
                logger.Info($"LoadDatabase with {Database.Count} items - {string.Format("{0:00}:{1:00}.{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10)}");
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, "SuccessStory");
                return false;
            }

            return true;
        }


        public void RefreshDataAll()
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            foreach (var item in Database.Items)
            {
                GameSettings gameSettings = PluginSettings.Settings.gameSettings.Find(x => x.Id == item.Key);

                if (gameSettings != null)
                {
                    SetDataFromSettings(gameSettings);
                }
            }

            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;
            logger.Info($"RefreshDataAll - {string.Format("{0:00}:{1:00}.{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10)}");
        }

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
                    Common.LogError(ex, false, true, "ScreenshotsVisualizer");
                }
            }, globalProgressOptions);
        }

        public void RefreshData(List<Guid> Ids)
        {
            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(
                $"{PluginName} - {resources.GetString("LOCCommonRefreshGameData")}",
                true
            );
            globalProgressOptions.IsIndeterminate = false;

            PlayniteApi.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
            {
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();

                activateGlobalProgress.ProgressMaxValue = Ids.Count;

                string CancelText = string.Empty;

                try
                {
                    foreach (Guid Id in Ids)
                    {
                        GameSettings gameSettings = PluginSettings.Settings.gameSettings.Find(x => x.Id == Id);

                        if (gameSettings != null)
                        {
                            SetDataFromSettings(gameSettings);
                        }

                        activateGlobalProgress.CurrentProgressValue++;
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, "ScreenshotsVisualizer");
                }

                stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed;
                logger.Info($"Task Refresh(){CancelText} - {string.Format("{0:00}:{1:00}.{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10)} for {activateGlobalProgress.CurrentProgressValue}/{Ids.Count} items");
            }, globalProgressOptions);
        }


        public void MoveToFolderToSave(Game game)
        {
            if (PluginSettings.Settings.EnableFolderToSave)
            {
                GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(
                    $"{PluginName} - {resources.GetString("LOCSsvMovingToSave")}",
                    false
                );
                globalProgressOptions.IsIndeterminate = true;

                PlayniteApi.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
                {
                    try
                    {
                        // Refresh data
                        GameSettings gameSettings = PluginSettings.Settings.gameSettings.Find(x => x.Id == game.Id);
                        if (gameSettings != null)
                        {
                            SetDataFromSettings(gameSettings);
                        }


                        string PathFolder = CommonPluginsStores.PlayniteTools.StringExpandWithStores(game, PluginSettings.Settings.FolderToSave);
                        string Pattern = CommonPluginsStores.PlayniteTools.StringExpandWithStores(game, PluginSettings.Settings.FileSavePattern);
                        string PatternWithDigit = string.Empty;

                        GameScreenshots gameScreenshots = Get(game);
                        int digit = 1;

                        if (!Directory.Exists(PathFolder))
                        {
                            Directory.CreateDirectory(PathFolder);
                        }

                        bool HaveDigit = false;
                        foreach (Screenshot screenshot in gameScreenshots.Items)
                        {
                            if (File.Exists(screenshot.FileName))
                            {
                                string ext = Path.GetExtension(screenshot.FileName);

                                Pattern = Pattern.Replace("{DateModified}", screenshot.Modifed.ToString("yyyy-MM-dd"));
                                Pattern = Pattern.Replace("{DateTimeModified}", screenshot.Modifed.ToString("yyyy-MM-dd HH_mm_ss"));

                                if (Pattern.Contains("{digit}"))
                                {
                                    HaveDigit = true;
                                    PatternWithDigit = Pattern;
                                    Pattern = PatternWithDigit.Replace("{digit}", string.Format("{0:0000}", digit));
                                    digit++;
                                }

                                Pattern = CommonPlayniteShared.Common.Paths.GetSafePathName(Pattern);

                                string destFileName = Path.Combine(PathFolder, Pattern);


                                // If file exists
                                if (File.Exists(destFileName + ext))
                                {
                                    if (HaveDigit)
                                    {
                                        while (File.Exists(destFileName + ext))
                                        {
                                            Pattern = PatternWithDigit.Replace("{digit}", string.Format("{0:0000}", digit));
                                            Pattern = CommonPlayniteShared.Common.Paths.GetSafePathName(Pattern);
                                            destFileName = Path.Combine(PathFolder, Pattern);
                                            digit++;
                                        }
                                    }
                                    else
                                    {
                                        destFileName += $" ({DateTime.Now.ToString("yyyy-MM-dd HH_mm_ss")})";
                                    }
                                }


                                try
                                {
                                    File.Move(screenshot.FileName, destFileName + ext);
                                }
                                catch (Exception ex)
                                {
                                    Common.LogError(ex, false, true, "ScreenshotsVisualizer");
                                    break;
                                }
                            }
                        }


                        // Refresh data
                        if (gameSettings != null)
                        {
                            SetDataFromSettings(gameSettings);
                        }
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false, true, "ScreenshotsVisualizer");
                    }
                }, globalProgressOptions);
            }
        }


        public override GameScreenshots Get(Guid Id, bool OnlyCache = false, bool Force = false)
        {
            GameScreenshots gameScreenshots = base.GetOnlyCache(Id);

            if (gameScreenshots == null)
            {
                Game game = PlayniteApi.Database.Games.Get(Id);
                if (game != null)
                {
                    gameScreenshots = GetDefault(game);
                    Add(gameScreenshots);
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

        public void SetDataFromSettings(GameSettings item)
        {
            System.Threading.SpinWait.SpinUntil(() => PlayniteApi.Database.IsOpen, -1);

            Game game = PlayniteApi.Database.Games.Get(item.Id);
            GameScreenshots gameScreenshots = GetDefault(game);

            try
            {
                if (PluginSettings.Settings.EnableFolderToSave && item.ScreenshotsFolders.Find(x => x.ScreenshotsFolder == PluginSettings.Settings.FolderToSave) == null)
                {
                    item.ScreenshotsFolders.Add(new FolderSettings
                    {
                        ScreenshotsFolder = PluginSettings.Settings.FolderToSave,
                        UsedFilePattern = true,
                        FilePattern = PluginSettings.Settings.FileSavePattern
                    });
                }

                gameScreenshots.ScreenshotsFolders = item.GetScreenshotsFolders(PlayniteApi);
                gameScreenshots.InSettings = true;

                foreach (var ScreenshotsFolder in item.ScreenshotsFolders)
                {
                    string PathFolder = CommonPluginsStores.PlayniteTools.StringExpandWithStores(game, ScreenshotsFolder.ScreenshotsFolder);

                    // Get files
                    string[] extensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".jfif", ".tga", ".mp4", ".avi" };
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
                                        string Pattern = CommonPluginsStores.PlayniteTools.StringExpandWithStores(game, ScreenshotsFolder.FilePattern);
                                        Pattern = CommonPlayniteShared.Common.Paths.GetSafePathName(Pattern);

                                        Pattern = Pattern.Replace("(", @"\(");
                                        Pattern = Pattern.Replace(")", @"\)");

                                        Pattern = Pattern.Replace("{digit}", @"\d*");
                                        Pattern = Pattern.Replace("{DateModified}", @"[0-9]{4}-[0-9]{2}-[0-9]{2}");
                                        Pattern = Pattern.Replace("{DateTimeModified}", @"[0-9]{4}-[0-9]{2}-[0-9]{2} [0-9]{2}_[0-9]{2}_[0-9]{2} ");
                              
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
                                    Common.LogError(ex, false, true, "ScreenshotsVisualizer");
                                }
                            });
                    }
                    else
                    {
                        logger.Warn($"Screenshots directory not found for {game.Name}");
                    }

                    var elements = gameScreenshots?.Items?.Where(x => x != null);
                    if (elements?.Count() > 0)
                    {
                        gameScreenshots.DateLastRefresh = DateTime.Now;
                        gameScreenshots.Items = elements.ToList();


                        // set duration if has video
                        var VideoElements = gameScreenshots.Items.Where(x => x.IsVideo).Select(x => x.Thumbnail);
                        Thread.Sleep(1000 * VideoElements.Count());
                    }

                    AddOrUpdate(gameScreenshots);
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error on file load for {game.Name}", true, "ScreenshotsVisualizer");
            }
        }


        #region Tag
        public override void AddTag(Game game, bool noUpdate = false)
        {
            GetPluginTags();
            GameScreenshots gameScreenshots = Get(game, true);

            if (gameScreenshots.HasData)
            {
                try
                {
                    Guid? TagId = FindGoodPluginTags(resources.GetString("LOCSsvTitle"));
                    if (TagId != null)
                    {                        
                        if (game.TagIds != null)
                        {
                            game.TagIds.Add((Guid)TagId);
                        }
                        else
                        {
                            game.TagIds = new List<Guid> { (Guid)TagId };
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
        #endregion


        public override void SetThemesResources(Game game)
        {
            GameScreenshots gameScreenshots = Get(game, true);

            if (gameScreenshots == null)
            {
                PluginSettings.Settings.HasData = false;
                PluginSettings.Settings.ListScreenshots = new List<Screenshot>();

                return;
            }

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


        public void ListBoxItem_MouseLeftButtonDownClick(object sender, MouseButtonEventArgs e)
        {
            ListBox listBox = (ListBox)sender;
            ListBoxItem item = ItemsControl.ContainerFromElement(listBox, e.OriginalSource as DependencyObject) as ListBoxItem;
            if (item != null)
            {
                int index = listBox.SelectedIndex;
                if (index == -1)
                {
                    return;
                }

                Screenshot screenshot = ((Screenshot)listBox.Items[index]);

                bool IsGood = false;

                if (PluginSettings.Settings.OpenViewerWithOnSelection)
                {
                    IsGood = true;
                }
                else
                {
                    if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
                    {
                        IsGood = true;
                    }
                }


                if (IsGood)
                {
                    WindowOptions windowOptions = new WindowOptions
                    {
                        ShowMinimizeButton = false,
                        ShowMaximizeButton = true,
                        ShowCloseButton = true,
                        Height = 720,
                        Width = 1280
                    };

                    var ViewExtension = new SsvSinglePictureView(screenshot);
                    Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PlayniteApi, resources.GetString("LOCSsv"), ViewExtension, windowOptions);
                    windowExtension.ResizeMode = ResizeMode.CanResize;
                    windowExtension.ShowDialog();
                }
            }
            else
            {

            }
        }

    }
}
