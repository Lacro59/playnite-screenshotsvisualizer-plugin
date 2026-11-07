using CommonPluginsShared;
using Playnite.SDK;
using System;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ScreenshotsVisualizer.Services
{
    /// <summary>
    /// Copies screenshot image files to the Windows clipboard using the same decode pipeline as UI display (WIC / Magick.NET).
    /// </summary>
    public static class SsvClipboardHelper
    {
        private static readonly ILogger Logger = LogManager.GetLogger();

        /// <summary>
        /// Decodes <paramref name="filePath"/> via <see cref="BitmapExtensions.BitmapFromFile"/> and sets a raster image on the clipboard.
        /// Does not place a file drop (CF_HDROP) or the original bytes.
        /// Expected failures (missing file, decode null) are logged as Warn with a user notification; unexpected exceptions as Error.
        /// </summary>
        /// <param name="filePath">Absolute path to an image file on disk.</param>
        /// <returns><c>true</c> if the clipboard was updated; otherwise <c>false</c>.</returns>
        public static bool TryCopyImageFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                Logger.Warn("[Clipboard][copy] skipped reason=empty-path");
                NotifyCopyFailed();
                return false;
            }

            if (!File.Exists(filePath))
            {
                Logger.Warn(string.Format("[Clipboard][copy] skipped reason=missing path=\"{0}\"", filePath));
                NotifyCopyFailed();
                return false;
            }

            try
            {
                BitmapImage bitmap = BitmapExtensions.BitmapFromFile(filePath);
                if (bitmap == null)
                {
                    // BitmapFromFile already logged Error; clipboard outcome is Warn (not a second Error).
                    Logger.Warn(string.Format("[Clipboard][copy] decode-failed path=\"{0}\"", filePath));
                    NotifyCopyFailed();
                    return false;
                }

                Clipboard.SetImage(bitmap);
                Common.LogDebug(string.Format("[Clipboard][copy] ok path=\"{0}\"", filePath));
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, string.Format("[Clipboard][copy] failed path=\"{0}\"", filePath));
                NotifyCopyFailed();
                return false;
            }
        }

        private static void NotifyCopyFailed()
        {
            string pluginName = ScreenshotsVisualizer.PluginDatabase.PluginName;
            API.Instance.Notifications.Add(new NotificationMessage(
                string.Format("{0}-ClipboardCopy-Error", pluginName),
                string.Format("{0}\r\n{1}", pluginName, ResourceProvider.GetString("LOCSsvClipboardCopyFailed")),
                NotificationType.Error));
        }
    }
}
