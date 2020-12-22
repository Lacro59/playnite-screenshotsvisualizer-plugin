using Playnite.SDK;
using PluginCommon;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.Services;
using System;
using System.Collections.Generic;
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

namespace ScreenshotsVisualizer.Views.Interface
{
    /// <summary>
    /// Logique d'interaction pour SsvListScreenshots.xaml
    /// </summary>
    public partial class SsvListScreenshots : UserControl
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private static IResourceProvider resources = new ResourceProvider();

        private static IPlayniteAPI _PlayniteApi;

        private ScreenshotsVisualizerDatabase PluginDatabase = ScreenshotsVisualizer.PluginDatabase;


        public SsvListScreenshots(IPlayniteAPI PlayniteApi)
        {
            _PlayniteApi = PlayniteApi;

            InitializeComponent();

            PART_ListScreenshots.AddHandler(UIElement.MouseDownEvent, new MouseButtonEventHandler(ListBoxItem_MouseLeftButtonDownClick), true);
        

        PluginDatabase.PropertyChanged += OnPropertyChanged;
        }


        protected void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            try
            {
                if (e.PropertyName == "GameSelectedData" || e.PropertyName == "PluginSettings")
                {
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Background, new ThreadStart(delegate
                    {
                        SetData(PluginDatabase.GameSelectedData.Items);
                    }));
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, "ScreenshotsVisualizer");
            }
        }


        public void SetData(List<Screenshot> screenshots)
        {
            screenshots.Sort((x, y) => y.Modifed.CompareTo(x.Modifed));

            PART_ListScreenshots.ItemsSource = null;
            PART_ListScreenshots.Items.Clear();
            PART_ListScreenshots.ItemsSource = screenshots;

            this.DataContext = new
            {
                CountItems = screenshots.Count
            };
        }


        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            IntegrationUI.SetControlSize((FrameworkElement)sender);
        }


        private void ListBoxItem_MouseLeftButtonDownClick(object sender, MouseButtonEventArgs e)
        {
            ListBoxItem item = ItemsControl.ContainerFromElement(PART_ListScreenshots, e.OriginalSource as DependencyObject) as ListBoxItem;
            if (item != null)
            {
                int index = PART_ListScreenshots.SelectedIndex;
                Screenshot screenshot = ((Screenshot)PART_ListScreenshots.Items[index]);

                bool IsGood = false;

                if (PluginDatabase.PluginSettings.OpenViewerWithOnSelection)
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
                    WindowCreationOptions windowCreationOptions = new WindowCreationOptions
                    {
                        ShowMinimizeButton = false,
                        ShowMaximizeButton = true,
                        ShowCloseButton = true
                    };

                    var ViewExtension = new SsvSinglePictureView(screenshot);
                    Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(_PlayniteApi, resources.GetString("LOCSsv"), ViewExtension, windowCreationOptions);
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
                ((VirtualizingStackPanel)sender).LineLeft();
            else
                ((VirtualizingStackPanel)sender).LineRight();
            e.Handled = true;
        }

        private void PART_ListScreenshots_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PluginDatabase.PluginSettings.LinkWithSinglePicture && PluginDatabase.PluginSettings.IntegrationShowSinglePicture)
            {
                SsvSinglePicture ssvSinglePicture = Tools.FindVisualChildren<SsvSinglePicture>(Application.Current.MainWindow).FirstOrDefault();

                if (ssvSinglePicture != null)
                {
                    ssvSinglePicture.SetPictureFromList(PART_ListScreenshots.SelectedIndex);
                }
            }
        }
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
