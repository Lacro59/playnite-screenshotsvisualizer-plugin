using ScreenshotsVisualizer.Services;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ScreenshotsVisualizer.Views.StartPage
{
    /// <summary>
    /// Logique d'interaction pour SsvCarouselSettings.xaml
    /// </summary>
    public partial class SsvCarouselSettings : UserControl
    {
        private ScreenshotsVisualizer Plugin { get; }
        private static ScreenshotsVisualizerDatabase PluginDatabase { get; set; } = ScreenshotsVisualizer.PluginDatabase;

        private List<string> SearchSources { get; set; } = new List<string>();


        public SsvCarouselSettings(ScreenshotsVisualizer plugin)
        {
            InitializeComponent();

            this.Plugin = plugin;
            this.DataContext = PluginDatabase.PluginSettings;

            PluginDatabase.PluginSettings.Settings.ssvCarouselOptions.SourcesList.Where(x => x.IsCheck)?.ForEach(x => 
            {
                SearchSources.Add(x.Name);
            });
            
            if (SearchSources.Count != 0)
            {
                FilterSource.Text = string.Join(", ", SearchSources);
            }
        }

        private void Grid_Unloaded(object sender, RoutedEventArgs e)
        {
            Plugin.SavePluginSettings(PluginDatabase.PluginSettings.Settings);
            PluginDatabase.PluginSettings.OnPropertyChanged();
        }

        private void ChkSource_Checked(object sender, RoutedEventArgs e)
        {
            FilterCbSource((CheckBox)sender);
        }

        private void ChkSource_Unchecked(object sender, RoutedEventArgs e)
        {
            FilterCbSource((CheckBox)sender);
        }

        private void FilterCbSource(CheckBox sender)
        {
            FilterSource.Text = string.Empty;

            int idx = PluginDatabase.PluginSettings.Settings.ssvCarouselOptions.SourcesList.FindIndex(x => x.Name == (string)sender.Tag);
            if (idx > -1)
            {
                PluginDatabase.PluginSettings.Settings.ssvCarouselOptions.SourcesList[idx].IsCheck = (bool)sender.IsChecked;
            }

            if ((bool)sender.IsChecked)
            {
                SearchSources.Add((string)sender.Tag);
            }
            else
            {
                SearchSources.Remove((string)sender.Tag);
            }

            if (SearchSources.Count != 0)
            {
                FilterSource.Text = string.Join(", ", SearchSources);
            }
        }
    }
}