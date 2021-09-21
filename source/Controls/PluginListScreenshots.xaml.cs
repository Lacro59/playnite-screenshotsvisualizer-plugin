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
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    /// Logique d'interaction pour PluginListScreenshots.xaml
    /// </summary>
    public partial class PluginListScreenshots : PluginUserControlExtend
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

        private PluginListScreenshotsDataContext ControlDataContext;
        internal override IDataContext _ControlDataContext
        {
            get
            {
                return ControlDataContext;
            }
            set
            {
                ControlDataContext = (PluginListScreenshotsDataContext)_ControlDataContext;
            }
        }


        public PluginListScreenshots()
        {
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

                    PART_ListScreenshots.AddHandler(UIElement.MouseDownEvent, new MouseButtonEventHandler(ListBoxItem_MouseLeftButtonDownClick), true);
                });
            });
        }


        public override void SetDefaultDataContext()
        {
            ControlDataContext = new PluginListScreenshotsDataContext
            {
                IsActivated = PluginDatabase.PluginSettings.Settings.EnableIntegrationShowPictures,
                AddBorder = PluginDatabase.PluginSettings.Settings.AddBorder,
                AddRoundedCorner = PluginDatabase.PluginSettings.Settings.AddRoundedCorner,
                IntegrationShowPicturesHeight = PluginDatabase.PluginSettings.Settings.IntegrationShowPicturesHeight,

                CountItems = 0,
                ItemsSource = new ObservableCollection<Screenshot>()
            };
        }


        public override Task<bool> SetData(Game newContext, PluginDataBaseGameBase PluginGameData)
        {
            return Task.Run(() =>
            {
                GameScreenshots gameScreenshots = (GameScreenshots)PluginGameData;

                List<Screenshot> screenshots = gameScreenshots.Items;
                screenshots.Sort((x, y) => y.Modifed.CompareTo(x.Modifed));

                ControlDataContext.ItemsSource = screenshots.ToObservable();
                ControlDataContext.CountItems = screenshots.Count;

                this.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new ThreadStart(delegate
                {
                    this.DataContext = ControlDataContext;
                }));

                return true;
            });
        }


        #region Events
        private void ListBoxItem_MouseLeftButtonDownClick(object sender, MouseButtonEventArgs e)
        {
            ListBoxItem item = ItemsControl.ContainerFromElement(PART_ListScreenshots, e.OriginalSource as DependencyObject) as ListBoxItem;
            if (item != null)
            {
                int index = PART_ListScreenshots.SelectedIndex;
                Screenshot screenshot = ((Screenshot)PART_ListScreenshots.Items[index]);

                bool IsGood = false;

                if (PluginDatabase.PluginSettings.Settings.OpenViewerWithOnSelection)
                {
                    IsGood = true;
                }
                else
                {
                    if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
                    {
                        IsGood = true;
                    }
                }


                if (IsGood)
                {
                    WindowOptions windowOptions = new WindowOptions
                    {
                        ShowMinimizeButton = false,
                        ShowMaximizeButton = true,
                        ShowCloseButton = true,
                        Height = 720,
                        Width = 1280
                    };

                    var ViewExtension = new SsvSinglePictureView(screenshot);
                    Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PluginDatabase.PlayniteApi, resources.GetString("LOCSsv"), ViewExtension, windowOptions);
                    windowExtension.ResizeMode = ResizeMode.CanResize;
                    windowExtension.ShowDialog();
                }
            }
            else
            {

            }
        }

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


    public class PluginListScreenshotsDataContext : IDataContext
    {
        public bool IsActivated { get; set; }
        public bool AddBorder { get; set; }
        public bool AddRoundedCorner { get; set; }
        public double IntegrationShowPicturesHeight { get; set; }

        public int CountItems { get; set; }
        public ObservableCollection<Screenshot> ItemsSource { get; set; }
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
