using CommonPluginsShared;
using CommonPluginsShared.Collections;
using CommonPluginsShared.Controls;
using CommonPluginsShared.Converters;
using CommonPluginsShared.Interfaces;
using Playnite.SDK;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.Services;
using ScreenshotsVisualizer.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
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
        private ScreenshotsVisualizerDatabase PluginDatabase => ScreenshotsVisualizer.PluginDatabase;
        protected override IPluginDatabase pluginDatabase => PluginDatabase;

        private PluginSinglePictureDataContext ControlDataContext = new PluginSinglePictureDataContext();
        protected override IDataContext controlDataContext
        {
            get => ControlDataContext;
            set => ControlDataContext = (PluginSinglePictureDataContext)controlDataContext;
        }

        private List<Screenshot> Screenshots { get; set; } = new List<Screenshot>();
        private int Index { get; set; } = 0;


        public PluginSinglePicture()
        {
            InitializeComponent();
            DataContext = ControlDataContext;
            Loaded += OnLoaded;
        }

        protected override void AttachStaticEvents()
        {
            base.AttachStaticEvents();
            AttachPluginEvents(PluginDatabase.PluginName, () =>
            {
                PluginDatabase.PluginSettings.PropertyChanged += CreatePluginSettingsHandler();
                PluginDatabase.DatabaseItemUpdated += CreateDatabaseItemUpdatedHandler<GameScreenshots>();
                PluginDatabase.DatabaseItemCollectionChanged += CreateDatabaseCollectionChangedHandler<GameScreenshots>();
            });
        }

        public override void SetDefaultDataContext()
        {
            ControlDataContext.IsActivated = PluginDatabase.PluginSettings.EnableIntegrationShowSinglePicture;
            ControlDataContext.AddBorder = PluginDatabase.PluginSettings.AddBorderSinglePicture;
            ControlDataContext.AddRoundedCorner = PluginDatabase.PluginSettings.AddRoundedCornerSinglePicture;
            ControlDataContext.IntegrationShowSinglePictureHeight = PluginDatabase.PluginSettings.IntegrationShowSinglePictureHeight;

            ControlDataContext.EnablePrev = false;
            ControlDataContext.EnableNext = false;

            ControlDataContext.IsVideo = false;
            ControlDataContext.Thumbnail = string.Empty;
            ControlDataContext.PictureSource = string.Empty;
            ControlDataContext.PictureInfos = string.Empty;
        }

        public override void SetData(Game newContext, PluginGameEntry pluginGameData)
        {
            GameScreenshots gameScreenshots = (GameScreenshots)pluginGameData;

            Screenshots = gameScreenshots.Items;
            Screenshots.Sort((x, y) => y.Modifed.CompareTo(x.Modifed));

            Index = 0;

            if (Screenshots.Count > 1)
            {
                ControlDataContext.EnablePrev = true;
                ControlDataContext.EnableNext = true;
            }

            if (Screenshots.Count > 0)
            {
                SetPicture(Screenshots[Index]);
            }
        }

        private void SetPicture(Screenshot screenshot)
        {
            bool isVideo = false;
            string thumbnail = string.Empty;
            string pictureSource = string.Empty;
            string pictureInfos = string.Empty;

            LocalDateTimeConverter Converters = new LocalDateTimeConverter();

            if (File.Exists(screenshot.FileName))
            {
                isVideo = screenshot.IsVideo;
                thumbnail = screenshot.Thumbnail;
                pictureSource = screenshot.FileName;
                pictureInfos = (string)Converters.Convert(screenshot.Modifed, null, null, null);
            }

            ControlDataContext.IsVideo = isVideo;
            ControlDataContext.Thumbnail = thumbnail;
            ControlDataContext.PictureSource = pictureSource;
            ControlDataContext.PictureInfos = pictureInfos;
        }

        public void SetPictureFromList(int index)
        {
            if (index != -1)
            {
                Index = index;

                SetPicture(Screenshots[index]);

                _ = Application.Current.Dispatcher?.BeginInvoke(DispatcherPriority.Loaded, new ThreadStart(delegate
                {
                    DataContext = null;
                    DataContext = ControlDataContext;
                }));
            }
        }

        #region Events

        private void PART_Prev_Click(object sender, RoutedEventArgs e)
        {
            if (Index == 0)
            {
                Index = Screenshots.Count - 1;
            }
            else
            {
                Index--;
            }

            SetPictureFromList(Index);
        }

        private void PART_Next_Click(object sender, RoutedEventArgs e)
        {
            if (Index == Screenshots.Count - 1)
            {
                Index = 0;
            }
            else
            {
                Index++;
            }

            SetPictureFromList(Index);
        }

        private void PART_Contener_MouseDown(object sender, MouseButtonEventArgs e)
        {
            bool isGood = false;

            if (PluginDatabase.PluginSettings.OpenViewerWithOnSelectionSinglePicture)
            {
                isGood = true;
            }
            else
            {
                if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
                {
                    isGood = true;
                }
            }

            if (isGood)
            {
                if (PluginDatabase.PluginSettings.UseExternalViewer)
                {
                    ScreenshotsVisualizerWindows.OpenWithExternalViewer(Screenshots[Index].FileName);
                }
                else
                {
                    ScreenshotsVisualizerWindows windows = PluginDatabase.PluginWindows as ScreenshotsVisualizerWindows;
                    if (windows != null)
                    {
                        windows.ShowSinglePictureWindow(Screenshots[Index], Screenshots);
                    }
                }
            }
        }

        #endregion
    }


    public class PluginSinglePictureDataContext : ObservableObject, IDataContext
    {
        private bool _isActivated;
        public bool IsActivated { get => _isActivated; set => SetValue(ref _isActivated, value); }

        private bool _addBorder;
        public bool AddBorder { get => _addBorder; set => SetValue(ref _addBorder, value); }

        private bool _addRoundedCorner;
        public bool AddRoundedCorner { get => _addRoundedCorner; set => SetValue(ref _addRoundedCorner, value); }

        private double _integrationShowSinglePictureHeight;
        public double IntegrationShowSinglePictureHeight { get => _integrationShowSinglePictureHeight; set => SetValue(ref _integrationShowSinglePictureHeight, value); }

        private bool _enablePrev;
        public bool EnablePrev { get => _enablePrev; set => SetValue(ref _enablePrev, value); }

        private bool _enableNext;
        public bool EnableNext { get => _enableNext; set => SetValue(ref _enableNext, value); }

        private bool _isVideo;
        public bool IsVideo { get => _isVideo; set => SetValue(ref _isVideo, value); }

        private string _thumbnail;
        public string Thumbnail { get => _thumbnail; set => SetValue(ref _thumbnail, value); }

        private string _pictureSource;
        public string PictureSource { get => _pictureSource; set => SetValue(ref _pictureSource, value); }

        private string _pictureInfos;
        public string PictureInfos { get => _pictureInfos; set => SetValue(ref _pictureInfos, value); }
    }
}