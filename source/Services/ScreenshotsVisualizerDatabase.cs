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
using Playnite.SDK.Data;
using CommonPluginsShared.Extensions;

namespace ScreenshotsVisualizer.Services
{
    public class ScreenshotsVisualizerDatabase : PluginDatabaseObject<ScreenshotsVisualizerSettingsViewModel, ScreeshotsVisualizeCollection, GameScreenshots, Screenshot>
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
                Common.LogError(ex, false, true, "ScreenshotsVisualizaer");
                return false;
            }

            return true;
        }


        #region Refresh data
        public void RefreshDataAll()
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

                string CancelText = string.Empty;
                activateGlobalProgress.ProgressMaxValue = Database.Items.Count;

                foreach (var item in Database.Items)
                {
                    if (activateGlobalProgress.CancelToken.IsCancellationRequested)
                    {
                        CancelText = " canceled";
                        break;
                    }

                    GameSettings gameSettings = GetGameSettings(item.Key);
                    if (gameSettings != null)
                    {
                        SetDataFromSettings(gameSettings);
                    }
                    activateGlobalProgress.CurrentProgressValue++;
                }

                stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed;
                logger.Info($"RefreshDataAll(){CancelText} - {string.Format("{0:00}:{1:00}.{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10)}");
            }, globalProgressOptions);
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
                    GameSettings gameSettings = GetGameSettings(game.Id);
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

                string CancelText = string.Empty;
                activateGlobalProgress.ProgressMaxValue = Ids.Count;

                try
                {
                    foreach (Guid Id in Ids)
                    {
                        if (activateGlobalProgress.CancelToken.IsCancellationRequested)
                        {
                            CancelText = " canceled";
                            break;
                        }

                        GameSettings gameSettings = GetGameSettings(Id);
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
                logger.Info($"Task RefreshData(){CancelText} - {string.Format("{0:00}:{1:00}.{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10)} for {activateGlobalProgress.CurrentProgressValue}/{Ids.Count} items");
            }, globalProgressOptions);
        }
        #endregion


        #region Move data
        public void MoveToFolderToSaveAll()
        {
            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(
                $"{PluginName} - {resources.GetString("LOCSsvMovingToSave")}",
                true
            );
            globalProgressOptions.IsIndeterminate = false;

            PlayniteApi.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
            {
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();

                string CancelText = string.Empty;
                activateGlobalProgress.ProgressMaxValue = Database.Items.Count;

                foreach (var item in Database.Items)
                {
                    if (activateGlobalProgress.CancelToken.IsCancellationRequested)
                    {
                        CancelText = " canceled";
                        break;
                    }

                    try
                    {
                        MoveToFolderToSaveWithNoLoader(item.Key);

                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false, true, "ScreenshotsVisualizer");
                    }
                    activateGlobalProgress.CurrentProgressValue++;
                }

                stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed;
                logger.Info($"MoveToFolderToSaveAll{CancelText} - {string.Format("{0:00}:{1:00}.{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10)}");
            }, globalProgressOptions);
        }

        public void MoveToFolderToSave(Game game)
        {
            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(
                $"{PluginName} - {resources.GetString("LOCSsvMovingToSave")}",
                false
            );
            globalProgressOptions.IsIndeterminate = true;

            PlayniteApi.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
            {
                MoveToFolderToSaveWithNoLoader(game);
            }, globalProgressOptions);
        }

        public void MoveToFolderToSave(List<Guid> Ids)
        {
            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(
                $"{PluginName} - {resources.GetString("LOCSsvMovingToSave")}",
                true
            );
            globalProgressOptions.IsIndeterminate = false;

            PlayniteApi.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
            {
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();

                string CancelText = string.Empty;
                activateGlobalProgress.ProgressMaxValue = Ids.Count;

                try
                {
                    foreach (Guid Id in Ids)
                    {
                        if (activateGlobalProgress.CancelToken.IsCancellationRequested)
                        {
                            CancelText = " canceled";
                            break;
                        }

                        MoveToFolderToSaveWithNoLoader(Id);
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, "ScreenshotsVisualizer");
                }

                stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed;
                logger.Info($"Task MoveToFolderToSave(){CancelText} - {string.Format("{0:00}:{1:00}.{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10)} for {activateGlobalProgress.CurrentProgressValue}/{Ids.Count} items");
            }, globalProgressOptions);
        }

        public void MoveToFolderToSaveWithNoLoader(Guid id)
        {
            Game game = PlayniteApi.Database.Games.Get(id);
            if (game != null)
            {
                MoveToFolderToSaveWithNoLoader(game);
            }
        }

        public void MoveToFolderToSaveWithNoLoader(Game game)
        {
            if (PluginSettings.Settings.EnableFolderToSave)
            {
                try
                {
                    if (PluginSettings.Settings.FolderToSave.IsNullOrEmpty() || PluginSettings.Settings.FileSavePattern.IsNullOrEmpty())
                    {
                        logger.Error("No settings to use folder to save");
                        PlayniteApi.Notifications.Add(new NotificationMessage(
                            $"{PluginName}-MoveToFolderToSave-Errors",
                            $"{PluginName}\r\n" + resources.GetString("LOCSsvMoveToFolderToSaveError"),
                            NotificationType.Error
                        ));
                    }
                    else
                    {
                        // Refresh data
                        GameSettings gameSettings = GetGameSettings(game.Id);
                        if (gameSettings != null)
                        {
                            SetDataFromSettings(gameSettings);
                        }

                        string PathFolder = PluginSettings.Settings.FolderToSave;
                        if (!PluginSettings.Settings.FolderToSave.Contains("{Name}"))
                        {
                            PathFolder = Path.Combine(PathFolder, "{Name}");
                        }
                        PathFolder = CommonPluginsStores.PlayniteTools.StringExpandWithStores(game, PathFolder);
                        PathFolder = CommonPluginsShared.Paths.GetSafePath(PathFolder);

                        GameScreenshots gameScreenshots = Get(game);
                        int digit = 1;

                        if (!Directory.Exists(PathFolder))
                        {
                            Directory.CreateDirectory(PathFolder);
                        }

                        bool HaveDigit = false;
                        foreach (Screenshot screenshot in gameScreenshots.Items)
                        {
                            string Pattern = CommonPluginsStores.PlayniteTools.StringExpandWithStores(game, PluginSettings.Settings.FileSavePattern);
                            string PatternWithDigit = string.Empty;

                            if (File.Exists(screenshot.FileName) && !screenshot.FileName.Contains(PathFolder, StringComparison.InvariantCultureIgnoreCase))
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
                                        destFileName += $"({DateTime.Now.ToString("yyyy-MM-dd HH_mm_ss")})";
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
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, "ScreenshotsVisualizer");
                }
            }
        }
        #endregion


        public GameSettings GetGameSettings(Guid Id)
        {
            List<FolderSettings> FolderSettingsGlobal = new List<FolderSettings>();

            if (PluginSettings.Settings.EnableFolderToSave && !PluginSettings.Settings.FolderToSave.IsNullOrEmpty())
            {
                FolderSettingsGlobal.Add(new FolderSettings
                {
                    ScreenshotsFolder = PluginSettings.Settings.FolderToSave,
                    UsedFilePattern = true,
                    FilePattern = PluginSettings.Settings.FileSavePattern
                });
            }

            if (!PluginSettings.Settings.GlobalScreenshootsPath.IsNullOrEmpty())
            {
                FolderSettingsGlobal.Add(new FolderSettings
                {
                    ScreenshotsFolder = PluginSettings.Settings.GlobalScreenshootsPath
                });
            }


            GameSettings gameSettings = PluginSettings.Settings.gameSettings.Find(x => x.Id == Id);
            if (gameSettings == null)
            {
                gameSettings = new GameSettings
                {
                    Id = Id,
                    ScreenshotsFolders = FolderSettingsGlobal
                };
            }
            else
            {
                foreach(FolderSettings folderSettings in FolderSettingsGlobal)
                {
                    var finded = gameSettings.ScreenshotsFolders
                        .Find(x => x.ScreenshotsFolder.IsEqual(folderSettings.ScreenshotsFolder) 
                                    && x.UsedFilePattern == folderSettings.UsedFilePattern
                                    && x.FilePattern.IsEqual(folderSettings.FilePattern));

                    if (finded == null)
                    {
                        gameSettings.ScreenshotsFolders.AddMissing(folderSettings);
                    }
                }
            }
            return gameSettings;
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


        public void SetDataFromSettings(GameSettings item)
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
                    string PathFolder = CommonPluginsStores.PlayniteTools.StringExpandWithStores(game, ScreenshotsFolder.ScreenshotsFolder);
                    PathFolder = CommonPluginsShared.Paths.GetSafePath(PathFolder);

                    // Get files
                    string[] extensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".jfif", ".tga", ".mp4", ".avi", ".mkv" };
                    if (Directory.Exists(PathFolder))
                    {
                        SearchOption searchOption = SearchOption.TopDirectoryOnly;
                        if (ScreenshotsFolder.ScanSubFolders)
                        {
                            searchOption = SearchOption.AllDirectories;
                        }

                        Parallel.ForEach(Directory.EnumerateFiles(PathFolder, "*.*", searchOption)
                            .Where(s => extensions.Any(ext => ext == Path.GetExtension(s))), (objectFile) =>
                            {
                                try
                                {
                                    DateTime Modified = File.GetLastWriteTime(objectFile);

                                    if (ScreenshotsFolder.UsedFilePattern)
                                    {
                                        string Pattern = CommonPluginsStores.PlayniteTools.StringExpandWithStores(game, ScreenshotsFolder.FilePattern);

                                        Pattern = Pattern.Replace("(", @"\(");
                                        Pattern = Pattern.Replace(")", @"\)");

                                        Pattern = Pattern.Replace("{digit}", @"\d*");
                                        Pattern = Pattern.Replace("{DateModified}", @"[0-9]{4}-[0-9]{2}-[0-9]{2}");
                                        Pattern = Pattern.Replace("{DateTimeModified}", @"[0-9]{4}-[0-9]{2}-[0-9]{2} [0-9]{2}_[0-9]{2}_[0-9]{2}");

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

                        Task.Run(() =>
                        {
                            // Force generation of video thumbnail
                            var VideoElements = gameScreenshots.Items.Where(x => x.IsVideo).Select(x => x.Thumbnail);
                        });
                        Thread.Sleep(500 * gameScreenshots.Items.Where(x => x.IsVideo).Count());
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
                    Common.LogError(ex, false, $"Tag insert error with {game.Name}", true, PluginName, string.Format(resources.GetString("LOCCommonNotificationTagError"), game.Name));
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
