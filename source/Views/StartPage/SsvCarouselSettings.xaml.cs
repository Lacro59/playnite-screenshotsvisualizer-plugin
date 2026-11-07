using CommonPluginsShared;
using CommonPluginsShared.Extensions;
using Playnite.SDK;
using ScreenshotsVisualizer.Models.StartPage;
using ScreenshotsVisualizer.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ScreenshotsVisualizer.Views.StartPage
{
    /// <summary>
    /// StartPage settings surface for <see cref="SsvCarouselOptions"/>.
    /// </summary>
    public partial class SsvCarouselSettings : UserControl
    {
        private static ILogger Logger => LogManager.GetLogger();

        private ScreenshotsVisualizer Plugin { get; }
        private static ScreenshotsVisualizerDatabase PluginDatabase { get; set; } = ScreenshotsVisualizer.PluginDatabase;

        private List<string> SearchSources { get; set; } = new List<string>();


        public SsvCarouselSettings(ScreenshotsVisualizer plugin)
        {
            InitializeComponent();

            this.Plugin = plugin;
            this.DataContext = PluginDatabase.PluginSettings;

            LogCarouselOptionsSnapshot("ctor");

            PluginDatabase.PluginSettings.ssvCarouselOptions.SourcesList.Where(x => x.IsCheck)?.ForEach(x =>
            {
                SearchSources.Add(x.Name);
            });

            if (SearchSources.Count != 0)
            {
                FilterSource.Text = string.Join(", ", SearchSources);
            }

            Loaded += SsvCarouselSettings_Loaded;
        }

        private void SsvCarouselSettings_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= SsvCarouselSettings_Loaded;
            LogCarouselOptionsSnapshot("loaded");
            LogEnableLowerResolutionBindingState();
        }

        private void Grid_Unloaded(object sender, RoutedEventArgs e)
        {
            LogCarouselOptionsSnapshot("unload-before-save");
            LogEnableLowerResolutionBindingState();

            try
            {
                Plugin.SavePluginSettings(PluginDatabase.PluginSettings);
                PluginDatabase.PluginSettings.OnPropertyChanged();

                SsvCarouselOptions options = PluginDatabase.PluginSettings?.ssvCarouselOptions;
                Logger.Info(string.Format(
                    "[SsvCarouselSettings][unload] saved EnableLowerResolution={0}, EnableAutoChange={1}, LimitPerGame={2}",
                    options?.EnableLowerResolution,
                    options?.EnableAutoChange,
                    options?.LimitPerGame));
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, "[SsvCarouselSettings][unload] SavePluginSettings failed", true, PluginDatabase.PluginName);
            }
        }

        private void LogCarouselOptionsSnapshot(string phase)
        {
            SsvCarouselOptions options = PluginDatabase.PluginSettings?.ssvCarouselOptions;
            if (options == null)
            {
                Logger.Warn(string.Format("[SsvCarouselSettings][{0}] ssvCarouselOptions is null", phase));
                return;
            }

            Common.LogDebug(string.Format(
                "[SsvCarouselSettings][{0}] EnableLowerResolution={1}, EnableAllRandom={2}, EnableAutoChange={3}, LimitPerGame={4}, Margin={5}, SourcesChecked={6}",
                phase,
                options.EnableLowerResolution,
                options.EnableAllRandom,
                options.EnableAutoChange,
                options.LimitPerGame,
                options.Margin,
                options.SourcesList?.Count(x => x.IsCheck) ?? 0));
        }

        private void LogEnableLowerResolutionBindingState()
        {
            CheckBox lowerResolutionCheckBox = FindName("PART_EnableLowerResolution") as CheckBox;
            if (lowerResolutionCheckBox == null)
            {
                Common.LogDebug("[SsvCarouselSettings][binding] PART_EnableLowerResolution not found");
                return;
            }

            BindingExpression expression = lowerResolutionCheckBox.GetBindingExpression(CheckBox.IsCheckedProperty);
            if (expression == null)
            {
                Logger.Warn("[SsvCarouselSettings][binding] EnableLowerResolution has no BindingExpression");
                return;
            }

            Common.LogDebug(string.Format(
                "[SsvCarouselSettings][binding] path={0}, status={1}, hasError={2}, targetIsChecked={3}",
                expression.ParentBinding?.Path?.Path ?? "null",
                expression.Status,
                expression.HasError,
                lowerResolutionCheckBox.IsChecked));
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

            int idx = PluginDatabase.PluginSettings.ssvCarouselOptions.SourcesList.FindIndex(x => x.Name == (string)sender.Tag);
            if (idx > -1)
            {
                PluginDatabase.PluginSettings.ssvCarouselOptions.SourcesList[idx].IsCheck = (bool)sender.IsChecked;
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
