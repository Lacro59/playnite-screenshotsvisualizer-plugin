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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ScreenshotsVisualizer.Controls
{
    /// <summary>
    /// Logique d'interaction pour PluginButton.xaml
    /// </summary>
    public partial class PluginButton : PluginUserControlExtend
    {
        private ScreenshotsVisualizerDatabase PluginDatabase => ScreenshotsVisualizer.PluginDatabase;
        internal override IPluginDatabase pluginDatabase => PluginDatabase;

        private PluginButtonDataContext ControlDataContext = new PluginButtonDataContext();
        internal override IDataContext controlDataContext
        {
            get => ControlDataContext;
            set => ControlDataContext = (PluginButtonDataContext)controlDataContext;
        }


        public PluginButton()
        {
            AlwaysShow = false;

            InitializeComponent();
            this.DataContext = ControlDataContext;

            _ = Task.Run(() =>
            {
                // Wait extension database are loaded
                _ = System.Threading.SpinWait.SpinUntil(() => PluginDatabase.IsLoaded, -1);

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
            ControlDataContext.IsActivated = PluginDatabase.PluginSettings.Settings.EnableIntegrationButton;
            ControlDataContext.DisplayDetails = PluginDatabase.PluginSettings.Settings.EnableIntegrationButtonDetails;

            ControlDataContext.Text = "\uea38";
            ControlDataContext.SsvDateLast = DateTime.Now;
            ControlDataContext.SsvTotal = 0;
        }


        public override void SetData(Game newContext, PluginDataBaseGameBase PluginGameData)
        {
            GameScreenshots gameScreenshots = (GameScreenshots)PluginGameData;

            if (ControlDataContext.DisplayDetails)
            {
                if (gameScreenshots.HasData)
                {
                    List<Screenshot> tmp = gameScreenshots.Items;
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
        }


        #region Events
        private void PART_PluginButton_Click(object sender, RoutedEventArgs e)
        {
            WindowOptions windowOptions = new WindowOptions
            {
                ShowMinimizeButton = false,
                ShowMaximizeButton = true,
                ShowCloseButton = true,
                CanBeResizable = true,
                Height = 720,
                Width = 1200
            };

            SsvScreenshotsView ViewExtension = new SsvScreenshotsView(PluginDatabase.GameContext);
            Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(ResourceProvider.GetString("LOCSsvTitle"), ViewExtension, windowOptions);
            windowExtension.ShowDialog();
        }
        #endregion
    }


    public class PluginButtonDataContext : ObservableObject, IDataContext
    {
        private bool isActivated;
        public bool IsActivated { get => isActivated; set => SetValue(ref isActivated, value); }

        private bool displayDetails;
        public bool DisplayDetails { get => displayDetails; set => SetValue(ref displayDetails, value); }

        private bool buttonContextMenu;
        public bool ButtonContextMenu { get => buttonContextMenu; set => SetValue(ref buttonContextMenu, value); }

        private string text = "\uea38";
        public string Text { get => text; set => SetValue(ref text, value); }

        private DateTime ssvDateLast = DateTime.Now;
        public DateTime SsvDateLast { get => ssvDateLast; set => SetValue(ref ssvDateLast, value); }

        private int ssvTotal = 7;
        public int SsvTotal { get => ssvTotal; set => SetValue(ref ssvTotal, value); }
    }
}
