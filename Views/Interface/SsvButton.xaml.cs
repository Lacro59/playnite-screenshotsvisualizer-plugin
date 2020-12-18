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
    /// Logique d'interaction pour SsvButton.xaml
    /// </summary>
    public partial class SsvButton : Button
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private ScreenshotsVisualizerDatabase PluginDatabase = ScreenshotsVisualizer.PluginDatabase;

        bool? _JustIcon = null;


        public SsvButton(bool? JustIcon = null)
        {
            _JustIcon = JustIcon;

            InitializeComponent();


            bool EnableIntegrationButtonJustIcon;
            if (_JustIcon == null)
            {
                EnableIntegrationButtonJustIcon = PluginDatabase.PluginSettings.EnableIntegrationInDescriptionOnlyIcon;
            }
            else
            {
                EnableIntegrationButtonJustIcon = (bool)_JustIcon;
            }

            this.DataContext = new
            {
                EnableIntegrationButtonJustIcon = EnableIntegrationButtonJustIcon
            };


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
                        bool EnableIntegrationButtonJustIcon;
                        if (_JustIcon == null)
                        {
                            EnableIntegrationButtonJustIcon = PluginDatabase.PluginSettings.EnableIntegrationInDescriptionOnlyIcon;
                        }
                        else
                        {
                            EnableIntegrationButtonJustIcon = (bool)_JustIcon;
                        }

                        this.DataContext = new
                        {
                            EnableIntegrationButtonJustIcon = EnableIntegrationButtonJustIcon
                        };
                    }));
                }
                else
                {
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "ScreenshotsVisualizer");
            }
        }
    }
}
