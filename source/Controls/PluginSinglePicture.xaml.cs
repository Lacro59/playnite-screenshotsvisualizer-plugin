using CommonPluginsShared;
using CommonPluginsShared.Collections;
using CommonPluginsShared.Controls;
using CommonPluginsShared.Converters;
using CommonPluginsShared.Interfaces;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.Services;
using ScreenshotsVisualizer.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ScreenshotsVisualizer.Controls
{
    /// <summary>
    /// Logique d'interaction pour PluginSinglePicture.xaml
    /// </summary>
    public partial class PluginSinglePicture : PluginUserControlExtend
    {
        private ScreenshotsVisualizerDatabase PluginDatabase = ScreenshotsVisualizer.PluginDatabase;
        internal override IPluginDatabase _PluginDatabase
        {
            get => PluginDatabase;
            set => PluginDatabase = (ScreenshotsVisualizerDatabase)_PluginDatabase;
        }

        private PluginSinglePictureDataContext ControlDataContext = new PluginSinglePictureDataContext();
        internal override IDataContext _ControlDataContext
        {
            get => ControlDataContext;
            set => ControlDataContext = (PluginSinglePictureDataContext)_ControlDataContext;
        }

        private List<Screenshot> screenshots = new List<Screenshot>();
        private int index = 0;


        public PluginSinglePicture()
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
                    PluginDatabase.PlayniteApi.Database.Games.ItemUpdated += Games_ItemUpdated;

                    // Apply settings
                    PluginSettings_PropertyChanged(null, null);
                });
            });
        }


        public override void SetDefaultDataContext()
        {
            ControlDataContext.IsActivated = PluginDatabase.PluginSettings.Settings.EnableIntegrationShowSinglePicture;
            ControlDataContext.AddBorder = PluginDatabase.PluginSettings.Settings.AddBorderSinglePicture;
            ControlDataContext.AddRoundedCorner = PluginDatabase.PluginSettings.Settings.AddRoundedCornerSinglePicture;
            ControlDataContext.IntegrationShowSinglePictureHeight = PluginDatabase.PluginSettings.Settings.IntegrationShowSinglePictureHeight;

            ControlDataContext.EnablePrev = false;
            ControlDataContext.EnableNext = false;

            ControlDataContext.IsVideo = false;
            ControlDataContext.Thumbnail = string.Empty;
            ControlDataContext.PictureSource = string.Empty;
            ControlDataContext.PictureInfos = string.Empty;
        }


        public override void SetData(Game newContext, PluginDataBaseGameBase PluginGameData)
        {
            GameScreenshots gameScreenshots = (GameScreenshots)PluginGameData;

            this.screenshots = gameScreenshots.Items;
            this.screenshots.Sort((x, y) => y.Modifed.CompareTo(x.Modifed));

            index = 0;

            if (screenshots.Count > 1)
            {
                ControlDataContext.EnablePrev = true;
                ControlDataContext.EnableNext = true;
            }

            if (screenshots.Count > 0)
            {
                SetPicture(screenshots[index]);
            }
        }


        private void SetPicture(Screenshot screenshot)
        {
            bool IsVideo = false;
            string Thumbnail = string.Empty;
            string PictureSource = string.Empty;
            string PictureInfos = string.Empty;

            var Converters = new LocalDateTimeConverter();

            if (File.Exists(screenshot.FileName))
            {
                IsVideo = screenshot.IsVideo;
                Thumbnail = screenshot.Thumbnail;
                PictureSource = screenshot.FileName;
                PictureInfos = (string)Converters.Convert(screenshot.Modifed, null, null, null);
            }

            ControlDataContext.IsVideo = IsVideo;
            ControlDataContext.Thumbnail = Thumbnail;
            ControlDataContext.PictureSource = PictureSource;
            ControlDataContext.PictureInfos = PictureInfos;
        }

        public void SetPictureFromList(int index)
        {
            if (index != -1)
            {
                this.index = index;

                SetPicture(screenshots[index]);

                this.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new ThreadStart(delegate
                {
                    this.DataContext = null;
                    this.DataContext = ControlDataContext;
                }));
            }
        }


        #region Events
        private void PART_Prev_Click(object sender, RoutedEventArgs e)
        {
            if (index == 0)
            {
                index = screenshots.Count - 1;
            }
            else
            {
                index--;
            }

            SetPictureFromList(index);
        }

        private void PART_Next_Click(object sender, RoutedEventArgs e)
        {
            if (index == screenshots.Count - 1)
            {
                index = 0;
            }
            else
            {
                index++;
            }

            SetPictureFromList(index);
        }


        private void PART_Contener_MouseDown(object sender, MouseButtonEventArgs e)
        {
            bool IsGood = false;

            if (PluginDatabase.PluginSettings.Settings.OpenViewerWithOnSelectionSinglePicture)
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

                var ViewExtension = new SsvSinglePictureView(screenshots[index]);
                Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PluginDatabase.PlayniteApi, resources.GetString("LOCSsv"), ViewExtension, windowOptions);
                windowExtension.ResizeMode = ResizeMode.CanResize;
                windowExtension.ShowDialog();
            }
        }
        #endregion
    }


    public class PluginSinglePictureDataContext : ObservableObject, IDataContext
    {
        private bool _IsActivated;
        public bool IsActivated { get => _IsActivated; set => SetValue(ref _IsActivated, value); }

        private bool _AddBorder;
        public bool AddBorder { get => _AddBorder; set => SetValue(ref _AddBorder, value); }

        private bool _AddRoundedCorner;
        public bool AddRoundedCorner { get => _AddRoundedCorner; set => SetValue(ref _AddRoundedCorner, value); }

        private double _IntegrationShowSinglePictureHeight;
        public double IntegrationShowSinglePictureHeight { get => _IntegrationShowSinglePictureHeight; set => SetValue(ref _IntegrationShowSinglePictureHeight, value); }

        private bool _EnablePrev;
        public bool EnablePrev { get => _EnablePrev; set => SetValue(ref _EnablePrev, value); }

        private bool _EnableNext;
        public bool EnableNext { get => _EnableNext; set => SetValue(ref _EnableNext, value); }

        private bool _IsVideo;
        public bool IsVideo { get => _IsVideo; set => SetValue(ref _IsVideo, value); }

        private string _Thumbnail;
        public string Thumbnail { get => _Thumbnail; set => SetValue(ref _Thumbnail, value); }

        private string _PictureSource;
        public string PictureSource { get => _PictureSource; set => SetValue(ref _PictureSource, value); }

        private string _PictureInfos;
        public string PictureInfos { get => _PictureInfos; set => SetValue(ref _PictureInfos, value); }
    }
}
