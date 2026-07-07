using CommonPlayniteShared;
using Playnite.SDK;
using ScreenshotsVisualizer.Models;
using System;
using System.IO;

namespace ScreenshotsVisualizer.Services
{
    /// <summary>
    /// One-shot migration support to remove persisted per-game entries duplicating applicable global screenshot sources.
    /// </summary>
    public static class SsvGlobalSourceSettingsMigration
    {
        /// <summary>
        /// Marker file written in <c>PluginUserDataPath</c> after a successful migration.
        /// </summary>
        public const string MigrationMarkerFileName = ".global-sources-runtime-migration.done";

        private const string MigrationArchiveNameFormat = "{0}_global-sources-migration_{1:yyyyMMdd_HHmmss}.zip";

        private static readonly ILogger Logger = LogManager.GetLogger();

        /// <summary>
        /// Returns the full path to the migration marker file.
        /// </summary>
        /// <param name="pluginUserDataPath">Plugin user data folder.</param>
        /// <returns>Marker file path.</returns>
        public static string GetMigrationMarkerPath(string pluginUserDataPath)
        {
            return Path.Combine(pluginUserDataPath ?? string.Empty, MigrationMarkerFileName);
        }

        /// <summary>
        /// Returns whether the one-shot global source migration has already completed.
        /// </summary>
        /// <param name="pluginUserDataPath">Plugin user data folder.</param>
        /// <returns><c>true</c> when the marker file exists.</returns>
        public static bool IsMigrationCompleted(string pluginUserDataPath)
        {
            string markerPath = GetMigrationMarkerPath(pluginUserDataPath);
            return !string.IsNullOrEmpty(markerPath) && File.Exists(markerPath);
        }

        /// <summary>
        /// Returns whether persisted settings still require global source duplicate cleanup.
        /// </summary>
        /// <param name="settings">Current plugin settings.</param>
        /// <returns><c>true</c> when duplicates remain.</returns>
        public static bool HasPendingMigrationWork(ScreenshotsVisualizerSettings settings)
        {
            return SsvGlobalSourceFolderHelper.CountPersistedGlobalSourceDuplicatesForMigration(settings) > 0;
        }

        /// <summary>
        /// Schedules the one-shot global source cleanup migration on startup when needed.
        /// </summary>
        /// <param name="settings">Current plugin settings.</param>
        /// <param name="pluginUserDataPath">Plugin user data folder.</param>
        /// <param name="pluginName">Plugin display name.</param>
        /// <param name="saveSettings">Persists settings after cleanup.</param>
        /// <param name="onCompleted">Optional callback invoked when migration is skipped or finished.</param>
        public static void ScheduleIfNeeded(
            ScreenshotsVisualizerSettings settings,
            string pluginUserDataPath,
            string pluginName,
            Action<ScreenshotsVisualizerSettings> saveSettings,
            Action onCompleted = null)
        {
            if (settings == null)
            {
                onCompleted?.Invoke();
                return;
            }

            bool hasPendingWork = HasPendingMigrationWork(settings);
            CommonPluginsShared.Common.LogDebug(true, string.Format(
                "[SsvGlobalSourceMigration] ScheduleIfNeeded — pending={0}, markerExists={1}.",
                hasPendingWork,
                IsMigrationCompleted(pluginUserDataPath)));
            if (IsMigrationCompleted(pluginUserDataPath))
            {
                if (!hasPendingWork)
                {
                    CommonPluginsShared.Common.LogDebug(true, "[SsvGlobalSourceMigration] Skipped — marker exists and no pending work.");
                    onCompleted?.Invoke();
                    return;
                }

                Logger.Warn("[SsvGlobalSourceMigration] Resuming migration — migratable global source duplicates remain after a previous run.");
            }
            else if (!hasPendingWork)
            {
                WriteMigrationMarker(pluginUserDataPath, 0, null, "No global source duplicates found.");
                CommonPluginsShared.Common.LogDebug(true, "[SsvGlobalSourceMigration] No pending duplicate found — marker written.");
                onCompleted?.Invoke();
                return;
            }

            RunMigration(settings, pluginUserDataPath, pluginName, saveSettings);
            onCompleted?.Invoke();
        }

        /// <summary>
        /// Runs backup, duplicate cleanup, settings save, and marker write.
        /// </summary>
        /// <param name="settings">Current plugin settings.</param>
        /// <param name="pluginUserDataPath">Plugin user data folder.</param>
        /// <param name="pluginName">Plugin display name.</param>
        /// <param name="saveSettings">Persists settings after cleanup.</param>
        /// <returns><c>true</c> when migration succeeded.</returns>
        public static bool RunMigration(
            ScreenshotsVisualizerSettings settings,
            string pluginUserDataPath,
            string pluginName,
            Action<ScreenshotsVisualizerSettings> saveSettings)
        {
            CommonPluginsShared.Common.LogDebug(true, "[SsvGlobalSourceMigration] RunMigration started.");
            SsvArchiveSettingsBackupResult backup = SsvArchiveSettingsMigration.TryCreateSettingsBackupZip(
                pluginUserDataPath,
                pluginName,
                MigrationArchiveNameFormat);
            if (!backup.Success)
            {
                Logger.Warn(string.Format("[SsvGlobalSourceMigration] Settings backup failed: {0}", backup.ErrorMessage));
                return false;
            }

            try
            {
                int removed = SsvGlobalSourceFolderHelper.RemovePersistedGlobalSourceDuplicatesForMigration(settings);
                if (removed <= 0)
                {
                    Logger.Warn("[SsvGlobalSourceMigration] No duplicate removed after backup.");
                    return false;
                }

                saveSettings?.Invoke(settings);
                WriteMigrationMarker(pluginUserDataPath, removed, backup.ArchivePath, null);

                Logger.Info(string.Format(
                    "[SsvGlobalSourceMigration] Completed — removed {0} duplicate(s), backup '{1}'.",
                    removed,
                    backup.ArchivePath));

                CommonPluginsShared.Common.LogDebug(true, string.Format(
                    "[SsvGlobalSourceMigration] Completed — removed {0} duplicate(s).",
                    removed));

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[SsvGlobalSourceMigration] Migration failed after backup.");
                return false;
            }
        }

        /// <summary>
        /// Writes the one-shot migration marker file.
        /// </summary>
        /// <param name="pluginUserDataPath">Plugin user data folder.</param>
        /// <param name="removedDuplicates">Number of removed duplicate rows.</param>
        /// <param name="archivePath">Backup archive path, if any.</param>
        /// <param name="note">Optional note stored in the marker file.</param>
        public static void WriteMigrationMarker(
            string pluginUserDataPath,
            int removedDuplicates,
            string archivePath,
            string note)
        {
            if (string.IsNullOrEmpty(pluginUserDataPath))
            {
                return;
            }

            string markerPath = GetMigrationMarkerPath(pluginUserDataPath);
            string content = string.Format(
                "Completed at {0:u}.{1}Removed: {2}.{1}Backup: {3}",
                DateTime.UtcNow,
                Environment.NewLine,
                removedDuplicates,
                archivePath ?? "—");

            if (!string.IsNullOrEmpty(note))
            {
                content += Environment.NewLine + "Note: " + note;
            }

            File.WriteAllText(markerPath, content);
            CommonPluginsShared.Common.LogDebug(true, string.Format(
                "[SsvGlobalSourceMigration] Marker written: '{0}'.",
                markerPath));
        }
    }
}

