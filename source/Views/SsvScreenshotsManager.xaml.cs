using CommonPluginsShared;
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

            PluginDatabase.Database.ItemUpdated += Database_ItemUpdated;

            PART_ListScreenshots.AddHandler(UIElement.MouseDownEvent, new MouseButtonEventHandler(PluginDatabase.ListBoxItem_MouseLeftButtonDownClick), true);
            PART_Copy.Visibility = Visibility.Collapsed;
        }

        private void Database_ItemUpdated(object sender, ItemUpdatedEventArgs<GameScreenshots> e)
        {
            _ = Application.Current.Dispatcher?.BeginInvoke(DispatcherPriority.Loaded, new ThreadStart(delegate
            {
                try
                {
                    SetData();
                    SetInfos();
                }
                catch (Exception ex)
                {

                }
            }));
        }

        private void SetData(int index = -1)
        {
            PART_DataLoad.Visibility = Visibility.Visible;
            PART_Data.Visibility = Visibility.Hidden;

            _ = Task.Run(() => 
            {
                try
                {
                    ObservableCollection<LveGame> LveGames = PluginDatabase.Database.Where(x => x.HasData)
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
                    if (index != -1)
                    {
                        PART_LveGames.SelectedIndex = index;
                    }

                    PART_DataLoad.Visibility = Visibility.Hidden;
                    PART_Data.Visibility = Visibility.Visible;
                }));
            });
        }


        private void SetInfos()
        {
            List<Screenshot> screenOnly = PluginDatabase.Database.SelectMany(x => x.Items).Where(x => !x.IsVideo).ToList();
            int ScreenshotsCount = screenOnly.Count;
            long ScreenshotsTotalSize = 0;
            foreach (Screenshot item in screenOnly)
            {
                ScreenshotsTotalSize += item.FileSize;
            }
            PART_ScreenshotsCount.Content = ScreenshotsCount > 1 ? string.Format(ResourceProvider.GetString("LOCSsvScreenshots"), ScreenshotsCount) : string.Format(ResourceProvider.GetString("LOCSsvScreenshot"), ScreenshotsCount);
            PART_ScreenshotsSize.Content = Tools.SizeSuffix(ScreenshotsTotalSize);


            List<Screenshot> videoOnly = PluginDatabase.Database.SelectMany(x => x.Items).Where(x => x.IsVideo).ToList();
            int VideosCount = videoOnly.Count;
            long VideosTotalSize = 0;
            foreach (Screenshot item in videoOnly)
            {
                VideosTotalSize += item.FileSize;
            }
            PART_VideosCount.Content = VideosCount > 1 ? string.Format(ResourceProvider.GetString("LOCSsvVideos"), VideosCount) : string.Format(ResourceProvider.GetString("LOCSsvVideo"), VideosCount);
            PART_VideosSize.Content = Tools.SizeSuffix(VideosTotalSize);


            PART_FilesCount.Content = ScreenshotsCount + VideosCount;
            PART_FilesSize.Content = Tools.SizeSuffix(ScreenshotsTotalSize + VideosTotalSize);
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

            _ = Task.Run(() =>
            {
                GameScreenshots gameScreenshots = PluginDatabase.Get(lveGame.Id, true);

                List<Screenshot> Screenshots = gameScreenshots.Items;
                Screenshots.Sort((x, y) => y.Modifed.CompareTo(x.Modifed));

                _ = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new ThreadStart(delegate
                {
                    ((SsvScreenshotsManagerData)DataContext).Screenshots = Screenshots.ToObservable();
                }));

                return true;
            });
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
            ListBoxItem item = UI.FindParent<ListBoxItem>((Button)sender);
            Screenshot screenshot = (Screenshot)item.DataContext;
            int indexSelected = PART_LveGames.SelectedIndex;

            MessageBoxResult RessultDialog = API.Instance.Dialogs.ShowMessage(
                string.Format(ResourceProvider.GetString("LOCSsvDeleteConfirm"), screenshot.FileNameOnly),
                PluginDatabase.PluginName,
                MessageBoxButton.YesNo
            );

            if (RessultDialog == MessageBoxResult.Yes)
            {
                try
                {
                    if (File.Exists(screenshot.FileName))
                    {
                        ((SsvScreenshotsManagerData)DataContext).Screenshots = new ObservableCollection<Screenshot>();

                        _ = Task.Run(() =>
                        {
                            // TODO do better
                            while (IsFileLocked(new FileInfo(screenshot.FileName)))
                            {

                            }

                            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                                screenshot.FileName,
                                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin,
                                Microsoft.VisualBasic.FileIO.UICancelOption.ThrowException);
                        });

                        GameScreenshots gameScreenshots = PluginDatabase.Get(((LveGame)PART_LveGames.SelectedItem).Id);
                        _ = gameScreenshots.Items.Remove(screenshot);
                        PluginDatabase.Update(gameScreenshots);

                        SetData(indexSelected);
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, true, PluginDatabase.PluginName);
                }
            }
            else
            {

            }
        }

        protected bool IsFileLocked(FileInfo file)
        {
            FileStream stream = null;

            try
            {
                stream = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            }
            finally
            {
                if (stream != null)
                {
                    stream.Close();
                }
            }

            //file is not locked
            return false;
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
                    Common.LogError(ex, false);
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

        public RelayCommand<Guid> GoToGame => PluginDatabase.GoToGame;
        public bool GameExist => API.Instance.Database.Games.Get(Id) != null;
    }
}
