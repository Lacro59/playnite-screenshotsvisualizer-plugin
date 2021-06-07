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
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
            get
            {
                return PluginDatabase;
            }
            set
            {
                PluginDatabase = (ScreenshotsVisualizerDatabase)_PluginDatabase;
            }
        }

        private PluginSinglePictureDataContext ControlDataContext;
        internal override IDataContext _ControlDataContext
        {
            get
            {
                return ControlDataContext;
            }
            set
            {
                ControlDataContext = (PluginSinglePictureDataContext)_ControlDataContext;
            }
        }

        private List<Screenshot> screenshots = new List<Screenshot>();
        private int index = 0;


        public PluginSinglePicture()
        {
            InitializeComponent();

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
            ControlDataContext = new PluginSinglePictureDataContext
            {
                IsActivated = PluginDatabase.PluginSettings.Settings.EnableIntegrationShowSinglePicture,
                AddBorder = PluginDatabase.PluginSettings.Settings.AddBorderSinglePicture,
                AddRoundedCorner = PluginDatabase.PluginSettings.Settings.AddRoundedCornerSinglePicture,
                IntegrationShowSinglePictureHeight = PluginDatabase.PluginSettings.Settings.IntegrationShowSinglePictureHeight,

                EnablePrev = false,
                EnableNext = false,

                PictureSource = string.Empty,
                PictureInfos = string.Empty
            };
        }


        public override Task<bool> SetData(Game newContext, PluginDataBaseGameBase PluginGameData)
        {
            return Task.Run(() =>
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
                
                this.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new ThreadStart(delegate
                {
                    this.DataContext = ControlDataContext;
                }));

                return true;
            });
        }


        private void SetPicture(Screenshot screenshot)
        {
            string PictureSource = string.Empty;
            string PictureInfos = string.Empty;

            var Converters = new LocalDateTimeConverter();

            if (File.Exists(screenshot.FileName))
            {
                PictureSource = screenshot.FileName;
                PictureInfos = (string)Converters.Convert(screenshot.Modifed, null, null, null);
            }

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
                WindowCreationOptions windowCreationOptions = new WindowCreationOptions
                {
                    ShowMinimizeButton = false,
                    ShowMaximizeButton = true,
                    ShowCloseButton = true,
                };

                var ViewExtension = new SsvSinglePictureView(screenshots[index]);
                Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PluginDatabase.PlayniteApi, resources.GetString("LOCSsv"), ViewExtension, windowCreationOptions);
                windowExtension.ResizeMode = ResizeMode.CanResize;
                windowExtension.Height = 720;
                windowExtension.Width = 1280;
                windowExtension.ShowDialog();
            }
        }
        #endregion
    }


    public class PluginSinglePictureDataContext : IDataContext
    {
        public bool IsActivated { get; set; }
        public bool AddBorder { get; set; }
        public bool AddRoundedCorner { get; set; }
        public double IntegrationShowSinglePictureHeight { get; set; }

        public bool EnablePrev { get; set; }
        public bool EnableNext { get; set; }

        public string PictureSource { get; set; }
        public string PictureInfos { get; set; }
    }
}
