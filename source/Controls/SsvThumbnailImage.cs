using CommonPlayniteShared;
using CommonPluginsShared;
using ScreenshotsVisualizer.Services;
using System;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ScreenshotsVisualizer.Controls
{
    /// <summary>
    /// Image control for SSV galleries: loads plugin cache JPEG thumbnails without redundant resizing,
    /// and bounds decode size for non-cache source paths.
    /// </summary>
    public class SsvThumbnailImage : Image
    {
        private int _loadSequence;
        private string _currentSourcePath;

        /// <summary>
        /// Identifies the <see cref="Source"/> dependency property (file path string).
        /// </summary>
        public static new readonly DependencyProperty SourceProperty = DependencyProperty.Register(
            nameof(Source),
            typeof(string),
            typeof(SsvThumbnailImage),
            new FrameworkPropertyMetadata(string.Empty, OnSourcePropertyChanged));

        /// <summary>
        /// Gets or sets the image file path to display.
        /// </summary>
        public new string Source
        {
            get => (string)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        private static void OnSourcePropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
        {
            try
            {
                SsvThumbnailImage control = (SsvThumbnailImage)dependencyObject;
                control.LoadSourceAsync(args.NewValue as string);
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, "SsvThumbnailImage");
            }
        }

        private async void LoadSourceAsync(string newSourcePath)
        {
            if (string.Equals(newSourcePath, _currentSourcePath, StringComparison.Ordinal))
            {
                return;
            }

            _currentSourcePath = newSourcePath;
            int sequence = Interlocked.Increment(ref _loadSequence);

            BitmapSource loadedImage = null;
            if (!string.IsNullOrWhiteSpace(newSourcePath) && File.Exists(newSourcePath))
            {
                loadedImage = await Task.Run(() => LoadBitmapFromPath(newSourcePath)).ConfigureAwait(true);
            }

            if (sequence != Volatile.Read(ref _loadSequence))
            {
                return;
            }

            base.Source = loadedImage;
        }

        private static BitmapSource LoadBitmapFromPath(string path)
        {
            try
            {
                BitmapImage bitmap;
                if (SsvThumbnailService.IsPluginThumbnailCachePath(path))
                {
                    bitmap = BitmapExtensions.BitmapFromFile(path);
                }
                else
                {
                    BitmapLoadProperties loadProperties = new BitmapLoadProperties(
                        SsvThumbnailService.ThumbnailMaxDimension,
                        0)
                    {
                        Source = path
                    };
                    bitmap = BitmapExtensions.BitmapFromFile(path, loadProperties);
                }

                if (bitmap == null)
                {
                    return null;
                }

                if (bitmap.CanFreeze)
                {
                    bitmap.Freeze();
                }

                return bitmap;
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, false, "SsvThumbnailImage");
                return null;
            }
        }
    }
}
