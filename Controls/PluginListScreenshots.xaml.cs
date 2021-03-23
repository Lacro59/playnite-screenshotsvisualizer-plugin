using CommonPluginsShared;
using CommonPluginsShared.Controls;
using Playnite.SDK;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.Services;
using ScreenshotsVisualizer.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
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

namespace ScreenshotsVisualizer.Controls
{
    /// <summary>
    /// Logique d'interaction pour PluginListScreenshots.xaml
    /// </summary>
    public partial class PluginListScreenshots : PluginUserControlExtend
    {
        private ScreenshotsVisualizerDatabase PluginDatabase = ScreenshotsVisualizer.PluginDatabase;


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


        #region OnPropertyChange
        // When settings is updated
        public override void PluginSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Apply settings
            this.DataContext = new
            {

            };

            // Publish changes for the currently displayed game
            GameContextChanged(null, GameContext);
        }

        // When game is changed
        public override void GameContextChanged(Game oldContext, Game newContext)
        {
            if (!PluginDatabase.IsLoaded)
            {
                return;
            }

            MustDisplay = PluginDatabase.PluginSettings.Settings.EnableIntegrationShowPictures;

            // When control is not used
            if (!PluginDatabase.PluginSettings.Settings.EnableIntegrationShowPictures)
            {
                return;
            }

            if (newContext != null)
            {
                GameScreenshots gameScreenshots = PluginDatabase.Get(newContext);

                if (!gameScreenshots.HasData)
                {
                    MustDisplay = false;
                    return;
                }

                SetData(gameScreenshots.Items);
            }
        }
        #endregion


        public void SetData(List<Screenshot> screenshots)
        {
            screenshots.Sort((x, y) => y.Modifed.CompareTo(x.Modifed));

            PART_ListScreenshots.ItemsSource = null;
            PART_ListScreenshots.Items.Clear();
            PART_ListScreenshots.ItemsSource = screenshots;

            this.DataContext = new
            {
                PluginDatabase.PluginSettings.Settings.AddBorder,
                PluginDatabase.PluginSettings.Settings.AddRoundedCorner,
                PluginDatabase.PluginSettings.Settings.IntegrationShowPicturesHeight,
                CountItems = screenshots.Count
            };
        }


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
                    WindowCreationOptions windowCreationOptions = new WindowCreationOptions
                    {
                        ShowMinimizeButton = false,
                        ShowMaximizeButton = true,
                        ShowCloseButton = true
                    };

                    var ViewExtension = new SsvSinglePictureView(screenshot);
                    Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PluginDatabase.PlayniteApi, resources.GetString("LOCSsv"), ViewExtension, windowCreationOptions);
                    windowExtension.ResizeMode = ResizeMode.CanResize;
                    windowExtension.Height = 720;
                    windowExtension.Width = 1280;
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
                PluginSinglePicture ssvSinglePicture = Tools.FindVisualChildren<PluginSinglePicture>(Application.Current.MainWindow).FirstOrDefault();

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
