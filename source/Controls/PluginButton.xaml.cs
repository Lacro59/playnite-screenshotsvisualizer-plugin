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
using System.Globalization;
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
    /// Logique d'interaction pour PluginButton.xaml
    /// </summary>
    public partial class PluginButton : PluginUserControlExtend
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

        private PluginButtonDataContext ControlDataContext;
        internal override IDataContext _ControlDataContext
        {
            get
            {
                return ControlDataContext;
            }
            set
            {
                ControlDataContext = (PluginButtonDataContext)_ControlDataContext;
            }
        }


        public PluginButton()
        {
            AlwaysShow = false;

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
            ControlDataContext = new PluginButtonDataContext
            {
                IsActivated = PluginDatabase.PluginSettings.Settings.EnableIntegrationButton,
                DisplayDetails = PluginDatabase.PluginSettings.Settings.EnableIntegrationButtonDetails,

                Text = "\uea38",
                SsvDateLast = DateTime.Now,
                SsvTotal = 0
            };
        }


        public override Task<bool> SetData(Game newContext, PluginDataBaseGameBase PluginGameData)
        {
            return Task.Run(() =>
            {
                GameScreenshots gameScreenshots = (GameScreenshots)PluginGameData;

                if (ControlDataContext.DisplayDetails)
                {
                    if (gameScreenshots.HasData)
                    {
                        var tmp = gameScreenshots.Items;
                        tmp.Sort((x, y) => y.Modifed.CompareTo(x.Modifed));
                        DateTime SsvDateLast = tmp[0].Modifed;

                        LocalDateConverter localDateConverter = new LocalDateConverter();

                        ControlDataContext.SsvDateLast = SsvDateLast;
                        ControlDataContext.SsvTotal = gameScreenshots.Items.Count();
                    }
                    else
                    {
                        ControlDataContext.DisplayDetails = false;
                    }
                }
                else
                {
                    ControlDataContext.DisplayDetails = false;
                }

                this.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new ThreadStart(delegate
                {
                    this.DataContext = ControlDataContext;
                }));

                return true;
            });
        }


        #region Events
        private void PART_PluginButton_Click(object sender, RoutedEventArgs e)
        {
            WindowOptions windowOptions = new WindowOptions
            {
                ShowMinimizeButton = false,
                ShowMaximizeButton = true,
                ShowCloseButton = true
            };

            var ViewExtension = new SsvScreenshotsView(PluginDatabase.GameContext);
            Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PluginDatabase.PlayniteApi, resources.GetString("LOCSsvTitle"), ViewExtension, windowOptions);
            windowExtension.ResizeMode = ResizeMode.CanResize;
            windowExtension.ShowDialog();
        }
        #endregion
    }


    public class PluginButtonDataContext : IDataContext
    {
        public bool IsActivated { get; set; }
        public bool DisplayDetails { get; set; } = true;
        public bool ButtonContextMenu { get; set; }

        public string Text { get; set; } = "\uea38";
        public DateTime SsvDateLast { get; set; } = DateTime.Now;
        public int SsvTotal { get; set; } = 7;
    }
}
