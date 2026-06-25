using ScreenshotsVisualizer.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace ScreenshotsVisualizer.ViewModels.Settings
{
    /// <summary>
    /// View model for a game with one or more screenshot folder configurations.
    /// </summary>
    public class ConfiguredGameItem
    {
        public Guid Id { get; set; }
        public string Icon { get; set; }
        public string Name { get; set; }
        public string SourceName { get; set; }
        public string SourceIcon { get; set; }
        public bool OverrideGlobalConfigs { get; set; }

        /// <summary>
        /// Legacy game-level fields kept for settings serialization compatibility.
        /// </summary>
        public bool UsedFilePattern { get; set; }

        /// <summary>
        /// Legacy game-level fields kept for settings serialization compatibility.
        /// </summary>
        public bool ScanSubFolders { get; set; }

        /// <summary>
        /// Legacy game-level fields kept for settings serialization compatibility.
        /// </summary>
        public string FilePattern { get; set; }

        /// <summary>
        /// Gets the folder configuration rows for this game.
        /// </summary>
        public ObservableCollection<FolderEntryItem> ScreenshotsFolders { get; } = new ObservableCollection<FolderEntryItem>();

        /// <summary>
        /// Creates a configured game item from persisted settings and display metadata.
        /// </summary>
        /// <param name="settings">Persisted game settings.</param>
        /// <param name="icon">Resolved icon file path.</param>
        /// <param name="name">Current game name from the Playnite database.</param>
        /// <param name="sourceName">Library source name.</param>
        /// <param name="sourceIcon">Source icon glyph.</param>
        /// <returns>A populated <see cref="ConfiguredGameItem"/>.</returns>
        public static ConfiguredGameItem FromGameSettings(
            GameSettings settings,
            string icon,
            string name,
            string sourceName,
            string sourceIcon)
        {
            var item = new ConfiguredGameItem
            {
                Id = settings.Id,
                Icon = icon,
                Name = name,
                SourceName = sourceName,
                SourceIcon = sourceIcon,
                OverrideGlobalConfigs = settings.OverrideGlobalConfigs,
                UsedFilePattern = settings.UsedFilePattern,
                ScanSubFolders = settings.ScanSubFolders,
                FilePattern = settings.FilePattern
            };

            if (!string.IsNullOrEmpty(settings.ScreenshotsFolder))
            {
                item.ScreenshotsFolders.Add(new FolderEntryItem(new FolderSettings
                {
                    UsedFilePattern = settings.UsedFilePattern,
                    FilePattern = settings.FilePattern,
                    ScreenshotsFolder = settings.ScreenshotsFolder,
                    ScanSubFolders = settings.ScanSubFolders
                }));
            }
            else if (settings.ScreenshotsFolders != null)
            {
                foreach (FolderSettings folder in settings.ScreenshotsFolders)
                {
                    item.ScreenshotsFolders.Add(new FolderEntryItem(folder));
                }
            }

            return item;
        }

        /// <summary>
        /// Maps this item to a <see cref="GameSettings"/> instance for persistence.
        /// </summary>
        /// <returns>Persisted game settings.</returns>
        public GameSettings ToGameSettings()
        {
            return new GameSettings
            {
                Id = Id,
                ScreenshotsFolders = ScreenshotsFolders.Select(x => x.ToModel()).ToList(),
                OverrideGlobalConfigs = OverrideGlobalConfigs,
                UsedFilePattern = UsedFilePattern,
                FilePattern = FilePattern,
                ScanSubFolders = ScanSubFolders
            };
        }
    }
}
