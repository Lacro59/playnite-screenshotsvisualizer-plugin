using CommonPluginsShared;
using CommonPluginsShared.Controls;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.Services;
using ScreenshotsVisualizer.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
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
    /// Logique d'interaction pour SsvButton.xaml
    /// </summary>
    public partial class SsvButton : PluginUserControlExtend
    {
        private ScreenshotsVisualizerDatabase PluginDatabase = ScreenshotsVisualizer.PluginDatabase;


        public SsvButton()
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
            MustDisplay = PluginDatabase.PluginSettings.Settings.EnableIntegrationButton;

            // When control is not used
            if (!PluginDatabase.PluginSettings.Settings.EnableIntegrationButton)
            {
                return;
            }

            LocalDateConverter localDateConverter = new LocalDateConverter();

            bool DisplayDetails = PluginDatabase.PluginSettings.Settings.EnableIntegrationButtonDetails;
            DateTime SsvDateLast = DateTime.Now;
            int SsvTotal = 0;

            if (DisplayDetails && newContext != null)
            {
                GameScreenshots gameScreenshots = PluginDatabase.Get(newContext);

                if (gameScreenshots.HasData)
                {
                    var tmp = gameScreenshots.Items;
                    tmp.Sort((x, y) => y.Modifed.CompareTo(x.Modifed));
                    SsvDateLast = tmp[0].Modifed;

                    SsvTotal = gameScreenshots.Items.Count();
                }
                else
                {
                    DisplayDetails = false;
                }
            }
            else
            {
                DisplayDetails = false;
            }

            this.DataContext = new
            {
                DisplayDetails,
                SsvDateLast = localDateConverter.Convert(SsvDateLast, null, null, CultureInfo.CurrentCulture),
                SsvTotal
            };
        }
        #endregion


        private void PART_SsvButton_Click(object sender, RoutedEventArgs e)
        {
            var ViewExtension = new SsvScreenshotsView(PluginDatabase.PlayniteApi, PluginDatabase.GameContext);
            Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PluginDatabase.PlayniteApi, resources.GetString("LOCSsvTitle"), ViewExtension);
            windowExtension.ShowDialog();
        }
    }
}
