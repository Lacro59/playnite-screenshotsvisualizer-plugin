using Playnite.SDK;
using PluginCommon;
using ScreenshotsVisualizer.Services;
using System;
using System.Collections.Generic;
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
    /// Logique d'interaction pour SsvDescriptionIntegration.xaml
    /// </summary>
    public partial class SsvDescriptionIntegration : StackPanel
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private ScreenshotsVisualizerDatabase PluginDatabase = ScreenshotsVisualizer.PluginDatabase;


        public SsvDescriptionIntegration(IPlayniteAPI PlayniteApi)
        {
            InitializeComponent();

            if (PluginDatabase.PluginSettings.IntegrationShowSinglePicture)
            {
                SsvSinglePicture ssvSinglePicture = new SsvSinglePicture(PlayniteApi);
                PART_SsvSinglePicture.Children.Add(ssvSinglePicture);
            }

            if (PluginDatabase.PluginSettings.IntegrationShowPictures)
            {
                SsvListScreenshots ssvListScreenshots = new SsvListScreenshots(PlayniteApi);
                PART_SsvListScreenshots.Children.Add(ssvListScreenshots);
            }

            PluginDatabase.PropertyChanged += OnPropertyChanged;
        }


        protected void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            try
            {
                if (e.PropertyName == "GameSelectedData" || e.PropertyName == "PluginSettings")
                {
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new ThreadStart(delegate
                    {
                        if (PluginDatabase.GameSelectedData.HasData)
                        {
                            this.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            this.Visibility = Visibility.Collapsed;
                            return;
                        }


                        // Margin with title
                        if (PluginDatabase.PluginSettings.IntegrationShowTitle)
                        {
                            PART_SsvSinglePicture.Margin = new Thickness(0, 5, 0, 5);
                        }
                        // Without title
                        else
                        {
                            if (PluginDatabase.PluginSettings.IntegrationShowSinglePicture)
                            {
                                PART_SsvSinglePicture.Margin = new Thickness(0, 5, 0, 5);
                            }
                        }


                        PART_SsvSinglePicture.Height = PluginDatabase.PluginSettings.IntegrationShowSinglePictureHeight;

                        PART_SsvListScreenshots.Height = PluginDatabase.PluginSettings.IntegrationShowPicturesHeight;


                        this.DataContext = new
                        {
                            IntegrationShowTitle = PluginDatabase.PluginSettings.IntegrationShowTitle,
                            IntegrationShowSinglePicture = PluginDatabase.PluginSettings.IntegrationShowSinglePicture,
                            IntegrationShowPictures = PluginDatabase.PluginSettings.IntegrationShowPictures
                        };
                    }));
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "ScreenshotsVisualizer");
            }
        }
    }
}
