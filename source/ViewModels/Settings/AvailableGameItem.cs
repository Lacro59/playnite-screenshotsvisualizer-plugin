using System;

namespace ScreenshotsVisualizer.ViewModels.Settings
{
    /// <summary>
    /// View model for a Playnite game that is not yet in the screenshot configuration list.
    /// </summary>
    public class AvailableGameItem
    {
        public Guid Id { get; set; }
        public string Icon { get; set; }
        public string Name { get; set; }
        public string SourceName { get; set; }
        public string SourceIcon { get; set; }
    }
}
