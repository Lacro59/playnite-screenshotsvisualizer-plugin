using CommonPluginsShared;
using Playnite.SDK;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Clients;
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

                    listGameScreenshots.Add(new ListGameScreenshot
                    {
                        Id = item.Id,
                        Icon = Icon,
                        Name = game.Name,
                        ScreenshotsFolder = item.ScreenshotsFolder,
                        UsedFilePattern = item.UsedFilePattern,
                        FilePattern = item.FilePattern,
                        SourceName = PlayniteTools.GetSourceName(PlayniteApi, item.Id)
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
                    SourceName = PlayniteTools.GetSourceName(PlayniteApi, item.Id)
                });
            }

            listGames.Sort((x, y) => x.Name.CompareTo(y.Name));
            listGameScreenshots.Sort((x, y) => x.Name.CompareTo(y.Name));
        }


        #region Action on list games screenshots
        private void ButtonSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            int index = int.Parse(((Button)sender).Tag.ToString());

            string SelectedFolder = _PlayniteApi.Dialogs.SelectFolder();
            if (!SelectedFolder.IsNullOrEmpty())
            {
                var TextBox = Tools.FindVisualChildren<TextBox>(((FrameworkElement)((FrameworkElement)sender).Parent).Parent).FirstOrDefault();

                if (TextBox != null)
                {
                    TextBox.Text = SelectedFolder;
                    listGameScreenshots[index].ScreenshotsFolder = SelectedFolder;
                }
            }
        }

        private void ButtonRemove_Click(object sender, RoutedEventArgs e)
        {
            int index = int.Parse(((Button)sender).Tag.ToString());

            PART_ListGameScreenshot.ItemsSource = null;
            listGameScreenshots.RemoveAt(index);
            PART_ListGameScreenshot.ItemsSource = listGameScreenshots;

            var TaskView = Task.Run(() =>
            {
                var DbWithoutAlready = _PlayniteApi.Database.Games.Where(x => !listGameScreenshots.Any(y => x.Id == y.Id));
                listGames = new List<ListGame>();
                foreach (Game item in DbWithoutAlready)
                {
                    string Icon = string.Empty;
                    if (!item.Icon.IsNullOrEmpty())
                    {
                        Icon = _PlayniteApi.Database.GetFullFilePath(item.Icon);
                    }

                    listGames.Add(new ListGame
                    {
                        Id = item.Id,
                        Icon = Icon,
                        Name = item.Name,
                        SourceName = PlayniteTools.GetSourceName(_PlayniteApi, item.Id)
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
                ScreenshotsFolder = string.Empty,
                UsedFilePattern = false,
                FilePattern = string.Empty,
                SourceName = SelectedItem.SourceName
            });
            
            listGameScreenshots.Sort((x, y) => x.Name.CompareTo(y.Name));
            PART_ListGameScreenshot.ItemsSource = null;
            PART_ListGameScreenshot.ItemsSource = listGameScreenshots;
        }
        #endregion


        // Search by name
        private void TextboxSearch_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            //if (!TextboxSearch.Text.IsNullOrEmpty())
            //{
                PART_ListGame.ItemsSource = listGames.FindAll(x => x.Name.ToLower().IndexOf(TextboxSearch.Text.ToLower()) > -1).ToList();
                PART_ListGameScreenshot.ItemsSource = listGameScreenshots.FindAll(x => x.Name.ToLower().IndexOf(TextboxSearch.Text.ToLower()) > -1).ToList();
            //}
        }


        // Add Steam game automaticly
        private void PART_BtAddSteamGame_Click(object sender, RoutedEventArgs e)
        {
            Steam steam = new Steam(_PluginUserDataPath);

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

                listGameScreenshots.Add(new ListGameScreenshot
                {
                    Id = game.Id,
                    Icon = Icon,
                    Name = game.Name,
                    ScreenshotsFolder = steam.GetGamePathScreenshotsFolder(_PlayniteApi.Database.Games.Get(game.Id)),
                    SourceName = game.SourceName
                });
            }

            PART_ListGame.ItemsSource = null;
            PART_ListGame.ItemsSource = listGames;

            listGameScreenshots.Sort((x, y) => x.Name.CompareTo(y.Name));
            PART_ListGameScreenshot.ItemsSource = null;
            PART_ListGameScreenshot.ItemsSource = listGameScreenshots;
        }
    }


    public class ListGame
    {
        public Guid Id { get; set; }
        public string Icon { get; set; }
        public string Name { get; set; }
        public string SourceName { get; set; }
    }
    
    public class ListGameScreenshot
    {
        public Guid Id { get; set; }
        public string Icon { get; set; }
        public string Name { get; set; }
        public string SourceName { get; set; }
        public bool UsedFilePattern { get; set; }
        public string FilePattern { get; set; }
        public string ScreenshotsFolder { get; set; }
    }
}