using Playnite.SDK;
using PluginCommon;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.Services;
using System;
using System.Collections.Generic;
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

namespace ScreenshotsVisualizer.Views.Interface
{
    /// <summary>
    /// Logique d'interaction pour SsvSinglePicture.xaml
    /// </summary>
    public partial class SsvSinglePicture : UserControl
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private static IResourceProvider resources = new ResourceProvider();

        private static IPlayniteAPI _PlayniteApi;

        private ScreenshotsVisualizerDatabase PluginDatabase = ScreenshotsVisualizer.PluginDatabase;

        private List<Screenshot> screenshots = new List<Screenshot>();
        private int index = 0;

        public SsvSinglePicture(IPlayniteAPI PlayniteApi)
        {
            _PlayniteApi = PlayniteApi;

            InitializeComponent();

            PluginDatabase.PropertyChanged += OnPropertyChanged;
        }


        protected void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            try
            {
                if (e.PropertyName == "GameSelectedData" || e.PropertyName == "PluginSettings")
                {
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Background, new ThreadStart(delegate
                    {
                        SetData(PluginDatabase.GameSelectedData.Items);

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
                    }));
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "ScreenshotsVisualizer");
            }
        }


        public void SetData(List<Screenshot> screenshots)
        {
            this.screenshots = screenshots;
            this.screenshots.Sort((x, y) => y.Modifed.CompareTo(x.Modifed));

            index = 0;

            if (screenshots.Count > 0)
            {
                SetPicture(screenshots[index]);
            }
            else
            {
                this.DataContext = new
                {
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
                PictureSource = PictureSource,
                PictureInfos = PictureInfos
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


        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            IntegrationUI.SetControlSize((FrameworkElement)sender);
        }


        private void PART_Contener_MouseDown(object sender, MouseButtonEventArgs e)
        {
            bool IsGood = false;

            if (PluginDatabase.PluginSettings.OpenViewerWithOnSelection)
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
                    ShowCloseButton = true
                };

                var ViewExtension = new SsvSinglePictureView(screenshots[index]);
                Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(_PlayniteApi, resources.GetString("LOCSsv"), ViewExtension, windowCreationOptions);
                windowExtension.ShowDialog();
            }
        }
    }
}
