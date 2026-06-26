using CommonPlayniteShared;
using CommonPluginsShared.Interfaces;
using Playnite.SDK;
using ScreenshotsVisualizer.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ScreenshotsVisualizer.ViewModels.Settings
{
    /// <summary>
    /// View model wrapper around <see cref="FolderSettings"/> for folder row bindings.
    /// </summary>
    public class FolderEntryItem : ObservableObject
    {
        private readonly FolderSettings _settings;

        /// <summary>
        /// Initializes a new folder entry with empty settings.
        /// </summary>
        public FolderEntryItem()
            : this(new FolderSettings())
        {
        }

        /// <summary>
        /// Initializes a new folder entry wrapping existing settings.
        /// </summary>
        /// <param name="settings">Persisted folder settings instance.</param>
        public FolderEntryItem(FolderSettings settings)
        {
            _settings = settings ?? new FolderSettings();
        }

        /// <summary>
        /// Gets the underlying settings model used for persistence.
        /// </summary>
        public FolderSettings Settings => _settings;

        /// <summary>
        /// Gets or sets the screenshot directory path or path template.
        /// </summary>
        public string ScreenshotsFolder
        {
            get => _settings.ScreenshotsFolder;
            set
            {
                if (_settings.ScreenshotsFolder != value)
                {
                    _settings.ScreenshotsFolder = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether file name pattern matching is enabled for this folder.
        /// </summary>
        public bool UsedFilePattern
        {
            get => _settings.UsedFilePattern;
            set
            {
                if (_settings.UsedFilePattern != value)
                {
                    _settings.UsedFilePattern = value;
                    OnPropertyChanged();
                    NotifyListSummaryPropertiesChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether subfolders are scanned recursively.
        /// </summary>
        public bool ScanSubFolders
        {
            get => _settings.ScanSubFolders;
            set
            {
                if (_settings.ScanSubFolders != value)
                {
                    _settings.ScanSubFolders = value;
                    OnPropertyChanged();
                    NotifyListSummaryPropertiesChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the file name pattern when <see cref="UsedFilePattern"/> is enabled.
        /// </summary>
        public string FilePattern
        {
            get => _settings.FilePattern;
            set
            {
                if (_settings.FilePattern != value)
                {
                    _settings.FilePattern = value;
                    OnPropertyChanged();
                    NotifyListSummaryPropertiesChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets how Playnite library sources constrain this global source entry.
        /// </summary>
        public SourceFilterMode ApplicableSourceFilterMode
        {
            get => _settings.ApplicableSourceFilterMode;
            set
            {
                if (_settings.ApplicableSourceFilterMode != value)
                {
                    _settings.ApplicableSourceFilterMode = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsApplicableSourceListEnabled));
                    NotifyListSummaryPropertiesChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the optional emulator constraint for this global source entry.
        /// </summary>
        public SsvApplicableEmulatorFilter ApplicableEmulatorFilter
        {
            get => _settings.ApplicableEmulatorFilter;
            set
            {
                if (_settings.ApplicableEmulatorFilter != value)
                {
                    _settings.ApplicableEmulatorFilter = value;
                    OnPropertyChanged();
                    NotifyListSummaryPropertiesChanged();
                }
            }
        }

        /// <summary>
        /// Gets whether the applicable source list is editable (whitelist or blacklist mode).
        /// </summary>
        public bool IsApplicableSourceListEnabled =>
            ApplicableSourceFilterMode == SourceFilterMode.Whitelist
            || ApplicableSourceFilterMode == SourceFilterMode.Blacklist;

        /// <summary>
        /// Replaces the applicable Playnite source names for global applicability filtering.
        /// </summary>
        /// <param name="sources">Normalized source names.</param>
        public void SetApplicableSources(IEnumerable<string> sources)
        {
            _settings.ApplicableSources = sources?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(System.StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();
            OnPropertyChanged(nameof(ApplicableSourcesDisplay));
            NotifyListSummaryPropertiesChanged();
        }

        /// <summary>
        /// Gets a read-only view of applicable source names for UI binding.
        /// </summary>
        public IReadOnlyList<string> ApplicableSources =>
            _settings.ApplicableSources ?? new List<string>();

        /// <summary>
        /// Gets a comma-separated display string of applicable sources.
        /// </summary>
        public string ApplicableSourcesDisplay =>
            ApplicableSources.Count == 0
                ? string.Empty
                : string.Join(", ", ApplicableSources);

        /// <summary>
        /// Gets whether a file pattern summary line should be shown in source lists.
        /// </summary>
        public bool ShowFilePatternSummary =>
            UsedFilePattern && !string.IsNullOrWhiteSpace(FilePattern);

        /// <summary>
        /// Gets the localized file pattern summary for source list rows.
        /// </summary>
        public string FilePatternSummary =>
            ShowFilePatternSummary
                ? string.Format(ResourceProvider.GetString("LOCSsvSourceListFilePatternPrefix"), FilePattern)
                : string.Empty;

        /// <summary>
        /// Gets whether the scan subfolders option should be shown in source lists.
        /// </summary>
        public bool ShowScanSubFoldersSummary => ScanSubFolders;

        /// <summary>
        /// Gets the localized scan subfolders summary for source list rows.
        /// </summary>
        public string ScanSubFoldersSummary =>
            ShowScanSubFoldersSummary
                ? ResourceProvider.GetString("LOCSsvScanSubFolders")
                : string.Empty;

        /// <summary>
        /// Gets whether applicability constraints should be shown in source lists.
        /// </summary>
        public bool ShowApplicabilitySummary => !string.IsNullOrEmpty(ApplicabilitySummary);

        /// <summary>
        /// Gets a compact applicability summary for global source list rows.
        /// </summary>
        public string ApplicabilitySummary
        {
            get
            {
                List<string> parts = new List<string>();

                if (ApplicableSourceFilterMode == SourceFilterMode.Whitelist
                    && ApplicableSources.Count > 0)
                {
                    parts.Add(string.Format(
                        ResourceProvider.GetString("LOCSsvSourceListSourcesWhitelist"),
                        ApplicableSourcesDisplay));
                }
                else if (ApplicableSourceFilterMode == SourceFilterMode.Blacklist
                    && ApplicableSources.Count > 0)
                {
                    parts.Add(string.Format(
                        ResourceProvider.GetString("LOCSsvSourceListSourcesBlacklist"),
                        ApplicableSourcesDisplay));
                }

                if (ApplicableEmulatorFilter == SsvApplicableEmulatorFilter.RetroArch)
                {
                    parts.Add(ResourceProvider.GetString("LOCSsvGlobalSourceEmulatorRetroArch"));
                }
                else if (ApplicableEmulatorFilter == SsvApplicableEmulatorFilter.ScummVM)
                {
                    parts.Add(ResourceProvider.GetString("LOCSsvGlobalSourceEmulatorScummVM"));
                }

                return parts.Count == 0 ? string.Empty : string.Join(" · ", parts);
            }
        }

        /// <summary>
        /// Creates a shallow copy of the wrapped settings for serialization.
        /// </summary>
        /// <returns>A new <see cref="FolderSettings"/> instance with the same values.</returns>
        public FolderSettings ToModel()
        {
            return _settings.Clone();
        }

        /// <summary>
        /// Copies all folder settings from another entry into this one.
        /// </summary>
        /// <param name="source">Source entry to copy from.</param>
        public void ApplyFrom(FolderEntryItem source)
        {
            if (source == null)
            {
                return;
            }

            ApplyFrom(source.ToModel());
        }

        /// <summary>
        /// Copies folder settings values into this entry.
        /// </summary>
        /// <param name="settings">Settings to apply.</param>
        public void ApplyFrom(FolderSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            ScreenshotsFolder = settings.ScreenshotsFolder;
            UsedFilePattern = settings.UsedFilePattern;
            ScanSubFolders = settings.ScanSubFolders;
            FilePattern = settings.FilePattern;
            ApplicableSourceFilterMode = settings.ApplicableSourceFilterMode;
            SetApplicableSources(settings.ApplicableSources);
            ApplicableEmulatorFilter = settings.ApplicableEmulatorFilter;
        }

        private void NotifyListSummaryPropertiesChanged()
        {
            OnPropertyChanged(nameof(ShowFilePatternSummary));
            OnPropertyChanged(nameof(FilePatternSummary));
            OnPropertyChanged(nameof(ShowScanSubFoldersSummary));
            OnPropertyChanged(nameof(ScanSubFoldersSummary));
            OnPropertyChanged(nameof(ShowApplicabilitySummary));
            OnPropertyChanged(nameof(ApplicabilitySummary));
        }
    }
}
