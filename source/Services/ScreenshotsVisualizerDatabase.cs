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
using CommonPluginsShared.Extensions;
using CommonPlayniteShared.Common;

namespace ScreenshotsVisualizer.Services
{
    public class ScreenshotsVisualizerDatabase : PluginDatabaseObject<ScreenshotsVisualizerSettingsViewModel, ScreeshotsVisualizeCollection, GameScreenshots, Screenshot>
    {
        public ScreenshotsVisualizerDatabase(ScreenshotsVisualizerSettingsViewModel PluginSettings, string PluginUserDataPath) : base(PluginSettings, "ScreenshotsVisualizer", PluginUserDataPath)
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
                Database.SetGameInfo<Screenshot>();

                stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed;
                Logger.Info($"LoadDatabase with {Database.Count} items - {string.Format("{0:00}:{1:00}.{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10)}");
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginName);
                return false;
            }

            return true;
        }


        #region Refresh data
        public void RefreshDataAll()
        {
            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(
                $"{PluginName} - {ResourceProvider.GetString("LOCCommonRefreshGameData")}",
                true
            );
            globalProgressOptions.IsIndeterminate = false;

            _ = API.Instance.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
            {
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();

                string CancelText = string.Empty;
                activateGlobalProgress.ProgressMaxValue = API.Instance.Database.Games.Count;

                Database.BeginBufferUpdate();

                API.Instance.Database.Games.ForEach(x =>
                {
                    if (activateGlobalProgress.CancelToken.IsCancellationRequested)
                    {
                        CancelText = " canceled";
                        return;
                    }

                    activateGlobalProgress.Text = x.Name;

                    GameSettings gameSettings = GetGameSettings(x.Id);
                    if (gameSettings != null)
                    {
                        SetDataFromSettings(gameSettings);
                    }
                    activateGlobalProgress.CurrentProgressValue++;
                });

                Database.EndBufferUpdate();

                stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed;
                Logger.Info($"RefreshDataAll(){CancelText} - {string.Format("{0:00}:{1:00}.{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10)}");
            }, globalProgressOptions);
        }

        public void RefreshData(Game game)
        {
            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(
                $"{PluginName} - {ResourceProvider.GetString("LOCCommonRefreshGameData")}",
                false
            );
            globalProgressOptions.IsIndeterminate = true;

            _ = API.Instance.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
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
                    Common.LogError(ex, false, true, PluginName);
                }
            }, globalProgressOptions);
        }

        public void RefreshData(List<Guid> Ids)
        {
            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(
                $"{PluginName} - {ResourceProvider.GetString("LOCCommonRefreshGameData")}",
                true
            );
            globalProgressOptions.IsIndeterminate = false;

            _ = API.Instance.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
            {
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();

                string CancelText = string.Empty;
                activateGlobalProgress.ProgressMaxValue = Ids.Count;

                Database.BeginBufferUpdate();

                try
                {
                    foreach (Guid Id in Ids)
                    {
                        if (activateGlobalProgress.CancelToken.IsCancellationRequested)
                        {
                            CancelText = " canceled";
                            break;
                        }

                        activateGlobalProgress.Text = API.Instance.Database.Games.Get(Id)?.Name;

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
                    Common.LogError(ex, false, true, PluginName);
                }

                Database.EndBufferUpdate();

                stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed;
                Logger.Info($"Task RefreshData(){CancelText} - {string.Format("{0:00}:{1:00}.{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10)} for {activateGlobalProgress.CurrentProgressValue}/{Ids.Count} items");
            }, globalProgressOptions);
        }
        #endregion


        #region Move data
        public void MoveToFolderToSaveAll()
        {
            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(
                $"{PluginName} - {ResourceProvider.GetString("LOCSsvMovingToSave")}",
                true
            );
            globalProgressOptions.IsIndeterminate = false;

            _ = API.Instance.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
            {
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();

                string CancelText = string.Empty;
                activateGlobalProgress.ProgressMaxValue = Database.Items.Count;

                foreach (KeyValuePair<Guid, GameScreenshots> item in Database.Items)
                {
                    if (activateGlobalProgress.CancelToken.IsCancellationRequested)
                    {
                        CancelText = " canceled";
                        break;
                    }

                    try
                    {
                        MoveToFolderToSaveWithNoLoader(item.Key, activateGlobalProgress);
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false, true, PluginName);
                    }
                    activateGlobalProgress.CurrentProgressValue++;
                }

                stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed;
                Logger.Info($"MoveToFolderToSaveAll{CancelText} - {string.Format("{0:00}:{1:00}.{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10)}");
            }, globalProgressOptions);
        }

        public void MoveToFolderToSave(Game game)
        {
            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(
                $"{PluginName} - {ResourceProvider.GetString("LOCSsvMovingToSave")}",
                false
            );
            globalProgressOptions.IsIndeterminate = true;

            _ = API.Instance.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
            {
                MoveToFolderToSaveWithNoLoader(game);
            }, globalProgressOptions);
        }

        public void MoveToFolderToSave(List<Guid> Ids)
        {
            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(
                $"{PluginName} - {ResourceProvider.GetString("LOCSsvMovingToSave")}",
                true
            );
            globalProgressOptions.IsIndeterminate = false;

            _ = API.Instance.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
            {
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();

                string CancelText = string.Empty;
                activateGlobalProgress.ProgressMaxValue = Ids.Count;

                Database.BeginBufferUpdate();

                try
                {
                    foreach (Guid Id in Ids)
                    {
                        if (activateGlobalProgress.CancelToken.IsCancellationRequested)
                        {
                            CancelText = " canceled";
                            break;
                        }

                        MoveToFolderToSaveWithNoLoader(Id, activateGlobalProgress);
                        activateGlobalProgress.CurrentProgressValue++;
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, PluginName);
                }

                Database.EndBufferUpdate();

                stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed;
                Logger.Info($"Task MoveToFolderToSave(){CancelText} - {string.Format("{0:00}:{1:00}.{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10)} for {activateGlobalProgress.CurrentProgressValue}/{Ids.Count} items");
            }, globalProgressOptions);
        }

        public void MoveToFolderToSaveWithNoLoader(Guid id, GlobalProgressActionArgs globalProgressActionArgs)
        {
            Game game = API.Instance.Database.Games.Get(id);
            if (game != null)
            {
                globalProgressActionArgs.Text = game.Name;
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
                        Logger.Error("No settings to use folder to save");
                        API.Instance.Notifications.Add(new NotificationMessage(
                            $"{PluginName}-MoveToFolderToSave-Errors",
                            $"{PluginName}\r\n" + ResourceProvider.GetString("LOCSsvMoveToFolderToSaveError"),
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
                        PathFolder = CommonPluginsShared.Paths.GetSafePath(PathFolder, true);

                        GameScreenshots gameScreenshots = Get(game);
                        int digit = 1;

                        FileSystem.CreateDirectory(PathFolder);

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
                                        while (File.Exists(destFileName + ext))
                                        {
                                            destFileName += $"({DateTime.Now.AddSeconds(digit).ToString("yyyy-MM-dd HH_mm_ss")})";
                                            digit++;
                                        }
                                    }
                                }

                                try
                                {
                                    File.Move(screenshot.FileName, destFileName + ext);
                                }
                                catch (Exception ex)
                                {
                                    Common.LogError(ex, false, true, PluginName);
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
                    Common.LogError(ex, false, true, PluginName);
                }
            }
        }
        #endregion


        #region Convert data
        private bool ConvertToJpg(Screenshot screenshot)
        {
            try
            {
                if (!screenshot.IsVideo)
                {
                    string oldFile = screenshot.FileName;
                    string newFile = ImageTools.ConvertToJpg(oldFile, PluginSettings.Settings.JpgQuality);

                    if (!newFile.IsNullOrEmpty())
                    {
                        DateTime dt = File.GetLastWriteTime(oldFile);
                        File.SetLastWriteTime(newFile, dt);
                        FileSystem.DeleteFileSafe(oldFile);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, false, PluginName);
            }

            return false;
        }

        private bool ConvertGameSsvToJpg(Guid id)
        {
            return ConvertGameSsvToJpg(API.Instance.Database.Games.Get(id));
        }

        public void ConvertGameSsvToJpg(List<Guid> ids)
        {
            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(
                $"{PluginName} - {ResourceProvider.GetString("LOCCommonConverting")}",
                true
            );
            globalProgressOptions.IsIndeterminate = ids.Count == 1;

            _ = API.Instance.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
            {
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();

                string CancelText = string.Empty;
                activateGlobalProgress.IsIndeterminate = true;
                if (ids.Count > 1)
                {
                    activateGlobalProgress.IsIndeterminate = false;
                    activateGlobalProgress.ProgressMaxValue = ids.Count;
                }

                Database.BeginBufferUpdate();

                try
                {
                    ids.ForEach(y =>
                    {
                        if (activateGlobalProgress.CancelToken.IsCancellationRequested)
                        {
                            CancelText = " canceled";
                            return;
                        }

                        activateGlobalProgress.Text = API.Instance.Database.Games.Get(y)?.Name;
                        if (ConvertGameSsvToJpg(y))
                        {
                            GameSettings gameSettings = GetGameSettings(y);
                            if (gameSettings != null)
                            {
                                SetDataFromSettings(gameSettings);
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, PluginName);
                }

                Database.EndBufferUpdate();

                stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed;
                Logger.Info($"Task ConvertGameSsvToJpg(){CancelText} - {string.Format("{0:00}:{1:00}.{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10)} for {ids.Count} items");
            }, globalProgressOptions);
        }

        public bool ConvertGameSsvToJpg(Game game)
        {
            bool hasConverted = false;
            if (game != null)
            {
                GameScreenshots data = Get(game);
                if (data.HasData)
                {
                    data.Items.ForEach(x =>
                    {
                        if (ConvertToJpg(x))
                        {
                            hasConverted = true;
                        }
                    });
                }
            }
            return hasConverted;
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
                foreach (FolderSettings folderSettings in FolderSettingsGlobal)
                {
                    FolderSettings finded = gameSettings.ScreenshotsFolders
                        .Find(x => x.ScreenshotsFolder.IsEqual(folderSettings.ScreenshotsFolder)
                                    && x.UsedFilePattern == folderSettings.UsedFilePattern
                                    && x.FilePattern.IsEqual(folderSettings.FilePattern));

                    if (finded == null)
                    {
                        _ = gameSettings.ScreenshotsFolders.AddMissing(folderSettings);
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
                Game game = API.Instance.Database.Games.Get(Id);
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
            _ = SpinWait.SpinUntil(() => API.Instance.Database.IsOpen, -1);

            Game game = API.Instance.Database.Games.Get(item.Id);
            if (game == null)
            {
                Logger.Warn($"Game not found for {item.Id}");
                return;
            }

            GameScreenshots gameScreenshots = GetDefault(game);
            try
            {
                gameScreenshots.ScreenshotsFolders = item.GetScreenshotsFolders();
                gameScreenshots.InSettings = true;

                foreach (FolderSettings ScreenshotsFolder in item.ScreenshotsFolders)
                {
                    try
                    {
                        if (ScreenshotsFolder?.ScreenshotsFolder == null || ScreenshotsFolder.ScreenshotsFolder.IsNullOrEmpty())
                        {
                            Logger.Warn($"Screenshots directory is empty for {game.Name}");
                            return;
                        }

                        string PathFolder = CommonPluginsStores.PlayniteTools.StringExpandWithStores(game, ScreenshotsFolder.ScreenshotsFolder);
                        PathFolder = CommonPluginsShared.Paths.GetSafePath(PathFolder, true);

                        // Get files
                        string[] extensions = { ".jpg", ".jpeg", ".webp", ".png", ".gif", ".bmp", ".jfif", ".tga", ".mp4", ".avi", ".mkv", ".webm" };
                        if (Directory.Exists(PathFolder))
                        {
                            SearchOption searchOption = SearchOption.TopDirectoryOnly;
                            if (ScreenshotsFolder.ScanSubFolders)
                            {
                                searchOption = SearchOption.AllDirectories;
                            }

                            Directory.EnumerateFiles(PathFolder, "*.*", searchOption)
                                .Where(s => extensions.Any(ext => ext == Path.GetExtension(s)))
                                .ForEach(objectFile =>
                                {
                                    try
                                    {
                                        DateTime Modified = File.GetLastWriteTime(objectFile);

                                        if (ScreenshotsFolder.UsedFilePattern)
                                        {
                                            string Pattern = CommonPluginsStores.PlayniteTools.StringExpandWithStores(game, ScreenshotsFolder.FilePattern);

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
                                        Common.LogError(ex, false, true, PluginName);
                                    }
                                });
                        }
                        else
                        {
                            Logger.Warn($"Screenshots directory not found for {game.Name} - {PathFolder}");
                        }

                        IEnumerable<Screenshot> elements = gameScreenshots?.Items?.Where(x => x != null);
                        if (elements?.Count() > 0)
                        {
                            elements = elements?.GroupBy(x => x.FileName)?.Select(g => g.First());

                            gameScreenshots.DateLastRefresh = DateTime.Now;
                            gameScreenshots.Items = elements.ToList();

                            // Force generation of data from video
                            gameScreenshots.Items.Where(x => x.IsVideo).ForEach(x =>
                            {
                                string thumb = x.Thumbnail;
                                string duration = x.DurationString;
                                string size = x.SizeString;
                            });
                        }

                        AddOrUpdate(gameScreenshots);
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false, $"Error on {game.Name} for {ScreenshotsFolder.ScreenshotsFolder}", true, PluginName);
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false);
            }
        }


        #region Tag
        public override void AddTag(Game game)
        {
            GameScreenshots item = Get(game, true);
            if (item.HasData)
            {
                try
                {
                    Guid? TagId = FindGoodPluginTags(ResourceProvider.GetString("LOCSsvTitle"));
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
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, $"Tag insert error with {game.Name}", true, PluginName, string.Format(ResourceProvider.GetString("LOCCommonNotificationTagError"), game.Name));
                    return;
                }
            }
            else if (TagMissing)
            {
                if (game.TagIds != null)
                {
                    game.TagIds.Add((Guid)AddNoDataTag());
                }
                else
                {
                    game.TagIds = new List<Guid> { (Guid)AddNoDataTag() };
                }
            }

            API.Instance.MainView.UIDispatcher?.Invoke(() =>
            {
                API.Instance.Database.Games.Update(game);
                game.OnPropertyChanged();
            });
        }
        #endregion


        public override void SetThemesResources(Game game)
        {
            GameScreenshots gameScreenshots = Get(game, true);
            PluginSettings.Settings.HasData = gameScreenshots?.HasData ?? false;
            PluginSettings.Settings.ListScreenshots = gameScreenshots?.Items ?? new List<Screenshot>();
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

                Screenshot screenshot = (Screenshot)listBox.Items[index];

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
                        CanBeResizable = true,
                        Height = 720,
                        Width = 1280
                    };

                    SsvSinglePictureView ViewExtension = new SsvSinglePictureView(screenshot, listBox.Items.Cast<Screenshot>().ToList());
                    Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(ResourceProvider.GetString("LOCSsv") + " - " + screenshot.FileNameOnly, ViewExtension, windowOptions);
                    _ = windowExtension.ShowDialog();
                }
            }
        }
    }
}
