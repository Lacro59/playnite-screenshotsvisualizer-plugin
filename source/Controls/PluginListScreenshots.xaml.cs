using CommonPluginsShared;
using CommonPluginsShared.Collections;
using CommonPluginsShared.Controls;
using CommonPluginsShared.Interfaces;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
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
        private ScreenshotsVisualizerDatabase PluginDatabase = ScreenshotsVisualizer.PluginDatabase;
        internal override IPluginDatabase _PluginDatabase
        {
            get => PluginDatabase;
            set => PluginDatabase = (ScreenshotsVisualizerDatabase)_PluginDatabase;
        }

        private PluginListScreenshotsDataContext ControlDataContext = new PluginListScreenshotsDataContext();
        internal override IDataContext _ControlDataContext
        {
            get => ControlDataContext;
            set => ControlDataContext = (PluginListScreenshotsDataContext)_ControlDataContext;
        }


        public PluginListScreenshots()
        {
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
        }


        public override void SetData(Game newContext, PluginDataBaseGameBase PluginGameData)
        {
            GameScreenshots gameScreenshots = (GameScreenshots)PluginGameData;

            List<Screenshot> screenshots = gameScreenshots.Items;
            screenshots.Sort((x, y) => y.Modifed.CompareTo(x.Modifed));

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

                if (ssvSinglePicture != null)
                {
                    ssvSinglePicture.SetPictureFromList(PART_ListScreenshots.SelectedIndex);
                }
            }
        }
        #endregion
    }


    public class PluginListScreenshotsDataContext : ObservableObject, IDataContext
    {
        private bool _IsActivated;
        public bool IsActivated { get => _IsActivated; set => SetValue(ref _IsActivated, value); }

        private bool _AddBorder;
        public bool AddBorder { get => _AddBorder; set => SetValue(ref _AddBorder, value); }

        private bool _AddRoundedCorner;
        public bool AddRoundedCorner { get => _AddRoundedCorner; set => SetValue(ref _AddRoundedCorner, value); }

        private bool _HideInfos;
        public bool HideInfos { get => _HideInfos; set => SetValue(ref _HideInfos, value); }

        private double _IntegrationShowPicturesHeight;
        public double IntegrationShowPicturesHeight { get => _IntegrationShowPicturesHeight; set => SetValue(ref _IntegrationShowPicturesHeight, value); }

        private int _CountItems = 10;
        public int CountItems { get => _CountItems; set => SetValue(ref _CountItems, value); }

        private ObservableCollection<Screenshot> _ItemsSource = new ObservableCollection<Screenshot>
        {
            new Screenshot
            {
                FileName = @"icon.png",
                Modifed = DateTime.Now
            }
        };
        public ObservableCollection<Screenshot> ItemsSource { get => _ItemsSource; set => SetValue(ref _ItemsSource, value); }
    }

    public class TwoSizeMultiValueConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            double returnValue = 0;

            if (parameter == null || ((parameter is string && (string)parameter == "+")))
            {
                returnValue = (double)values[0] + (double)values[1];
            }
            else
            {
                returnValue = (double)values[0] - (double)values[1] - 25;
            }

            return returnValue;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
