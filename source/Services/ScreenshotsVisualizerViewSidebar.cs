using CommonPluginsShared.Controls;
using Playnite.SDK;
using Playnite.SDK.Plugins;
using ScreenshotsVisualizer.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

namespace ScreenshotsVisualizer.Services
{
    public class ScreenshotsVisualizerViewSidebar : SidebarItem
    {
        public ScreenshotsVisualizerViewSidebar(ScreenshotsVisualizer plugin)
        {
            Type = SiderbarItemType.View;
            Title = ResourceProvider.GetString("LOCSsv");
            Icon = new TextBlock
            {
                Text = "\uea38",
                FontFamily = ResourceProvider.GetResource("CommonFont") as FontFamily
            };
            Opened = () =>
            {
                if (plugin.SidebarItemControl == null)
                {
                    plugin.SidebarItemControl = new SidebarItemControl();
                    plugin.SidebarItemControl.SetTitle(ResourceProvider.GetString("LOCSsv"));
                    plugin.SidebarItemControl.AddContent(new SsvScreenshotsManager());
                }

                return plugin.SidebarItemControl;
            };
            Visible = plugin.PluginSettings.Settings.EnableIntegrationButtonSide;
        }
    }
}
