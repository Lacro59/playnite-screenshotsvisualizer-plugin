using CommonPluginsShared;
using Playnite.SDK;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ScreenshotsVisualizer.Views
{
    public partial class ScreenshotsVisualizerSettingsView : UserControl
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private IPlayniteAPI _PlayniteApi;

        private string _PluginUserDataPath;

        public static List<ListGameScreenshot> listGameScreenshots = new List<ListGameScreenshot>();
        public static List<ListGame> listGames = new List<ListGame>();


        public ScreenshotsVisualizerSettingsView(IPlayniteAPI PlayniteApi, string PluginUserDataPath)
        {
            _PlayniteApi = PlayniteApi;
            _PluginUserDataPath = PluginUserDataPath;

            InitializeComponent();


            var TaskView = Task.Run(() =>
            {
                LoadData(PlayniteApi);

                this.Dispatcher.BeginInvoke((Action)delegate 
                {
                    PART_ListGame.ItemsSource = listGames;
                    PART_ListGameScreenshot.ItemsSource = listGameScreenshots;
                });
            });
        }


        private void LoadData(IPlayniteAPI PlayniteApi)
        {
            listGameScreenshots = new List<ListGameScreenshot>();
            foreach (var item in ScreenshotsVisualizer.PluginDatabase.PluginSettings.Settings.gameSettings)
            {
                Game game = PlayniteApi.Database.Games.Get(item.Id);

                if (game != null)
                {
                    string Icon = string.Empty;
                    if (!game.Icon.IsNullOrEmpty())
                    {
                        Icon = PlayniteApi.Database.GetFullFilePath(game.Icon);
                    }

                    // TEMP
                    List<FolderSettings> ScreenshotsFolders = new List<FolderSettings>();
                    if (!item.ScreenshotsFolder.IsNullOrEmpty())
                    {
                        ScreenshotsFolders.Add(new FolderSettings
                        {
                            UsedFilePattern = item.UsedFilePattern,
                            FilePattern = item.FilePattern,
                            ScreenshotsFolder = item.ScreenshotsFolder
                        });
                    }
                    else
                    {
                        ScreenshotsFolders = item.ScreenshotsFolders;
                    }

                    listGameScreenshots.Add(new ListGameScreenshot
                    {
                        Id = item.Id,
                        Icon = Icon,
                        Name = game.Name,
                        ScreenshotsFolders = ScreenshotsFolders,
                        UsedFilePattern = item.UsedFilePattern,
                        FilePattern = item.FilePattern,
                        SourceName = PlayniteTools.GetSourceName(PlayniteApi, item.Id),
                        SourceIcon = TransformIcon.Get(PlayniteTools.GetSourceName(PlayniteApi, item.Id))
                    });
                }
                else
                {
                    logger.Warn($"Game is deleted - {item.Id}");
                }
            }

            var DbWithoutAlready = PlayniteApi.Database.Games.Where(x => !listGameScreenshots.Any(y => x.Id == y.Id));
            listGames = new List<ListGame>();
            foreach (Game item in DbWithoutAlready)
            {
                string Icon = string.Empty;
                if (!item.Icon.IsNullOrEmpty())
                {
                    Icon = PlayniteApi.Database.GetFullFilePath(item.Icon);
                }

                listGames.Add(new ListGame
                {
                    Id = item.Id,
                    Icon = Icon,
                    Name = item.Name,
                    SourceName = PlayniteTools.GetSourceName(PlayniteApi, item.Id),
                    SourceIcon = TransformIcon.Get(PlayniteTools.GetSourceName(PlayniteApi, item.Id))
                });
            }

            listGames.Sort((x, y) => x.Name.CompareTo(y.Name));
            listGameScreenshots.Sort((x, y) => x.Name.CompareTo(y.Name));
        }


        #region Action on list games screenshots
        private void ButtonSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            int index = int.Parse(UI.FindParent<ItemsControl>((Button)sender).Tag.ToString());
            int indexFolder = int.Parse(((Button)sender).Tag.ToString());

            var item = ((List<ListGameScreenshot>)PART_ListGameScreenshot.ItemsSource)[index];
            int ControlIndex = listGameScreenshots.FindIndex(x => x == item);

            string SelectedFolder = _PlayniteApi.Dialogs.SelectFolder();
            if (!SelectedFolder.IsNullOrEmpty())
            {
                var TextBox = UI.FindVisualChildren<TextBox>(((FrameworkElement)((FrameworkElement)sender).Parent).Parent).FirstOrDefault();

                if (TextBox != null)
                {
                    TextBox.Text = SelectedFolder;
                    listGameScreenshots[ControlIndex].ScreenshotsFolders[indexFolder].ScreenshotsFolder = SelectedFolder;
                }
            }

            PART_ListGameScreenshot.ItemsSource = null;
            PART_ListGameScreenshot.ItemsSource = listGameScreenshots;
            TextboxSearch_TextChanged(null, null);
        }

        private void ButtonRemove_Click(object sender, RoutedEventArgs e)
        {
            int index = int.Parse(((Button)sender).Tag.ToString());

            var item = ((List<ListGameScreenshot>)PART_ListGameScreenshot.ItemsSource)[index];
            int ControlIndex = listGameScreenshots.FindIndex(x => x == item);

            PART_ListGameScreenshot.ItemsSource = null;
            listGameScreenshots.RemoveAt(ControlIndex);
            PART_ListGameScreenshot.ItemsSource = listGameScreenshots;
            TextboxSearch_TextChanged(null, null);

            var TaskView = Task.Run(() =>
            {
                var DbWithoutAlready = _PlayniteApi.Database.Games.Where(x => !listGameScreenshots.Any(y => x.Id == y.Id));
                listGames = new List<ListGame>();
                foreach (Game game in DbWithoutAlready)
                {
                    string Icon = string.Empty;
                    if (!game.Icon.IsNullOrEmpty())
                    {
                        Icon = _PlayniteApi.Database.GetFullFilePath(game.Icon);
                    }

                    listGames.Add(new ListGame
                    {
                        Id = game.Id,
                        Icon = Icon,
                        Name = game.Name,
                        SourceName = PlayniteTools.GetSourceName(_PlayniteApi, game.Id)
                    });
                }
                
                this.Dispatcher.BeginInvoke((Action)delegate
                {
                    listGames.Sort((x, y) => x.Name.CompareTo(y.Name));
                    PART_ListGame.ItemsSource = null;
                    PART_ListGame.ItemsSource = listGames;

                    PART_ListGameScreenshot.ItemsSource = null;
                    PART_ListGameScreenshot.ItemsSource = listGameScreenshots;
                });
            });
        }

        private void ButtonRemoveFolder_Click(object sender, RoutedEventArgs e)
        {
            int index = int.Parse(UI.FindParent<ItemsControl>((Button)sender).Tag.ToString());
            int indexFolder = int.Parse(((Button)sender).Tag.ToString());

            var item = ((List<ListGameScreenshot>)PART_ListGameScreenshot.ItemsSource)[index];
            int ControlIndex = listGameScreenshots.FindIndex(x => x == item);

            PART_ListGameScreenshot.ItemsSource = null;
            listGameScreenshots[ControlIndex].ScreenshotsFolders.RemoveAt(indexFolder);
            PART_ListGameScreenshot.ItemsSource = listGameScreenshots;
            TextboxSearch_TextChanged(null, null);
        }

        private void ButtonAddFolder_Click(object sender, RoutedEventArgs e)
        {
            int index = int.Parse(((Button)sender).Tag.ToString());

            var item = ((List<ListGameScreenshot>)PART_ListGameScreenshot.ItemsSource)[index];
            int ControlIndex = listGameScreenshots.FindIndex(x => x == item);

            PART_ListGameScreenshot.ItemsSource = null;
            listGameScreenshots[ControlIndex].ScreenshotsFolders.Add(new FolderSettings());
            PART_ListGameScreenshot.ItemsSource = listGameScreenshots;
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
            ListGame SelectedItem = (ListGame)PART_ListGame.SelectedItem;

            PART_ListGame.ItemsSource = null;
            listGames.RemoveAt(index);
            PART_ListGame.ItemsSource = listGames;


            string Icon = string.Empty;
            if (!SelectedItem.Icon.IsNullOrEmpty())
            {
                Icon = _PlayniteApi.Database.GetFullFilePath(SelectedItem.Icon);
            }

            listGameScreenshots.Add(new ListGameScreenshot
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
            
            listGameScreenshots.Sort((x, y) => x.Name.CompareTo(y.Name));
            PART_ListGameScreenshot.ItemsSource = null;
            PART_ListGameScreenshot.ItemsSource = listGameScreenshots;
        }
        #endregion


        // Search by name
        private void TextboxSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            PART_ListGame.ItemsSource = listGames.FindAll(x => x.Name.ToLower().IndexOf(TextboxSearch.Text.ToLower()) > -1).ToList();
            PART_ListGameScreenshot.ItemsSource = listGameScreenshots.FindAll(x => x.Name.ToLower().IndexOf(TextboxSearch.Text.ToLower()) > -1).ToList();
        }


        // Add Steam game automaticly
        private void PART_BtAddSteamGame_Click(object sender, RoutedEventArgs e)
        {
            var tmpList = listGames.GetClone().Where(x => x.SourceName == "Steam").ToList();
            foreach (var game in tmpList)
            {
                int index = listGames.FindIndex(x => x.Id == game.Id);
                listGames.RemoveAt(index);

                string Icon = string.Empty;
                if (!game.Icon.IsNullOrEmpty())
                {
                    Icon = _PlayniteApi.Database.GetFullFilePath(game.Icon);
                }

                List<FolderSettings> ScreenshotsFolders = new List<FolderSettings>();
                ScreenshotsFolders.Add(new FolderSettings
                {
                    ScreenshotsFolder = "{SteamScreenshotsDir}\\" + _PlayniteApi.Database.Games.Get(game.Id).GameId + "\\screenshots"
                });

                listGameScreenshots.Add(new ListGameScreenshot
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
            PART_ListGame.ItemsSource = listGames;

            listGameScreenshots.Sort((x, y) => x.Name.CompareTo(y.Name));
            PART_ListGameScreenshot.ItemsSource = null;
            PART_ListGameScreenshot.ItemsSource = listGameScreenshots;

            TextboxSearch_TextChanged(null, null);
        }

        // Add Ubisoft Connect game automaticly
        private void PART_BtAddUplay_Click(object sender, RoutedEventArgs e)
        {
            var tmpList = listGames.GetClone().Where(x => x.SourceName.ToLower() == "ubisoft connect" || x.SourceName.ToLower() == "uplay").ToList();
            foreach (var game in tmpList)
            {
                int index = listGames.FindIndex(x => x.Id == game.Id);
                listGames.RemoveAt(index);

                string Icon = string.Empty;
                if (!game.Icon.IsNullOrEmpty())
                {
                    Icon = _PlayniteApi.Database.GetFullFilePath(game.Icon);
                }

                List<FolderSettings> ScreenshotsFolders = new List<FolderSettings>();
                ScreenshotsFolders.Add(new FolderSettings
                {
                    ScreenshotsFolder = "{UbisoftScreenshotsDir}\\" + game.Name
                });

                listGameScreenshots.Add(new ListGameScreenshot
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
            PART_ListGame.ItemsSource = listGames;

            listGameScreenshots.Sort((x, y) => x.Name.CompareTo(y.Name));
            PART_ListGameScreenshot.ItemsSource = null;
            PART_ListGameScreenshot.ItemsSource = listGameScreenshots;

            TextboxSearch_TextChanged(null, null);
        }


        #region FolderToSave
        private void ButtonSelectFolderToSave_Click(object sender, RoutedEventArgs e)
        {
            string SelectedFolder = _PlayniteApi.Dialogs.SelectFolder();
            if (!SelectedFolder.IsNullOrEmpty())
            {
                PART_FolderToSave.Text = SelectedFolder;
            }
        }
        #endregion
    }


    public class ListGame
    {
        public Guid Id { get; set; }
        public string Icon { get; set; }
        public string Name { get; set; }
        public string SourceName { get; set; }
        public string SourceIcon { get; set; }
    }
    
    public class ListGameScreenshot
    {
        public Guid Id { get; set; }
        public string Icon { get; set; }
        public string Name { get; set; }
        public string SourceName { get; set; }
        public string SourceIcon { get; set; }
        public bool UsedFilePattern { get; set; }
        public string FilePattern { get; set; }
        public List<FolderSettings> ScreenshotsFolders { get; set; }
    }
}