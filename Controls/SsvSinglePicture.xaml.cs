using CommonPluginsShared;
using CommonPluginsShared.Controls;
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
    /// Logique d'interaction pour SsvSinglePicture.xaml
    /// </summary>
    public partial class SsvSinglePicture : PluginUserControlExtend
    {
        private ScreenshotsVisualizerDatabase PluginDatabase = ScreenshotsVisualizer.PluginDatabase;

        private List<Screenshot> screenshots = new List<Screenshot>();
        private int index = 0;


        public SsvSinglePicture()
        {
            InitializeComponent();

            PluginDatabase.PluginSettings.PropertyChanged += PluginSettings_PropertyChanged;
            PluginDatabase.Database.ItemUpdated += Database_ItemUpdated;
            PluginDatabase.Database.ItemCollectionChanged += Database_ItemCollectionChanged;
            PluginDatabase.PlayniteApi.Database.Games.ItemUpdated += Games_ItemUpdated;

            // Apply settings
            PluginSettings_PropertyChanged(null, null);
        }


        #region OnPropertyChange
        // When settings is updated
        public override void PluginSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Apply settings
            this.DataContext = new
            {

            };

            // Publish changes for the currently displayed game
            GameContextChanged(null, GameContext);
        }

        // When game is changed
        public override void GameContextChanged(Game oldContext, Game newContext)
        {
            MustDisplay = PluginDatabase.PluginSettings.Settings.EnableIntegrationShowSinglePicture;

            // When control is not used
            if (!PluginDatabase.PluginSettings.Settings.EnableIntegrationShowSinglePicture)
            {
                return;
            }

            if (newContext != null)
            {
                GameScreenshots gameScreenshots = PluginDatabase.Get(newContext);

                if (!gameScreenshots.HasData)
                {
                    MustDisplay = false;
                    return;
                }

                SetData(gameScreenshots.Items);
            }
        }
        #endregion


        public void SetData(List<Screenshot> screenshots)
        {
            this.screenshots = screenshots;
            this.screenshots.Sort((x, y) => y.Modifed.CompareTo(x.Modifed));

            index = 0;

            if (screenshots.Count > 1)
            {
                PART_Prev.IsEnabled = true;
                PART_Next.IsEnabled = true;
            }
            else
            {
                PART_Prev.IsEnabled = false;
                PART_Next.IsEnabled = false;
            }

            if (screenshots.Count > 0)
            {
                SetPicture(screenshots[index]);
            }
            else
            {
                this.DataContext = new
                {
                    AddBorder = PluginDatabase.PluginSettings.Settings.AddBorderSinglePicture,
                    AddRoundedCorner = PluginDatabase.PluginSettings.Settings.AddRoundedCornerSinglePicture,
                    PluginDatabase.PluginSettings.Settings.IntegrationShowSinglePictureHeight,
                    PictureSource = string.Empty,
                    PictureInfos = string.Empty
                };
            }
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

            this.DataContext = new
            {
                AddBorder = PluginDatabase.PluginSettings.Settings.AddBorderSinglePicture,
                AddRoundedCorner = PluginDatabase.PluginSettings.Settings.AddRoundedCornerSinglePicture,
                PluginDatabase.PluginSettings.Settings.IntegrationShowSinglePictureHeight,
                PictureSource,
                PictureInfos
            };
        }


        public void SetPictureFromList(int index)
        {
            if (index != -1)
            {
                this.index = index;

                SetPicture(screenshots[index]);
            }
        }


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

            SetPicture(screenshots[index]);
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

            SetPicture(screenshots[index]);
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
    }
}
