using Playnite.SDK;
using Playnite.SDK.Models;
using PluginCommon;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.Services;
using System;
using System.Collections.Generic;
using System.IO;
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

namespace ScreenshotsVisualizer.Views
{
    /// <summary>
    /// Logique d'interaction pour SsvScreenshotsView.xaml
    /// </summary>
    public partial class SsvScreenshotsView : UserControl
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private static IResourceProvider resources = new ResourceProvider();

        private IPlayniteAPI _PlayniteApi;

        private ScreenshotsVisualizerDatabase PluginDatabase = ScreenshotsVisualizer.PluginDatabase;

        private GameScreenshots gameScreenshots;

        public SsvScreenshotsView(IPlayniteAPI PlayniteApi, Game GameSelected)
        {
            _PlayniteApi = PlayniteApi;

            InitializeComponent();

            gameScreenshots = PluginDatabase.Get(GameSelected);
            var Items = gameScreenshots.Items;
            Items.Sort((x, y) => y.Modifed.CompareTo(x.Modifed));

            PART_ListScreenshots.ItemsSource = Items;
        }


        private void PART_ListScreenshots_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Screenshot screenshot = (Screenshot)PART_ListScreenshots.SelectedItem;

            if (File.Exists(screenshot.FileName))
            {
                TgaConverter tgaConverter = new TgaConverter();
                PART_Screenshot.Source = (BitmapImage)tgaConverter.Convert(screenshot.FileName, null, null, null);
                //PART_Screenshot.Source = new BitmapImage(new Uri(screenshot.FileName));
            }
        }

        private void PART_BtDelete_Click(object sender, RoutedEventArgs e)
        {
            int index = int.Parse(((Button)sender).Tag.ToString());

            Screenshot screenshot = (Screenshot)PART_ListScreenshots.Items[index];

            var RessultDialog = _PlayniteApi.Dialogs.ShowMessage(
                string.Format(resources.GetString("LOCSsvDeleteConfirm"), screenshot.FileNameOnly),
                "ScreenshotsVisualizer",
                MessageBoxButton.YesNo
            );

            if (RessultDialog == MessageBoxResult.Yes)
            {
                try
                {
                    if (File.Exists(screenshot.FileName))
                    {
                        PART_ListScreenshots.SelectedIndex = -1;
                        PART_ListScreenshots.ItemsSource = null;

                        PART_Screenshot.Source = null;
                        PART_Screenshot.UpdateLayout();
                        var tmp = ((Image)((Grid)((Grid)((Button)sender).Parent).Parent).FindName("PART_Picture"));
                        tmp.Source = null;
                        tmp.UpdateLayout();

                        System.GC.Collect();
                        System.GC.WaitForPendingFinalizers();

                        Task.Run(() => 
                        {
                            while(IsFileLocked(new FileInfo(screenshot.FileName)))
                            {

                            }

                            File.Delete(screenshot.FileName);
                        });

                        gameScreenshots.Items.Remove(screenshot);
                        PluginDatabase.Update(gameScreenshots);
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, "ScreenshotsVisualizer");
                }

                var Items = gameScreenshots.Items;
                Items.Sort((x, y) => y.Modifed.CompareTo(x.Modifed));

                PART_ListScreenshots.ItemsSource = Items;
            }
            else
            {

            }
        }

        protected bool IsFileLocked(FileInfo file)
        {
            FileStream stream = null;

            try
            {
                stream = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            }
            finally
            {
                if (stream != null)
                    stream.Close();
            }

            //file is not locked
            return false;
        }
    }
}
