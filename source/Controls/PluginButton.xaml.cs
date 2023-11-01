using CommonPluginsShared;
using CommonPluginsShared.Collections;
using CommonPluginsShared.Controls;
using CommonPluginsShared.Converters;
using CommonPluginsShared.Interfaces;
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
        private ScreenshotsVisualizerDatabase PluginDatabase = ScreenshotsVisualizer.PluginDatabase;
        internal override IPluginDatabase _PluginDatabase
        {
            get => PluginDatabase;
            set => PluginDatabase = (ScreenshotsVisualizerDatabase)_PluginDatabase;
        }

        private PluginButtonDataContext ControlDataContext = new PluginButtonDataContext();
        internal override IDataContext _ControlDataContext
        {
            get => ControlDataContext;
            set => ControlDataContext = (PluginButtonDataContext)_ControlDataContext;
        }


        public PluginButton()
        {
            AlwaysShow = false;

            InitializeComponent();
            this.DataContext = ControlDataContext;

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
            Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PluginDatabase.PlayniteApi, resources.GetString("LOCSsvTitle"), ViewExtension, windowOptions);
            windowExtension.ShowDialog();
        }
        #endregion
    }


    public class PluginButtonDataContext : ObservableObject, IDataContext
    {
        private bool _IsActivated;
        public bool IsActivated { get => _IsActivated; set => SetValue(ref _IsActivated, value); }

        private bool _DisplayDetails;
        public bool DisplayDetails { get => _DisplayDetails; set => SetValue(ref _DisplayDetails, value); }

        private bool _ButtonContextMenu;
        public bool ButtonContextMenu { get => _ButtonContextMenu; set => SetValue(ref _ButtonContextMenu, value); }

        private string _Text = "\uea38";
        public string Text { get => _Text; set => SetValue(ref _Text, value); }

        private DateTime _SsvDateLast = DateTime.Now;
        public DateTime SsvDateLast { get => _SsvDateLast; set => SetValue(ref _SsvDateLast, value); }

        private int _SsvTotal = 7;
        public int SsvTotal { get => _SsvTotal; set => SetValue(ref _SsvTotal, value); }
    }
}
