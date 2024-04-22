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
        private ScreenshotsVisualizerDatabase PluginDatabase => ScreenshotsVisualizer.PluginDatabase;
        internal override IPluginDatabase pluginDatabase => PluginDatabase;

        private PluginListScreenshotsDataContext ControlDataContext = new PluginListScreenshotsDataContext();
        internal override IDataContext controlDataContext
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
            ControlDataContext.DateTaked = default;
            if (this.Tag is DateTime)
            {
                ControlDataContext.IsActivated = true;
                ControlDataContext.DateTaked = (DateTime)this.Tag;
            }
        }


        public override void SetData(Game newContext, PluginDataBaseGameBase PluginGameData)
        {
            GameScreenshots gameScreenshots = (GameScreenshots)PluginGameData;

            List<Screenshot> screenshots = gameScreenshots.Items;
            screenshots.Sort((x, y) => y.Modifed.CompareTo(x.Modifed));


            // With PlayerActivities
            if (ControlDataContext.DateTaked != default)
            {
                screenshots = screenshots
                    .Where(x => x.Modifed.ToLocalTime().ToString("yyyy-MM--dd").IsEqual(ControlDataContext.DateTaked.ToString("yyyy-MM--dd")))
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
        private bool isActivated;
        public bool IsActivated { get => isActivated; set => SetValue(ref isActivated, value); }

        private DateTime dateTaked;
        public DateTime DateTaked { get => dateTaked; set => SetValue(ref dateTaked, value); }

        private bool addBorder;
        public bool AddBorder { get => addBorder; set => SetValue(ref addBorder, value); }

        private bool addRoundedCorner;
        public bool AddRoundedCorner { get => addRoundedCorner; set => SetValue(ref addRoundedCorner, value); }

        private bool hideInfos;
        public bool HideInfos { get => hideInfos; set => SetValue(ref hideInfos, value); }

        private double integrationShowPicturesHeight;
        public double IntegrationShowPicturesHeight { get => integrationShowPicturesHeight; set => SetValue(ref integrationShowPicturesHeight, value); }

        private int countItems = 10;
        public int CountItems { get => countItems; set => SetValue(ref countItems, value); }

        private ObservableCollection<Screenshot> itemsSource = new ObservableCollection<Screenshot>
        {
            new Screenshot
            {
                FileName = @"icon.png",
                Modifed = DateTime.Now
            }
        };
        public ObservableCollection<Screenshot> ItemsSource { get => itemsSource; set => SetValue(ref itemsSource, value); }
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
