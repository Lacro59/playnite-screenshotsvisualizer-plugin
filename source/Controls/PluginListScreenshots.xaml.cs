using CommonPluginsShared;
using CommonPluginsShared.Collections;
using CommonPluginsShared.Controls;
using CommonPluginsShared.Extensions;
using CommonPluginsShared.Interfaces;
using Playnite.SDK;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace ScreenshotsVisualizer.Controls
{
    /// <summary>
    /// Logique d'interaction pour PluginListScreenshots.xaml
    /// </summary>
    public partial class PluginListScreenshots : PluginUserControlExtend
    {
        private static ScreenshotsVisualizerDatabase PluginDatabase => ScreenshotsVisualizer.PluginDatabase;
        protected override IPluginDatabase pluginDatabase => PluginDatabase;

        private PluginListScreenshotsDataContext ControlDataContext = new PluginListScreenshotsDataContext();
        protected override IDataContext controlDataContext
        {
            get => ControlDataContext;
            set => ControlDataContext = (PluginListScreenshotsDataContext)controlDataContext;
        }


        public PluginListScreenshots()
        {
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

                    PART_ListScreenshots.AddHandler(UIElement.MouseDownEvent, new MouseButtonEventHandler(PluginDatabase.ListBoxItem_MouseLeftButtonDownClick), true);
                });
            });
        }

        public override void SetDefaultDataContext()
        {
            ControlDataContext.IsActivated = PluginDatabase.PluginSettings.Settings.EnableIntegrationShowPictures;
            ControlDataContext.AddBorder = PluginDatabase.PluginSettings.Settings.AddBorder;
            ControlDataContext.AddRoundedCorner = PluginDatabase.PluginSettings.Settings.AddRoundedCorner;
            ControlDataContext.HideInfos = PluginDatabase.PluginSettings.Settings.HideScreenshotsInfos;
            ControlDataContext.IntegrationShowPicturesHeight = PluginDatabase.PluginSettings.Settings.IntegrationShowPicturesHeight;

            ControlDataContext.CountItems = 0;
            ControlDataContext.ItemsSource = new ObservableCollection<Screenshot>();


            // With PlayerActivities
            ControlDataContext.DateTaken = default;
            if (this.Tag is DateTime)
            {
                ControlDataContext.IsActivated = true;
                ControlDataContext.DateTaken = (DateTime)this.Tag;
            }
        }

        public override void SetData(Game newContext, PluginDataBaseGameBase pluginGameData)
        {
            GameScreenshots gameScreenshots = (GameScreenshots)pluginGameData;

            List<Screenshot> screenshots = gameScreenshots.Items;
            screenshots.Sort((x, y) => y.Modifed.CompareTo(x.Modifed));


            // With PlayerActivities
            if (ControlDataContext.DateTaken != default)
            {
                screenshots = screenshots
                    .Where(x => x.Modifed.ToLocalTime().ToString("yyyy-MM--dd").IsEqual(ControlDataContext.DateTaken.ToString("yyyy-MM--dd")))
                    .ToList();
            }


            ControlDataContext.ItemsSource = screenshots.ToObservable();
            ControlDataContext.CountItems = screenshots.Count;
        }

        #region Events

        private void VirtualizingStackPanel_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                ((VirtualizingStackPanel)sender).LineLeft();
            }
            else
            {
                ((VirtualizingStackPanel)sender).LineRight();
            }
            e.Handled = true;
        }

        private void PART_ListScreenshots_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PluginDatabase.PluginSettings.Settings.LinkWithSinglePicture && PluginDatabase.PluginSettings.Settings.EnableIntegrationShowSinglePicture)
            {
                PluginSinglePicture ssvSinglePicture = UI.FindVisualChildren<PluginSinglePicture>(Application.Current.MainWindow).FirstOrDefault();
                ssvSinglePicture?.SetPictureFromList(PART_ListScreenshots.SelectedIndex);
            }
        }
        
        #endregion
    }


    public class PluginListScreenshotsDataContext : ObservableObject, IDataContext
    {
        private bool _isActivated;
        public bool IsActivated { get => _isActivated; set => SetValue(ref _isActivated, value); }

        private DateTime _dateTaken;
        public DateTime DateTaken { get => _dateTaken; set => SetValue(ref _dateTaken, value); }

        private bool _addBorder;
        public bool AddBorder { get => _addBorder; set => SetValue(ref _addBorder, value); }

        private bool _addRoundedCorner;
        public bool AddRoundedCorner { get => _addRoundedCorner; set => SetValue(ref _addRoundedCorner, value); }

        private bool _hideInfos;
        public bool HideInfos { get => _hideInfos; set => SetValue(ref _hideInfos, value); }

        private double _integrationShowPicturesHeight;
        public double IntegrationShowPicturesHeight { get => _integrationShowPicturesHeight; set => SetValue(ref _integrationShowPicturesHeight, value); }

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


    public class TwoSizeMultiValueConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            double returnValue = parameter == null || (parameter is string && (string)parameter == "+")
                ? (double)values[0] + (double)values[1]
                : (double)values[0] - (double)values[1] - 25;
            return returnValue;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}