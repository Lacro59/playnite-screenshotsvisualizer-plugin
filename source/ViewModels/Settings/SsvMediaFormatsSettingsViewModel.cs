using CommonPluginsShared;
using Playnite.SDK;
using ScreenshotsVisualizer.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ScreenshotsVisualizer.ViewModels.Settings
{
    /// <summary>
    /// View model for active image and video extension lists in settings.
    /// </summary>
    public class SsvMediaFormatsSettingsViewModel : ObservableObject
    {
        private string _newImageExtension = string.Empty;
        private string _newVideoExtension = string.Empty;

        /// <summary>
        /// Initializes a new media formats settings view model.
        /// </summary>
        public SsvMediaFormatsSettingsViewModel()
        {
            ImageExtensions = new ObservableCollection<SsvMediaFormatListItem>();
            VideoExtensions = new ObservableCollection<SsvMediaFormatListItem>();

            AddImageCommand = new RelayCommand(AddImage);
            AddVideoCommand = new RelayCommand(AddVideo);
            RemoveImageCommand = new RelayCommand<object>(RemoveImage, CanRemoveItem);
            RemoveVideoCommand = new RelayCommand<object>(RemoveVideo, CanRemoveItem);
            RestoreDefaultImagesCommand = new RelayCommand(RestoreDefaultImages);
            RestoreDefaultVideosCommand = new RelayCommand(RestoreDefaultVideos);
        }

        /// <summary>
        /// Gets the active image extensions shown in the list view.
        /// </summary>
        public ObservableCollection<SsvMediaFormatListItem> ImageExtensions { get; }

        /// <summary>
        /// Gets the active video extensions shown in the list view.
        /// </summary>
        public ObservableCollection<SsvMediaFormatListItem> VideoExtensions { get; }

        /// <summary>
        /// Gets or sets the image extension entered in the add field.
        /// </summary>
        public string NewImageExtension
        {
            get => _newImageExtension;
            set => SetValue(ref _newImageExtension, value);
        }

        /// <summary>
        /// Gets or sets the video extension entered in the add field.
        /// </summary>
        public string NewVideoExtension
        {
            get => _newVideoExtension;
            set => SetValue(ref _newVideoExtension, value);
        }

        /// <summary>
        /// Gets the command that adds an image extension to the list.
        /// </summary>
        public RelayCommand AddImageCommand { get; }

        /// <summary>
        /// Gets the command that adds a video extension to the list.
        /// </summary>
        public RelayCommand AddVideoCommand { get; }

        /// <summary>
        /// Gets the command that removes an image extension from the list.
        /// </summary>
        public RelayCommand<object> RemoveImageCommand { get; }

        /// <summary>
        /// Gets the command that removes a video extension from the list.
        /// </summary>
        public RelayCommand<object> RemoveVideoCommand { get; }

        /// <summary>
        /// Gets the command that restores default image extensions.
        /// </summary>
        public RelayCommand RestoreDefaultImagesCommand { get; }

        /// <summary>
        /// Gets the command that restores default video extensions.
        /// </summary>
        public RelayCommand RestoreDefaultVideosCommand { get; }

        /// <summary>
        /// Loads active extension lists from plugin settings.
        /// </summary>
        /// <param name="settings">Current plugin settings instance.</param>
        public void LoadFrom(ScreenshotsVisualizerSettings settings)
        {
            SsvMediaFormatCatalog.EnsureSettingsInitialized(settings);
            LoadActiveList(ImageExtensions, SsvMediaFormatKind.Image, settings);
            LoadActiveList(VideoExtensions, SsvMediaFormatKind.Video, settings);
            NewImageExtension = string.Empty;
            NewVideoExtension = string.Empty;
        }

        /// <summary>
        /// Writes active extension lists back to plugin settings.
        /// </summary>
        /// <param name="settings">Plugin settings instance to update.</param>
        public void ApplyToSettings(ScreenshotsVisualizerSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            SsvMediaFormatCatalog.ApplyActiveExtensions(
                settings,
                SsvMediaFormatKind.Image,
                ImageExtensions.Select(x => x.Extension));
            SsvMediaFormatCatalog.ApplyActiveExtensions(
                settings,
                SsvMediaFormatKind.Video,
                VideoExtensions.Select(x => x.Extension));
            SsvMediaFormatCatalog.NormalizeForPersistence(settings);
        }

        private void AddImage()
        {
            TryAddExtension(NewImageExtension, SsvMediaFormatKind.Image, ImageExtensions);
            NewImageExtension = string.Empty;
        }

        private void AddVideo()
        {
            TryAddExtension(NewVideoExtension, SsvMediaFormatKind.Video, VideoExtensions);
            NewVideoExtension = string.Empty;
        }

        private void TryAddExtension(
            string input,
            SsvMediaFormatKind kind,
            ObservableCollection<SsvMediaFormatListItem> target)
        {
            if (!SsvMediaFormatCatalog.TryNormalizeForActiveList(input, kind, out string normalized))
            {
                API.Instance.Dialogs.ShowErrorMessage(
                    ResourceProvider.GetString("LOCSsvMediaFormatsExtensionInvalid"),
                    ResourceProvider.GetString("LOCSsv"));
                return;
            }

            if (target.Any(x => string.Equals(x.Extension, normalized, System.StringComparison.OrdinalIgnoreCase)))
            {
                API.Instance.Dialogs.ShowErrorMessage(
                    ResourceProvider.GetString("LOCSsvMediaFormatsExtensionDuplicate"),
                    ResourceProvider.GetString("LOCSsv"));
                return;
            }

            target.Add(SsvMediaFormatListItem.FromExtension(normalized, kind));
        }

        private void RemoveImage(object parameter)
        {
            RemoveItem(parameter, ImageExtensions);
        }

        private void RemoveVideo(object parameter)
        {
            RemoveItem(parameter, VideoExtensions);
        }

        private static bool CanRemoveItem(object parameter)
        {
            SsvMediaFormatListItem item = parameter as SsvMediaFormatListItem;
            return item != null && item.CanRemove;
        }

        private static void RemoveItem(object parameter, ObservableCollection<SsvMediaFormatListItem> target)
        {
            SsvMediaFormatListItem item = parameter as SsvMediaFormatListItem;
            if (item != null && item.CanRemove && target.Contains(item))
            {
                target.Remove(item);
            }
        }

        private void RestoreDefaultImages()
        {
            RestoreDefaults(SsvMediaFormatKind.Image, ImageExtensions);
        }

        private void RestoreDefaultVideos()
        {
            RestoreDefaults(SsvMediaFormatKind.Video, VideoExtensions);
        }

        private static void RestoreDefaults(SsvMediaFormatKind kind, ObservableCollection<SsvMediaFormatListItem> target)
        {
            List<string> defaults = SsvMediaFormatCatalog.GetDefaultEnabledExtensions(kind);
            HashSet<string> customExtensions = new HashSet<string>(
                target.Where(x => x.IsCustom).Select(x => x.Extension),
                System.StringComparer.OrdinalIgnoreCase);

            target.Clear();
            HashSet<string> added = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            foreach (string extension in defaults)
            {
                if (added.Add(extension))
                {
                    target.Add(SsvMediaFormatListItem.FromExtension(extension, kind));
                }
            }

            foreach (string extension in customExtensions.OrderBy(x => x, System.StringComparer.OrdinalIgnoreCase))
            {
                if (added.Add(extension))
                {
                    target.Add(SsvMediaFormatListItem.FromExtension(extension, kind));
                }
            }
        }

        private static void LoadActiveList(
            ObservableCollection<SsvMediaFormatListItem> target,
            SsvMediaFormatKind kind,
            ScreenshotsVisualizerSettings settings)
        {
            target.Clear();
            foreach (string extension in SsvMediaFormatCatalog.GetMergedActiveExtensions(settings, kind))
            {
                target.Add(SsvMediaFormatListItem.FromExtension(extension, kind));
            }
        }
    }

    /// <summary>
    /// Row model for an active media extension in the settings list view.
    /// </summary>
    public sealed class SsvMediaFormatListItem
    {
        private SsvMediaFormatListItem(string extension, bool isEssential, bool isCustom, string codecHint)
        {
            Extension = extension;
            IsEssential = isEssential;
            IsCustom = isCustom;
            CodecHint = codecHint;
        }

        /// <summary>
        /// Gets the file extension including the leading dot.
        /// </summary>
        public string Extension { get; }

        /// <summary>
        /// Gets the label shown in the list view.
        /// </summary>
        public string DisplayName => Extension;

        /// <summary>
        /// Gets whether the extension must remain in the active list.
        /// </summary>
        public bool IsEssential { get; }

        /// <summary>
        /// Gets whether the extension is not part of the built-in catalog.
        /// </summary>
        public bool IsCustom { get; }

        /// <summary>
        /// Gets whether the user can remove this entry.
        /// </summary>
        public bool CanRemove => !IsEssential;

        /// <summary>
        /// Gets the localized codec requirement hint.
        /// </summary>
        public string CodecHint { get; }

        /// <summary>
        /// Creates a list item for the given extension.
        /// </summary>
        /// <param name="extension">Normalized extension.</param>
        /// <param name="kind">Image or video category.</param>
        public static SsvMediaFormatListItem FromExtension(string extension, SsvMediaFormatKind kind)
        {
            SsvMediaFormatDefinition definition = SsvMediaFormatCatalog.TryGetDefinition(extension);
            bool isEssential = definition != null && definition.IsEssential;
            bool isCustom = definition == null;

            string codecHint;
            if (definition != null && definition.Kind == kind)
            {
                codecHint = isEssential
                    ? ResourceProvider.GetString("LOCSsvMediaFormatsEssentialHint")
                    : ResourceProvider.GetString(definition.CodecHintResourceKey);
            }
            else
            {
                codecHint = ResourceProvider.GetString(SsvMediaFormatCatalog.CustomUnknownHintResourceKey);
            }

            return new SsvMediaFormatListItem(extension, isEssential, isCustom, codecHint);
        }
    }
}
