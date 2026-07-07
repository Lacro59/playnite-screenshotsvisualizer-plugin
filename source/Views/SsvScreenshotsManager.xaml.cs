using CommonPluginsShared;
using CommonPluginsShared.Commands;
using CommonPluginsShared.UI;
using CommonPluginsShared.Utilities;
using Playnite.SDK;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ScreenshotsVisualizer.Views
{
    /// <summary>
    /// Logique d'interaction pour SsvScreenshotsManager.xaml
    /// </summary>
    public partial class SsvScreenshotsManager : UserControl
    {
        private ScreenshotsVisualizerDatabase PluginDatabase => ScreenshotsVisualizer.PluginDatabase;


        public SsvScreenshotsManager()
        {
            InitializeComponent();
            DataContext = new SsvScreenshotsManagerData();

            SetData();
            SetInfos();

            PluginDatabase.DatabaseItemUpdated += Database_ItemUpdated;

            PART_ListScreenshots.AddHandler(UIElement.MouseDownEvent, new MouseButtonEventHandler(PluginDatabase.ListBoxItem_MouseLeftButtonDownClick), true);
            PART_Copy.Visibility = Visibility.Collapsed;
        }

        private void Database_ItemUpdated(object sender, ItemUpdatedEventArgs<GameScreenshots> e)
        {
            _ = Application.Current.Dispatcher?.BeginInvoke(DispatcherPriority.Loaded, new ThreadStart(delegate
            {
                try
                {
                    Guid? selectedGameId = (PART_LveGames.SelectedItem as LveGame)?.Id;
                    SetData(selectedGameId);
                    SetInfos();
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, true);
                }
            }));
        }

        private void SetData(Guid? selectedGameId = null)
        {
            PART_DataLoad.Visibility = Visibility.Visible;
            PART_Data.Visibility = Visibility.Hidden;

            _ = Task.Run(() => 
            {
                try
                {
                    ObservableCollection<LveGame> LveGames = PluginDatabase.GetAllCache().Where(x => x.HasData)
                                                                    .Select(x => new LveGame
                                                                    {
                                                                        Id = x.Id,
                                                                        Icon = API.Instance.Database.GetFullFilePath(x.Icon),
                                                                        Name = x.Name,
                                                                        LastActivity = x.LastActivity,
                                                                        SourceName = PlayniteTools.GetSourceName(x.Id),

                                                                        LastSsv = x.Items.Select(y => y.Modifed).Max(),
                                                                        Total = x.Items.Count
                                                                    }).ToObservable();
                    return LveGames;
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, PluginDatabase.PluginName);
                    return new ObservableCollection<LveGame>();
                }
            }).ContinueWith(antecedent =>
            {
                _ = Application.Current.Dispatcher?.BeginInvoke(DispatcherPriority.Loaded, new ThreadStart(delegate
                {
                    ((SsvScreenshotsManagerData)DataContext).LveGames = antecedent.Result;

                    PART_LveGames.Sorting();
                    if (selectedGameId.HasValue)
                    {
                        LveGame selectedGame = ((SsvScreenshotsManagerData)DataContext).LveGames
                            .FirstOrDefault(x => x.Id == selectedGameId.Value);
                        if (selectedGame != null)
                        {
                            PART_LveGames.SelectedItem = selectedGame;
                        }
                    }

                    PART_DataLoad.Visibility = Visibility.Hidden;
                    PART_Data.Visibility = Visibility.Visible;
                }));
            });
        }


        private void SetInfos()
        {
            List<Screenshot> screenOnly = PluginDatabase.GetAllCache().SelectMany(x => x.Items).Where(x => !x.IsVideo).ToList();
            int ScreenshotsCount = screenOnly.Count;
            long ScreenshotsTotalSize = 0;
            foreach (Screenshot item in screenOnly)
            {
                ScreenshotsTotalSize += item.FileSize;
            }
            PART_ScreenshotsCount.Content = ScreenshotsCount > 1 ? string.Format(ResourceProvider.GetString("LOCSsvScreenshots"), ScreenshotsCount) : string.Format(ResourceProvider.GetString("LOCSsvScreenshot"), ScreenshotsCount);
            PART_ScreenshotsSize.Content = UtilityTools.SizeSuffix(ScreenshotsTotalSize);


            List<Screenshot> videoOnly = PluginDatabase.GetAllCache().SelectMany(x => x.Items).Where(x => x.IsVideo).ToList();
            int VideosCount = videoOnly.Count;
            long VideosTotalSize = 0;
            foreach (Screenshot item in videoOnly)
            {
                VideosTotalSize += item.FileSize;
            }
            PART_VideosCount.Content = VideosCount > 1 ? string.Format(ResourceProvider.GetString("LOCSsvVideos"), VideosCount) : string.Format(ResourceProvider.GetString("LOCSsvVideo"), VideosCount);
            PART_VideosSize.Content = UtilityTools.SizeSuffix(VideosTotalSize);


            PART_FilesCount.Content = ScreenshotsCount + VideosCount;
            PART_FilesSize.Content = UtilityTools.SizeSuffix(ScreenshotsTotalSize + VideosTotalSize);
        }


        private void PART_LveGames_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender == null)
            {
                return;
            }


            ListBox listBox = (ListBox)sender;
            LveGame lveGame = (LveGame)listBox.SelectedItem;

            if (lveGame == null)
            {
                return;
            }

            RefreshScreenshotsForGame(lveGame.Id);
        }

        private void RefreshScreenshotsForGame(Guid gameId)
        {
            _ = Task.Run(() =>
            {
                GameScreenshots gameScreenshots = PluginDatabase.Get(gameId, true);
                if (gameScreenshots?.Items == null)
                {
                    return false;
                }

                List<Screenshot> screenshots = gameScreenshots.Items;
                screenshots.Sort((x, y) => y.Modifed.CompareTo(x.Modifed));

                _ = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new ThreadStart(delegate
                {
                    ((SsvScreenshotsManagerData)DataContext).Screenshots = screenshots.ToObservable();
                }));

                return true;
            });
        }

        private void ClearScreenshotPreview()
        {
            SsvScreenshotsManagerData dataContext = (SsvScreenshotsManagerData)DataContext;
            dataContext.FileNameImage = string.Empty;
            dataContext.FileNameVideo = string.Empty;
            dataContext.FilePath = string.Empty;
        }

        private void PART_ListScreenshots_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender == null)
            {
                return;
            }

            ListBox listbox = sender as ListBox;
            if (listbox.SelectedItem is null)
            {
                ((SsvScreenshotsManagerData)DataContext).FileNameImage = string.Empty;
                ((SsvScreenshotsManagerData)DataContext).FileNameVideo = string.Empty;
                ((SsvScreenshotsManagerData)DataContext).FilePath = string.Empty;
                return;
            }

            Screenshot item = listbox.SelectedItem as Screenshot;
            if (item.IsVideo)
            {
                ((SsvScreenshotsManagerData)DataContext).FileNameImage = string.Empty;
                ((SsvScreenshotsManagerData)DataContext).FileNameVideo = item.FileName;
            }
            else
            {
                ((SsvScreenshotsManagerData)DataContext).FileNameImage = item.FileName;
                ((SsvScreenshotsManagerData)DataContext).FileNameVideo = string.Empty;
            }
            ((SsvScreenshotsManagerData)DataContext).FilePath = item.FileName;
        }


        private void PART_BtDelete_Click(object sender, RoutedEventArgs e)
        {
            ListBoxItem item = UIHelper.FindParent<ListBoxItem>((Button)sender);
            Screenshot screenshot = item?.DataContext as Screenshot;
            if (screenshot == null)
            {
                return;
            }

            LveGame selectedGame = PART_LveGames.SelectedItem as LveGame;
            if (selectedGame == null)
            {
                return;
            }

            Guid gameId = selectedGame.Id;

            MessageBoxResult RessultDialog = API.Instance.Dialogs.ShowMessage(
                string.Format(ResourceProvider.GetString("LOCSsvDeleteConfirm"), screenshot.FileNameOnly),
                PluginDatabase.PluginName,
                MessageBoxButton.YesNo
            );

            if (RessultDialog == MessageBoxResult.Yes)
            {
                try
                {
                    ((SsvScreenshotsManagerData)DataContext).Screenshots = new ObservableCollection<Screenshot>();
                    ClearScreenshotPreview();

                    SsvScreenshotDeleteResult result = PluginDatabase.TryDeleteScreenshot(gameId, screenshot);
                    if (result == SsvScreenshotDeleteResult.Success
                        || result == SsvScreenshotDeleteResult.SkippedMissingPhysicalFile)
                    {
                        SetInfos();
                        RefreshScreenshotsForGame(gameId);
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, PluginDatabase.PluginName);
                }
            }
        }


        private void PART_Copy_Click(object sender, RoutedEventArgs e)
        {
            Screenshot screenshot = PART_ListScreenshots.SelectedItem as Screenshot;
            if (!screenshot?.IsVideo ?? false && File.Exists(screenshot?.FileName))
            {
                try
                {
                    System.Drawing.Image img = System.Drawing.Image.FromFile(screenshot.FileName);
                    Clipboard.SetDataObject(img);
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, PluginDatabase.PluginName);
                }
            }
        }

        private void Grid_MouseEnter(object sender, MouseEventArgs e)
        {
            Screenshot screenshot = (Screenshot)PART_ListScreenshots.SelectedItem;
            if (!screenshot?.IsVideo ?? false && File.Exists(screenshot?.FileName))
            {
                PART_Copy.Visibility = Visibility.Visible;
            }
        }

        private void Grid_MouseLeave(object sender, MouseEventArgs e)
        {
            PART_Copy.Visibility = Visibility.Collapsed;
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string filePath = Path.GetDirectoryName(((SsvScreenshotsManagerData)DataContext).FilePath);
            if (Directory.Exists(filePath))
            {
                _ = Process.Start(filePath);
            }
        }
    }


    public class SsvScreenshotsManagerData : ObservableObject
    {
        private ObservableCollection<LveGame> _LveGames = new ObservableCollection<LveGame>();
        public ObservableCollection<LveGame> LveGames { get => _LveGames; set => SetValue(ref _LveGames, value); }

        private ObservableCollection<Screenshot> _Screenshots = new ObservableCollection<Screenshot>();
        public ObservableCollection<Screenshot> Screenshots { get => _Screenshots; set => SetValue(ref _Screenshots, value); }

        private string _FileNameImage = string.Empty;
        public string FileNameImage { get => _FileNameImage; set => SetValue(ref _FileNameImage, value); }

        private string _FileNameVideo = string.Empty;
        public string FileNameVideo { get => _FileNameVideo; set => SetValue(ref _FileNameVideo, value); }

        private string _FilePath = string.Empty;
        public string FilePath { get => _FilePath; set => SetValue(ref _FilePath, value); }
    }


    public class LveGame
    {
        private ScreenshotsVisualizerDatabase PluginDatabase => ScreenshotsVisualizer.PluginDatabase;

        public Guid Id { get; set; }
        public string Icon { get; set; }
        public string Name { get; set; } 
        public DateTime? LastActivity { get; set; }
        public string SourceName { get; set; }
        public string SourceIcon => TransformIcon.Get(SourceName);

        public DateTime LastSsv { get; set; }
        public int Total { get; set; }

        public RelayCommand<Guid> GoToGame => CommandsNavigation.GoToGame;
        public bool GameExist => API.Instance.Database.Games.Get(Id) != null;
    }
}
