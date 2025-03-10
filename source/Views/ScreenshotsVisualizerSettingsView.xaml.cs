﻿using CommonPlayniteShared.Common;
using CommonPluginsControls.Controls;
using CommonPluginsShared;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ScreenshotsVisualizer.Views
{
    public partial class ScreenshotsVisualizerSettingsView : UserControl
    {
        private static ILogger Logger => LogManager.GetLogger();

        private ScreenshotsVisualizerDatabase PluginDatabase => ScreenshotsVisualizer.PluginDatabase;

        public static List<ListGameScreenshot> ListGameScreenshots { get; set; } = new List<ListGameScreenshot>();
        private List<ListGame> ListGames { get; set; } = new List<ListGame>();

        private bool UserFilter(object item)
        {
            if (string.IsNullOrEmpty(TextboxSearch.Text))
            {
                return true;
            }
            else
            {
                if (item is ListGame lg)
                {
                    return lg.Name.IndexOf(TextboxSearch.Text, StringComparison.OrdinalIgnoreCase) >= 0;
                }

                if (item is ListGameScreenshot lgs)
                {
                    return lgs.Name.IndexOf(TextboxSearch.Text, StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }

            return true;
        }


        public ScreenshotsVisualizerSettingsView()
        {
            InitializeComponent();

            Task TaskView = Task.Run(() =>
            {
                LoadData();

                _ = Application.Current.Dispatcher?.BeginInvoke((Action)delegate
                {
                    PART_ListGame.ItemsSource = ListGames;
                    PART_ListGameScreenshot.ItemsSource = ListGameScreenshots;

                    CollectionView viewGame = (CollectionView)CollectionViewSource.GetDefaultView(PART_ListGame.ItemsSource);
                    viewGame.Filter = UserFilter;

                    CollectionView viewGameScreenshot = (CollectionView)CollectionViewSource.GetDefaultView(PART_ListGameScreenshot.ItemsSource);
                    viewGameScreenshot.Filter = UserFilter;
                });
            });
        }


        private void LoadData()
        {
            ListGameScreenshots = new List<ListGameScreenshot>();
            foreach (GameSettings item in ScreenshotsVisualizer.PluginDatabase.PluginSettings.Settings.gameSettings)
            {
                Game game = API.Instance.Database.Games.Get(item.Id);

                if (game != null)
                {
                    string Icon = string.Empty;
                    if (!game.Icon.IsNullOrEmpty())
                    {
                        Icon = API.Instance.Database.GetFullFilePath(game.Icon);
                    }

                    // TEMP
                    List<FolderSettings> ScreenshotsFolders = new List<FolderSettings>();
                    if (!item.ScreenshotsFolder.IsNullOrEmpty())
                    {
                        ScreenshotsFolders.Add(new FolderSettings
                        {
                            UsedFilePattern = item.UsedFilePattern,
                            FilePattern = item.FilePattern,
                            ScreenshotsFolder = item.ScreenshotsFolder,
                            ScanSubFolders = item.ScanSubFolders
                        });
                    }
                    else
                    {
                        ScreenshotsFolders = item.ScreenshotsFolders;
                    }

                    ListGameScreenshots.Add(new ListGameScreenshot
                    {
                        Id = item.Id,
                        Icon = Icon,
                        Name = game.Name,
                        ScreenshotsFolders = ScreenshotsFolders,
                        UsedFilePattern = item.UsedFilePattern,
                        ScanSubFolders = item.ScanSubFolders,
                        FilePattern = item.FilePattern,
                        SourceName = PlayniteTools.GetSourceName(item.Id),
                        SourceIcon = TransformIcon.Get(PlayniteTools.GetSourceName(item.Id))
                    });
                }
                else
                {
                    Logger.Warn($"Game is deleted - {item.Id}");
                }
            }

            IEnumerable<Game> DbWithoutAlready = API.Instance.Database.Games.Where(x => !ListGameScreenshots.Any(y => x.Id == y.Id));
            ListGames = new List<ListGame>();
            foreach (Game item in DbWithoutAlready)
            {
                string Icon = string.Empty;
                if (!item.Icon.IsNullOrEmpty())
                {
                    Icon = API.Instance.Database.GetFullFilePath(item.Icon);
                }

                ListGames.Add(new ListGame
                {
                    Id = item.Id,
                    Icon = Icon,
                    Name = item.Name,
                    SourceName = PlayniteTools.GetSourceName(item.Id),
                    SourceIcon = TransformIcon.Get(PlayniteTools.GetSourceName(item.Id))
                });
            }

            ListGames.Sort((x, y) => x.Name.CompareTo(y.Name));
            ListGameScreenshots.Sort((x, y) => x.Name.CompareTo(y.Name));
        }


        #region Action on list games screenshots
        private void ButtonSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            Guid Id = Guid.Parse(UI.FindParent<ItemsControl>((Button)sender).Tag.ToString());
            int indexFolder = int.Parse(((Button)sender).Tag.ToString());

            ListGameScreenshot item = ((List<ListGameScreenshot>)PART_ListGameScreenshot.ItemsSource).Find(x => x.Id == Id);
            int ControlIndex = ListGameScreenshots.FindIndex(x => x == item);

            string SelectedFolder = API.Instance.Dialogs.SelectFolder();
            if (!SelectedFolder.IsNullOrEmpty())
            {
                TextBox TextBox = UI.FindVisualChildren<TextBox>(((FrameworkElement)((FrameworkElement)sender).Parent).Parent).FirstOrDefault();

                if (TextBox != null)
                {
                    TextBox.Text = SelectedFolder;
                    ListGameScreenshots[ControlIndex].ScreenshotsFolders[indexFolder].ScreenshotsFolder = SelectedFolder;
                }
            }

            PART_ListGameScreenshot.ItemsSource = null;
            PART_ListGameScreenshot.ItemsSource = ListGameScreenshots;
            TextboxSearch_TextChanged(null, null);
        }

        private void ButtonRemove_Click(object sender, RoutedEventArgs e)
        {
            Guid Id = Guid.Parse(((Button)sender).Tag.ToString());

            ListGameScreenshot item = ((List<ListGameScreenshot>)PART_ListGameScreenshot.ItemsSource).Find(x => x.Id == Id);
            PART_ListGameScreenshot.ItemsSource = null;
            _ = ListGameScreenshots.Remove(item);
            PART_ListGameScreenshot.ItemsSource = ListGameScreenshots;
            TextboxSearch_TextChanged(null, null);

            Task TaskView = Task.Run(() =>
            {
                IEnumerable<Game> DbWithoutAlready = API.Instance.Database.Games.Where(x => !ListGameScreenshots.Any(y => x.Id == y.Id));
                ListGames = new List<ListGame>();
                foreach (Game game in DbWithoutAlready)
                {
                    string Icon = string.Empty;
                    if (!game.Icon.IsNullOrEmpty())
                    {
                        Icon = API.Instance.Database.GetFullFilePath(game.Icon);
                    }

                    ListGames.Add(new ListGame
                    {
                        Id = game.Id,
                        Icon = Icon,
                        Name = game.Name,
                        SourceName = PlayniteTools.GetSourceName(game.Id)
                    });
                }

                Application.Current.Dispatcher.BeginInvoke((Action)delegate
                {
                    ListGames.Sort((x, y) => x.Name.CompareTo(y.Name));
                    PART_ListGame.ItemsSource = null;
                    PART_ListGame.ItemsSource = ListGames;

                    PART_ListGameScreenshot.ItemsSource = null;
                    PART_ListGameScreenshot.ItemsSource = ListGameScreenshots;
                });
            });
        }

        private void ButtonRemoveFolder_Click(object sender, RoutedEventArgs e)
        {
            Guid Id = Guid.Parse(UI.FindParent<ItemsControl>((Button)sender).Tag.ToString());
            int indexFolder = int.Parse(((Button)sender).Tag.ToString());

            ListGameScreenshot item = ((List<ListGameScreenshot>)PART_ListGameScreenshot.ItemsSource).Find(x => x.Id == Id);
            int ControlIndex = ListGameScreenshots.FindIndex(x => x == item);

            PART_ListGameScreenshot.ItemsSource = null;
            ListGameScreenshots[ControlIndex].ScreenshotsFolders.RemoveAt(indexFolder);
            PART_ListGameScreenshot.ItemsSource = ListGameScreenshots;
            TextboxSearch_TextChanged(null, null);
        }

        private void ButtonAddFolder_Click(object sender, RoutedEventArgs e)
        {
            Guid Id = Guid.Parse(((Button)sender).Tag.ToString());

            ListGameScreenshot item = ((List<ListGameScreenshot>)PART_ListGameScreenshot.ItemsSource).Find(x => x.Id == Id);
            int ControlIndex = ListGameScreenshots.FindIndex(x => x == item);

            PART_ListGameScreenshot.ItemsSource = null;
            ListGameScreenshots[ControlIndex].ScreenshotsFolders.Add(new FolderSettings());
            PART_ListGameScreenshot.ItemsSource = ListGameScreenshots;
            TextboxSearch_TextChanged(null, null);
        }


        private void PART_BtToDigit_Click(object sender, RoutedEventArgs e)
        {
            Guid Id = Guid.Parse(UI.FindParent<ItemsControl>((Button)sender).Tag.ToString());
            int indexFolder = int.Parse(((Button)sender).Tag.ToString());

            ListGameScreenshot item = ((List<ListGameScreenshot>)PART_ListGameScreenshot.ItemsSource).Find(x => x.Id == Id);
            int ControlIndex = ListGameScreenshots.FindIndex(x => x == item);

            if (ListGameScreenshots[ControlIndex].ScreenshotsFolders[indexFolder]?.FilePattern == null)
            {
                return;
            }

            PART_ListGameScreenshot.ItemsSource = null;
            ListGameScreenshots[ControlIndex].ScreenshotsFolders[indexFolder].FilePattern = Regex.Replace(ListGameScreenshots[ControlIndex].ScreenshotsFolders[indexFolder].FilePattern, @"\d+", "{digit}");
            PART_ListGameScreenshot.ItemsSource = ListGameScreenshots;
            TextboxSearch_TextChanged(null, null);
        }
        #endregion


        #region Action on list games
        private void PART_ListGame_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PART_BtSelectGame.IsEnabled = true;
        }

        private void PART_BtSelectGame_Click(object sender, RoutedEventArgs e)
        {
            int index = PART_ListGame.SelectedIndex;
            if (index < 0)
            {
                return;
            }

            ListGame SelectedItem = (ListGame)PART_ListGame.SelectedItem;

            PART_ListGame.ItemsSource = null;
            _ = ListGames.Remove(SelectedItem);
            PART_ListGame.ItemsSource = ListGames;


            string Icon = string.Empty;
            if (!SelectedItem.Icon.IsNullOrEmpty())
            {
                Icon = API.Instance.Database.GetFullFilePath(SelectedItem.Icon);
            }

            ListGameScreenshots.Add(new ListGameScreenshot
            {
                Id = SelectedItem.Id,
                Icon = Icon,
                Name = SelectedItem.Name,
                ScreenshotsFolders = new List<FolderSettings>(),
                UsedFilePattern = false,
                FilePattern = string.Empty,
                SourceName = SelectedItem.SourceName,
                SourceIcon = TransformIcon.Get(SelectedItem.SourceName)
            });

            ListGameScreenshots.Sort((x, y) => x.Name.CompareTo(y.Name));
            PART_ListGameScreenshot.ItemsSource = null;
            PART_ListGameScreenshot.ItemsSource = ListGameScreenshots;

            TextboxSearch_TextChanged(null, null);
        }
        #endregion


        // Search by name
        private void TextboxSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            CollectionViewSource.GetDefaultView(PART_ListGame.ItemsSource).Refresh();
            CollectionViewSource.GetDefaultView(PART_ListGameScreenshot.ItemsSource).Refresh();
        }


        // Add Steam game automaticly
        private void PART_BtAddSteamGame_Click(object sender, RoutedEventArgs e)
        {
            TextboxSearch.Text = string.Empty;

            List<ListGame> tmpList = Serialization.GetClone(ListGames).Where(x => x.SourceName == "Steam").ToList();
            foreach (ListGame game in tmpList)
            {
                int index = ListGames.FindIndex(x => x.Id == game.Id);
                ListGames.RemoveAt(index);

                string Icon = string.Empty;
                if (!game.Icon.IsNullOrEmpty())
                {
                    Icon = API.Instance.Database.GetFullFilePath(game.Icon);
                }

                List<FolderSettings> ScreenshotsFolders = new List<FolderSettings>
                {
                    new FolderSettings
                    {
                        ScreenshotsFolder = "{SteamScreenshotsDir}\\" + API.Instance.Database.Games.Get(game.Id).GameId + "\\screenshots"
                    }
                };

                ListGameScreenshots.Add(new ListGameScreenshot
                {
                    Id = game.Id,
                    Icon = Icon,
                    Name = game.Name,
                    ScreenshotsFolders = ScreenshotsFolders,
                    SourceName = game.SourceName,
                    SourceIcon = TransformIcon.Get(game.SourceName)
                });
            }

            PART_ListGame.ItemsSource = null;
            PART_ListGame.ItemsSource = ListGames;

            ListGameScreenshots.Sort((x, y) => x.Name.CompareTo(y.Name));
            PART_ListGameScreenshot.ItemsSource = null;
            PART_ListGameScreenshot.ItemsSource = ListGameScreenshots;

            TextboxSearch_TextChanged(null, null);
        }

        // Add Ubisoft Connect game automaticly
        private void PART_BtAddUplay_Click(object sender, RoutedEventArgs e)
        {
            TextboxSearch.Text = string.Empty;

            List<ListGame> tmpList = Serialization.GetClone(ListGames).Where(x => x.SourceName.ToLower() == "ubisoft connect" || x.SourceName.ToLower() == "uplay").ToList();
            foreach (ListGame game in tmpList)
            {
                int index = ListGames.FindIndex(x => x.Id == game.Id);
                ListGames.RemoveAt(index);

                string Icon = string.Empty;
                if (!game.Icon.IsNullOrEmpty())
                {
                    Icon = API.Instance.Database.GetFullFilePath(game.Icon);
                }

                List<FolderSettings> ScreenshotsFolders = new List<FolderSettings>();
                ScreenshotsFolders.Add(new FolderSettings
                {
                    ScreenshotsFolder = "{UbisoftScreenshotsDir}\\" + game.Name
                });

                ListGameScreenshots.Add(new ListGameScreenshot
                {
                    Id = game.Id,
                    Icon = Icon,
                    Name = game.Name,
                    ScreenshotsFolders = ScreenshotsFolders,
                    SourceName = game.SourceName,
                    SourceIcon = TransformIcon.Get(game.SourceName)
                });
            }

            PART_ListGame.ItemsSource = null;
            PART_ListGame.ItemsSource = ListGames;

            ListGameScreenshots.Sort((x, y) => x.Name.CompareTo(y.Name));
            PART_ListGameScreenshot.ItemsSource = null;
            PART_ListGameScreenshot.ItemsSource = ListGameScreenshots;

            TextboxSearch_TextChanged(null, null);
        }

        // Add RetroArch game automaticly
        private void PART_BtAddURetroArch_Click(object sender, RoutedEventArgs e)
        {
            TextboxSearch.Text = string.Empty;

            List<ListGame> tmpList = Serialization.GetClone(ListGames).Where(x => PlayniteTools.GameUseRetroArch(API.Instance.Database.Games.Get(x.Id))).ToList();
            foreach (ListGame game in tmpList)
            {
                int index = ListGames.FindIndex(x => x.Id == game.Id);
                ListGames.RemoveAt(index);

                string Icon = string.Empty;
                if (!game.Icon.IsNullOrEmpty())
                {
                    Icon = API.Instance.Database.GetFullFilePath(game.Icon);
                }

                List<FolderSettings> ScreenshotsFolders = new List<FolderSettings>
                {
                    new FolderSettings
                    {
                        ScreenshotsFolder = "{RetroArchScreenshotsDir}",
                        UsedFilePattern = true,
                        FilePattern = "{ImageNameNoExt}-{digit}-{digit}",
                    }
                };

                ListGameScreenshots.Add(new ListGameScreenshot
                {
                    Id = game.Id,
                    Icon = Icon,
                    Name = game.Name,
                    ScreenshotsFolders = ScreenshotsFolders,
                    SourceName = game.SourceName,
                    SourceIcon = TransformIcon.Get(game.SourceName)
                });
            }

            PART_ListGame.ItemsSource = null;
            PART_ListGame.ItemsSource = ListGames;

            ListGameScreenshots.Sort((x, y) => x.Name.CompareTo(y.Name));
            PART_ListGameScreenshot.ItemsSource = null;
            PART_ListGameScreenshot.ItemsSource = ListGameScreenshots;

            TextboxSearch_TextChanged(null, null);
        }

        // Add ScummVM game automaticly
        private void PART_BtAddURetroScummVM_Click(object sender, RoutedEventArgs e)
        {
            TextboxSearch.Text = string.Empty;

            List<ListGame> tmpList = Serialization.GetClone(ListGames).Where(x => PlayniteTools.GameUseScummVM(API.Instance.Database.Games.Get(x.Id))).ToList();
            foreach (ListGame game in tmpList)
            {
                int index = ListGames.FindIndex(x => x.Id == game.Id);
                ListGames.RemoveAt(index);

                string Icon = string.Empty;
                if (!game.Icon.IsNullOrEmpty())
                {
                    Icon = API.Instance.Database.GetFullFilePath(game.Icon);
                }

                List<FolderSettings> ScreenshotsFolders = new List<FolderSettings>
                {
                    new FolderSettings
                    {
                        ScreenshotsFolder = "{UserProfile}\\Pictures\\ScummVM Screenshots",
                        UsedFilePattern = true,
                        FilePattern = "scummvm-{ImageNameNoExt}-{digit}",
                    }
                };

                ListGameScreenshots.Add(new ListGameScreenshot
                {
                    Id = game.Id,
                    Icon = Icon,
                    Name = game.Name,
                    ScreenshotsFolders = ScreenshotsFolders,
                    SourceName = game.SourceName,
                    SourceIcon = TransformIcon.Get(game.SourceName)
                });
            }

            PART_ListGame.ItemsSource = null;
            PART_ListGame.ItemsSource = ListGames;

            ListGameScreenshots.Sort((x, y) => x.Name.CompareTo(y.Name));
            PART_ListGameScreenshot.ItemsSource = null;
            PART_ListGameScreenshot.ItemsSource = ListGameScreenshots;

            TextboxSearch_TextChanged(null, null);
        }


        #region FolderToSave
        private void ButtonSelectFolderToSave_Click(object sender, RoutedEventArgs e)
        {
            string SelectedFolder = API.Instance.Dialogs.SelectFolder();
            if (!SelectedFolder.IsNullOrEmpty())
            {
                PART_FolderToSave.Text = SelectedFolder;
                PluginDatabase.PluginSettings.Settings.FolderToSave = SelectedFolder;
            }
        }
        #endregion


        private void PART_BtAddGame_Click(object sender, RoutedEventArgs e)
        {
            TextboxSearch_TextChanged(null, null);
        }


        private void PART_BtRelative_Click(object sender, RoutedEventArgs e)
        {
            TextboxSearch.Text = string.Empty;

            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(
                PluginDatabase.PluginName,
                true
            );
            globalProgressOptions.IsIndeterminate = false;

            _ = API.Instance.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
            {
                try
                {
                    activateGlobalProgress.ProgressMaxValue = ListGameScreenshots.Count;

                    foreach (ListGameScreenshot game in ListGameScreenshots)
                    {
                        if (activateGlobalProgress.CancelToken.IsCancellationRequested)
                        {
                            break;
                        }

                        foreach (FolderSettings screenshotsFolders in game.ScreenshotsFolders)
                        {
                            screenshotsFolders.ScreenshotsFolder = CommonPluginsStores.PlayniteTools.PathToRelativeWithStores
                            (
                                API.Instance.Database.Games.Get(game.Id),
                                screenshotsFolders.ScreenshotsFolder
                            );
                        }

                        activateGlobalProgress.CurrentProgressValue++;
                    }

                    _ = Application.Current.Dispatcher?.BeginInvoke((Action)delegate
                    {
                        ListGameScreenshots.Sort((x, y) => x.Name.CompareTo(y.Name));
                        PART_ListGameScreenshot.ItemsSource = null;
                        PART_ListGameScreenshot.ItemsSource = ListGameScreenshots;

                        TextboxSearch_TextChanged(null, null);
                    });
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, PluginDatabase.PluginName);
                }
            }, globalProgressOptions);
        }

        private void PART_BtAbsolute_Click(object sender, RoutedEventArgs e)
        {
            TextboxSearch.Text = string.Empty;

            GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(
                PluginDatabase.PluginName,
                true
            );
            globalProgressOptions.IsIndeterminate = false;

            _ = API.Instance.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
            {
                try
                {
                    activateGlobalProgress.ProgressMaxValue = ListGameScreenshots.Count;

                    foreach (ListGameScreenshot game in ListGameScreenshots)
                    {
                        if (activateGlobalProgress.CancelToken.IsCancellationRequested)
                        {
                            break;
                        }

                        foreach (FolderSettings screenshotsFolders in game.ScreenshotsFolders)
                        {
                            screenshotsFolders.ScreenshotsFolder = CommonPluginsStores.PlayniteTools.StringExpandWithStores
                            (
                                API.Instance.Database.Games.Get(game.Id),
                                screenshotsFolders.ScreenshotsFolder
                            );
                        }

                        activateGlobalProgress.CurrentProgressValue++;
                    }

                    _ = Application.Current.Dispatcher?.BeginInvoke((Action)delegate
                    {
                        ListGameScreenshots.Sort((x, y) => x.Name.CompareTo(y.Name));
                        PART_ListGameScreenshot.ItemsSource = null;
                        PART_ListGameScreenshot.ItemsSource = ListGameScreenshots;

                        TextboxSearch_TextChanged(null, null);
                    });
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, PluginDatabase.PluginName);
                }
            }, globalProgressOptions);
        }


        private void PART_SelectVariables_Click(object sender, RoutedEventArgs e)
        {
            SelectVariable ViewExtension = new SelectVariable();
            Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(ResourceProvider.GetString("LOCCommonSelectVariable"), ViewExtension);
            windowExtension.ResizeMode = ResizeMode.CanResize;
            _ = windowExtension.ShowDialog();
        }

        private void ButtonClear_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string PathThumbnail = Path.Combine(PluginDatabase.Paths.PluginCachePath, "Thumbnails");
                FileSystem.DeleteDirectory(PathThumbnail);
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }
        }
    }


    public class ListGame
    {
        public Guid Id { get; set; }
        public string Icon { get; set; }
        public string Name { get; set; }
        public string SourceName { get; set; }
        public string SourceIcon { get; set; }
        public bool IsVisible { get; set; } = true;
    }

    public class ListGameScreenshot
    {
        public Guid Id { get; set; }
        public string Icon { get; set; }
        public string Name { get; set; }
        public string SourceName { get; set; }
        public string SourceIcon { get; set; }
        public bool UsedFilePattern { get; set; }
        public bool ScanSubFolders { get; set; }
        public string FilePattern { get; set; }
        public List<FolderSettings> ScreenshotsFolders { get; set; }
        public bool IsVisible { get; set; } = true;
    }
}
