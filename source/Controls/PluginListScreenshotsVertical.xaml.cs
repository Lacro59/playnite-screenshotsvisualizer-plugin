using CommonPluginsShared;
using CommonPluginsShared.Collections;
using CommonPluginsShared.Controls;
using CommonPluginsShared.Interfaces;
using CommonPluginsShared.UI;
using Playnite.SDK;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;

namespace ScreenshotsVisualizer.Controls
{
    /// <summary>
    /// Logique d'interaction pour PluginListScreenshotsVertical.xaml
    /// </summary>
    public partial class PluginListScreenshotsVertical : PluginUserControlExtend
    {
        private static ScreenshotsVisualizerDatabase PluginDatabase => ScreenshotsVisualizer.PluginDatabase;
        protected override IPluginDatabase pluginDatabase => PluginDatabase;

        private PluginListScreenshotsVerticalDataContext ControlDataContext = new PluginListScreenshotsVerticalDataContext();
        protected override IDataContext controlDataContext
        {
            get => ControlDataContext;
            set => ControlDataContext = (PluginListScreenshotsVerticalDataContext)controlDataContext;
        }


        public PluginListScreenshotsVertical()
        {
            InitializeComponent();
            this.DataContext = ControlDataContext;
            Loaded += OnLoaded;
            PART_ListScreenshots.AddHandler(System.Windows.UIElement.MouseDownEvent, new MouseButtonEventHandler(PluginDatabase.ListBoxItem_MouseLeftButtonDownClick), true);
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
            ControlDataContext.IsActivated = PluginDatabase.PluginSettings.EnableIntegrationShowPicturesVertical;
            ControlDataContext.AddBorder = PluginDatabase.PluginSettings.AddBorder;
            ControlDataContext.AddRoundedCorner = PluginDatabase.PluginSettings.AddRoundedCorner;
            ControlDataContext.HideInfos = PluginDatabase.PluginSettings.HideScreenshotsInfos;

            ControlDataContext.CountItems = 0;
            ControlDataContext.ItemsSource = new ObservableCollection<Screenshot>();
        }

        public override void SetData(Game newContext, PluginGameEntry pluginGameData)
        {
            GameScreenshots gameScreenshots = (GameScreenshots)pluginGameData;

            List<Screenshot> screenshots = gameScreenshots.Items;
            screenshots.Sort((x, y) => y.Modifed.CompareTo(x.Modifed));

            ControlDataContext.ItemsSource = screenshots.ToObservable();
            ControlDataContext.CountItems = screenshots.Count;
        }

        #region Events

        private void PART_ListScreenshots_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PluginDatabase.PluginSettings.LinkWithSinglePicture && PluginDatabase.PluginSettings.EnableIntegrationShowSinglePicture)
            {
                PluginSinglePicture ssvSinglePicture = UIHelper.FindVisualChildren<PluginSinglePicture>(System.Windows.Application.Current.MainWindow).FirstOrDefault();
                ssvSinglePicture?.SetPictureFromList(PART_ListScreenshots.SelectedIndex);
            }
        }

        #endregion
    }


    public class PluginListScreenshotsVerticalDataContext : ObservableObject, IDataContext
    {
        private bool _isActivated;
        public bool IsActivated { get => _isActivated; set => SetValue(ref _isActivated, value); }

        private bool _addBorder;
        public bool AddBorder { get => _addBorder; set => SetValue(ref _addBorder, value); }

        private bool _addRoundedCorner;
        public bool AddRoundedCorner { get => _addRoundedCorner; set => SetValue(ref _addRoundedCorner, value); }

        private bool _hideInfos;
        public bool HideInfos { get => _hideInfos; set => SetValue(ref _hideInfos, value); }

        private int _countItems = 10;
        public int CountItems { get => _countItems; set => SetValue(ref _countItems, value); }

        private ObservableCollection<Screenshot> _itemsSource = new ObservableCollection<Screenshot>
        {
            new Screenshot
            {
                FileName = @"icon.png",
                Modifed = DateTime.Now
            }
        };
        public ObservableCollection<Screenshot> ItemsSource { get => _itemsSource; set => SetValue(ref _itemsSource, value); }
    }
}