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
    /// Logique d'interaction pour SsvButtonDetails.xaml
    /// </summary>
    public partial class SsvButtonDetails : Button
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private static IResourceProvider resources = new ResourceProvider();

        private ScreenshotsVisualizerDatabase PluginDatabase = ScreenshotsVisualizer.PluginDatabase;


        public SsvButtonDetails()
        {
            InitializeComponent();

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
                        Ssv_LabelTitle.Content = resources.GetString("LOCSsvTitle") + " - " + PluginDatabase.GameSelectedData.Items.Count;
                        
                        if (PluginDatabase.GameSelectedData.Items.Count > 0)
                        {
                            var Converters = new LocalDateTimeConverter();

                            var tmp = PluginDatabase.GameSelectedData.Items;
                            tmp.Sort((x, y) => y.Modifed.CompareTo(x.Modifed));

                            Ssv_labelButton.Content = Converters.Convert(tmp[0].Modifed, null, null, null);
                        }
                        else
                        {
                            Ssv_labelButton.Content = string.Empty;
                        }
                    }));
                }
                else
                {
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "ScreenshotVisualizer");
            }
        }
    }
}
