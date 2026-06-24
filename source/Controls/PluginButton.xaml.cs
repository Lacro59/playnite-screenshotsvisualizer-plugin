using CommonPluginsShared;
using CommonPluginsShared.Collections;
using CommonPluginsShared.Controls;
using CommonPluginsShared.Interfaces;
using Playnite.SDK;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.Services;
using ScreenshotsVisualizer.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace ScreenshotsVisualizer.Controls
{
    /// <summary>
    /// Logique d'interaction pour PluginButton.xaml
    /// </summary>
    public partial class PluginButton : PluginUserControlExtend
    {
        private static ScreenshotsVisualizerDatabase PluginDatabase => ScreenshotsVisualizer.PluginDatabase;
        protected override IPluginDatabase pluginDatabase => PluginDatabase;

        private PluginButtonDataContext ControlDataContext = new PluginButtonDataContext();
        protected override IDataContext controlDataContext
        {
            get => ControlDataContext;
            set => ControlDataContext = (PluginButtonDataContext)controlDataContext;
        }


        public PluginButton()
        {
            AlwaysShow = false;

            InitializeComponent();
            this.DataContext = ControlDataContext;
            Loaded += OnLoaded;
        }

        protected override void AttachStaticEvents()
        {
            base.AttachStaticEvents();
            AttachPluginEvents(PluginDatabase.PluginName, () =>
            {
                PluginDatabase.PluginSettings.PropertyChanged += CreatePluginSettingsHandler();
                PluginDatabase.DatabaseItemUpdated += CreateDatabaseItemUpdatedHandler<GameScreenshots>();
                PluginDatabase.DatabaseItemCollectionChanged += CreateDatabaseCollectionChangedHandler<GameScreenshots>();
            });
        }

        public override void SetDefaultDataContext()
        {
            ControlDataContext.IsActivated = PluginDatabase.PluginSettings.EnableIntegrationButton;
            ControlDataContext.DisplayDetails = PluginDatabase.PluginSettings.EnableIntegrationButtonDetails;

            ControlDataContext.Text = "\uea38";
            ControlDataContext.SsvDateLast = DateTime.Now;
            ControlDataContext.SsvTotal = 0;
        }

        public override void SetData(Game newContext, PluginGameEntry pluginGameData)
        {
            GameScreenshots gameScreenshots = (GameScreenshots)pluginGameData;

            if (ControlDataContext.DisplayDetails)
            {
                if (gameScreenshots.HasData)
                {
                    List<Screenshot> tmp = gameScreenshots.Items;
                    tmp.Sort((x, y) => y.Modifed.CompareTo(x.Modifed));
                    DateTime SsvDateLast = tmp[0].Modifed;

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
            PluginDatabase.PluginWindows.ShowPluginGameDataWindow(PluginDatabase.GameContext);
        }

        #endregion
    }


    public class PluginButtonDataContext : ObservableObject, IDataContext
    {
        private bool _isActivated;
        public bool IsActivated { get => _isActivated; set => SetValue(ref _isActivated, value); }

        private bool _displayDetails;
        public bool DisplayDetails { get => _displayDetails; set => SetValue(ref _displayDetails, value); }

        private bool _buttonContextMenu;
        public bool ButtonContextMenu { get => _buttonContextMenu; set => SetValue(ref _buttonContextMenu, value); }

        private string _text = "\uea38";
        public string Text { get => _text; set => SetValue(ref _text, value); }

        private DateTime _ssvDateLast = DateTime.Now;
        public DateTime SsvDateLast { get => _ssvDateLast; set => SetValue(ref _ssvDateLast, value); }

        private int _ssvTotal = 7;
        public int SsvTotal { get => _ssvTotal; set => SetValue(ref _ssvTotal, value); }
    }
}