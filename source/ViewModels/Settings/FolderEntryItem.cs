using Playnite.SDK;
using ScreenshotsVisualizer.Models;
using System.Collections.Generic;

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
                }
            }
        }

        private bool _canRemoveFolder;

        /// <summary>
        /// Gets or sets whether this folder row can be removed (more than one folder on the game).
        /// </summary>
        public bool CanRemoveFolder
        {
            get => _canRemoveFolder;
            set
            {
                if (_canRemoveFolder != value)
                {
                    _canRemoveFolder = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Creates a shallow copy of the wrapped settings for serialization.
        /// </summary>
        /// <returns>A new <see cref="FolderSettings"/> instance with the same values.</returns>
        public FolderSettings ToModel()
        {
            return new FolderSettings
            {
                ScreenshotsFolder = _settings.ScreenshotsFolder,
                UsedFilePattern = _settings.UsedFilePattern,
                ScanSubFolders = _settings.ScanSubFolders,
                FilePattern = _settings.FilePattern
            };
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
        }
    }
}
