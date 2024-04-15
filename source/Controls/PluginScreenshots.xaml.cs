using CommonPluginsShared;
using CommonPluginsShared.Collections;
using CommonPluginsShared.Controls;
using CommonPluginsShared.Extensions;
using CommonPluginsShared.Interfaces;
using Playnite.SDK;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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

namespace ScreenshotsVisualizer.Controls
{
    /// <summary>
    /// Logique d'interaction pour PluginScreenshots.xaml
    /// </summary>
    public partial class PluginScreenshots : PluginUserControlExtend
    {
        private ScreenshotsVisualizerDatabase PluginDatabase => ScreenshotsVisualizer.PluginDatabase;
        internal override IPluginDatabase _PluginDatabase => PluginDatabase;

        private PluginScreenshotsDataContext ControlDataContext = new PluginScreenshotsDataContext();
        internal override IDataContext _ControlDataContext
        {
            get => ControlDataContext;
            set => ControlDataContext = (PluginScreenshotsDataContext)_ControlDataContext;
        }


        public PluginScreenshots()
        {
            InitializeComponent();
            this.DataContext = ControlDataContext;

            Task.Run(() =>
            {
                // Wait extension database are loaded
                System.Threading.SpinWait.SpinUntil(() => PluginDatabase.IsLoaded, -1);

                this.Dispatcher.BeginInvoke((Action)delegate
                {
                    PluginDatabase.PluginSettings.PropertyChanged += PluginSettings_PropertyChanged;
                    PluginDatabase.Database.ItemUpdated += Database_ItemUpdated;
                    PluginDatabase.Database.ItemCollectionChanged += Database_ItemCollectionChanged;
                    API.Instance.Database.Games.ItemUpdated += Games_ItemUpdated;

                    // Apply settings
                    PluginSettings_PropertyChanged(null, null);

                    PART_ListScreenshots.AddHandler(UIElement.MouseDownEvent, new MouseButtonEventHandler(PluginDatabase.ListBoxItem_MouseLeftButtonDownClick), true);
                });
            });
        }


        public override void SetDefaultDataContext()
        {
            ControlDataContext.IsActivated = PluginDatabase.PluginSettings.Settings.EnableIntegrationShowPictures;
            ControlDataContext.AddBorder = PluginDatabase.PluginSettings.Settings.AddBorder;
            ControlDataContext.AddRoundedCorner = PluginDatabase.PluginSettings.Settings.AddRoundedCorner;
            ControlDataContext.HideInfos = PluginDatabase.PluginSettings.Settings.HideScreenshotsInfos;

            ControlDataContext.ItemsSource = new ObservableCollection<Screenshot>();
        }


        public override void SetData(Game newContext, PluginDataBaseGameBase PluginGameData)
        {
            GameScreenshots gameScreenshots = (GameScreenshots)PluginGameData;

            List<Screenshot> screenshots = gameScreenshots.Items;
            screenshots.Sort((x, y) => y.Modifed.CompareTo(x.Modifed));

            ControlDataContext.ItemsSource = screenshots.ToObservable();
        }


        #region Events
        private void PART_ListScreenshots_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PluginDatabase.PluginSettings.Settings.LinkWithSinglePicture && PluginDatabase.PluginSettings.Settings.EnableIntegrationShowSinglePicture)
            {
                PluginSinglePicture ssvSinglePicture = UI.FindVisualChildren<PluginSinglePicture>(Application.Current.MainWindow).FirstOrDefault();

                if (ssvSinglePicture != null)
                {
                    ssvSinglePicture.SetPictureFromList(PART_ListScreenshots.SelectedIndex);
                }
            }
        }

        private void PART_BtDelete_Click(object sender, RoutedEventArgs e)
        {
            ListBoxItem item = UI.FindParent<ListBoxItem>((Button)sender);
            Screenshot screenshot = (Screenshot)item.DataContext;

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
                        Task.Run(() =>
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

                        GameScreenshots gameScreenshots = PluginDatabase.Get(GameContext);
                        gameScreenshots.Items.Remove(screenshot);
                        PluginDatabase.Update(gameScreenshots);

                        SetData(GameContext, gameScreenshots);
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
        #endregion

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
    }


    public class PluginScreenshotsDataContext : ObservableObject, IDataContext
    {
        private bool _IsActivated;
        public bool IsActivated { get => _IsActivated; set => SetValue(ref _IsActivated, value); }

        private bool _AddBorder;
        public bool AddBorder { get => _AddBorder; set => SetValue(ref _AddBorder, value); }

        private bool _AddRoundedCorner;
        public bool AddRoundedCorner { get => _AddRoundedCorner; set => SetValue(ref _AddRoundedCorner, value); }

        private bool _HideInfos;
        public bool HideInfos { get => _HideInfos; set => SetValue(ref _HideInfos, value); }

        private ObservableCollection<Screenshot> _ItemsSource = new ObservableCollection<Screenshot>
        {
            new Screenshot
            {
                FileName = @"icon.png",
                Modifed = DateTime.Now
            }
        };
        public ObservableCollection<Screenshot> ItemsSource { get => _ItemsSource; set => SetValue(ref _ItemsSource, value); }
    }
}
