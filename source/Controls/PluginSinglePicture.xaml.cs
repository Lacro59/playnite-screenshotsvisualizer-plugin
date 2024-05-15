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
        private ScreenshotsVisualizerDatabase PluginDatabase => ScreenshotsVisualizer.PluginDatabase;
        internal override IPluginDatabase pluginDatabase => PluginDatabase;

        private PluginSinglePictureDataContext ControlDataContext = new PluginSinglePictureDataContext();
        internal override IDataContext controlDataContext
        {
            get => ControlDataContext;
            set => ControlDataContext = (PluginSinglePictureDataContext)controlDataContext;
        }

        private List<Screenshot> screenshots = new List<Screenshot>();
        private int index = 0;


        public PluginSinglePicture()
        {
            InitializeComponent();
            this.DataContext = ControlDataContext;

            _ = Task.Run(() =>
            {
                // Wait extension database are loaded
                _ = SpinWait.SpinUntil(() => PluginDatabase.IsLoaded, -1);

                _ = Application.Current.Dispatcher.BeginInvoke((Action)delegate
                {
                    PluginDatabase.PluginSettings.PropertyChanged += PluginSettings_PropertyChanged;
                    PluginDatabase.Database.ItemUpdated += Database_ItemUpdated;
                    PluginDatabase.Database.ItemCollectionChanged += Database_ItemCollectionChanged;
                    API.Instance.Database.Games.ItemUpdated += Games_ItemUpdated;

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

            LocalDateTimeConverter Converters = new LocalDateTimeConverter();

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

                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new ThreadStart(delegate
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
                    CanBeResizable = true,
                    Height = 720,
                    Width = 1280
                };

                SsvSinglePictureView ViewExtension = new SsvSinglePictureView(screenshots[index], screenshots);
                Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(ResourceProvider.GetString("LOCSsv") + " - " + screenshots[index].FileNameOnly, ViewExtension, windowOptions);
                windowExtension.ShowDialog();
            }
        }
        #endregion
    }


    public class PluginSinglePictureDataContext : ObservableObject, IDataContext
    {
        private bool isActivated;
        public bool IsActivated { get => isActivated; set => SetValue(ref isActivated, value); }

        private bool addBorder;
        public bool AddBorder { get => addBorder; set => SetValue(ref addBorder, value); }

        private bool addRoundedCorner;
        public bool AddRoundedCorner { get => addRoundedCorner; set => SetValue(ref addRoundedCorner, value); }

        private double integrationShowSinglePictureHeight;
        public double IntegrationShowSinglePictureHeight { get => integrationShowSinglePictureHeight; set => SetValue(ref integrationShowSinglePictureHeight, value); }

        private bool enablePrev;
        public bool EnablePrev { get => enablePrev; set => SetValue(ref enablePrev, value); }

        private bool enableNext;
        public bool EnableNext { get => enableNext; set => SetValue(ref enableNext, value); }

        private bool isVideo;
        public bool IsVideo { get => isVideo; set => SetValue(ref isVideo, value); }

        private string thumbnail;
        public string Thumbnail { get => thumbnail; set => SetValue(ref thumbnail, value); }

        private string pictureSource;
        public string PictureSource { get => pictureSource; set => SetValue(ref pictureSource, value); }

        private string pictureInfos;
        public string PictureInfos { get => pictureInfos; set => SetValue(ref pictureInfos, value); }
    }
}
