using System;
using System.Collections.Generic;
using System.Linq;
using CommonPluginsShared.Interfaces;
using CommonPluginsStores;
using Playnite.SDK;

namespace ScreenshotsVisualizer.Models
{
    public class GameSettings
    {
        public Guid Id { get; set; }
        public List<FolderSettings> ScreenshotsFolders { get; set; }
        public bool OverrideGlobalConfigs { get; set; }

        // TODO TEMP
        public bool ScanSubFolders { get; set; }
        public bool UsedFilePattern { get; set; }
        public string FilePattern { get; set; }
        public string ScreenshotsFolder { get; set; }

        public List<string> GetScreenshotsFolders() => ScreenshotsFolders
                .Select(x => PlayniteTools.StringExpandWithStores(API.Instance.Database.Games.Get(Id), x.ScreenshotsFolder))
                .ToList();
    }


    /// <summary>
    /// Screenshot folder path, scan options, and optional global applicability constraints.
    /// Applicability fields are meaningful only for entries in
    /// <see cref="ScreenshotsVisualizerSettings.GlobalScreenshotSources"/>; they are ignored on per-game folders.
    /// </summary>
    public class FolderSettings
    {
        /// <summary>
        /// Gets or sets whether subfolders are scanned recursively for this source.
        /// </summary>
        public bool ScanSubFolders { get; set; }

        /// <summary>
        /// Gets or sets whether file name pattern matching is enabled.
        /// </summary>
        public bool UsedFilePattern { get; set; }

        /// <summary>
        /// Gets or sets the file name pattern when <see cref="UsedFilePattern"/> is enabled.
        /// </summary>
        public string FilePattern { get; set; }

        /// <summary>
        /// Gets or sets the screenshot directory path or path template.
        /// </summary>
        public string ScreenshotsFolder { get; set; }

        /// <summary>
        /// Gets or sets how Playnite library source names constrain this global source.
        /// Default <see cref="SourceFilterMode.All"/> applies to every source.
        /// </summary>
        public SourceFilterMode ApplicableSourceFilterMode { get; set; } = SourceFilterMode.All;

        /// <summary>
        /// Gets or sets normalized Playnite source names used with
        /// <see cref="ApplicableSourceFilterMode"/> (whitelist or blacklist).
        /// Empty or null means no named source restriction when mode is <see cref="SourceFilterMode.All"/>.
        /// </summary>
        public List<string> ApplicableSources { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets an optional emulator constraint for this global source.
        /// Default <see cref="SsvApplicableEmulatorFilter.None"/> does not filter by emulator.
        /// </summary>
        public SsvApplicableEmulatorFilter ApplicableEmulatorFilter { get; set; } = SsvApplicableEmulatorFilter.None;

        /// <summary>
        /// Creates a shallow copy of this folder settings instance, including applicability fields.
        /// </summary>
        /// <returns>A new <see cref="FolderSettings"/> with the same values.</returns>
        public FolderSettings Clone()
        {
            return new FolderSettings
            {
                ScanSubFolders = ScanSubFolders,
                UsedFilePattern = UsedFilePattern,
                FilePattern = FilePattern,
                ScreenshotsFolder = ScreenshotsFolder,
                ApplicableSourceFilterMode = ApplicableSourceFilterMode,
                ApplicableSources = ApplicableSources != null
                    ? new List<string>(ApplicableSources)
                    : new List<string>(),
                ApplicableEmulatorFilter = ApplicableEmulatorFilter
            };
        }
    }
}
