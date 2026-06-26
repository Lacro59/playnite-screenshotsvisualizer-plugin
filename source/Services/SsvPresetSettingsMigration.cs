using CommonPlayniteShared;
using Playnite.SDK;
using ScreenshotsVisualizer.Models;
using System;
using System.IO;

namespace ScreenshotsVisualizer.Services
{
    /// <summary>
    /// One-shot migration support for global preset folder configuration (backup ZIP, marker, consolidation).
    /// </summary>
    public static class SsvPresetSettingsMigration
    {
        /// <summary>
        /// Marker file written in <c>PluginUserDataPath</c> after a successful migration.
        /// </summary>
        public const string MigrationMarkerFileName = ".preset-global-migration.done";

        private const string MigrationArchiveNameFormat = "{0}_preset-settings-migration_{1:yyyyMMdd_HHmmss}.zip";

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
        /// Returns whether the one-shot preset configuration migration has already completed.
        /// </summary>
        /// <param name="pluginUserDataPath">Plugin user data folder.</param>
        /// <returns><c>true</c> when the marker file exists.</returns>
        public static bool IsMigrationCompleted(string pluginUserDataPath)
        {
            string markerPath = GetMigrationMarkerPath(pluginUserDataPath);
            return !string.IsNullOrEmpty(markerPath) && File.Exists(markerPath);
        }

        /// <summary>
        /// Returns whether persisted settings still require preset global migration work.
        /// </summary>
        /// <param name="settings">Current plugin settings.</param>
        /// <returns><c>true</c> when duplicates or missing canonical globals remain.</returns>
        public static bool HasPendingMigrationWork(ScreenshotsVisualizerSettings settings)
        {
            if (settings == null)
            {
                return false;
            }

            SsvPresetDuplicateAnalysis analysis = SsvPresetFolderHelper.AnalyzePersistedPresetDuplicates(settings);
            return analysis.TotalRemovable > 0 || analysis.MissingGlobalPresets.Count > 0;
        }

        /// <summary>
        /// Schedules the one-shot preset settings migration on startup when needed.
        /// Intended to run after <see cref="SsvArchiveSettingsMigration"/> has finished.
        /// </summary>
        /// <param name="settings">Current plugin settings.</param>
        /// <param name="pluginUserDataPath">Plugin user data folder.</param>
        /// <param name="pluginName">Plugin display name.</param>
        /// <param name="saveSettings">Persists settings after cleanup.</param>
        public static void ScheduleIfNeeded(
            ScreenshotsVisualizerSettings settings,
            string pluginUserDataPath,
            string pluginName,
            Action<ScreenshotsVisualizerSettings> saveSettings)
        {
            if (settings == null)
            {
                return;
            }

            SsvPresetDuplicateAnalysis analysis = SsvPresetFolderHelper.AnalyzePersistedPresetDuplicates(settings);
            if (IsMigrationCompleted(pluginUserDataPath))
            {
                if (!HasPendingMigrationWork(settings))
                {
                    return;
                }

                Logger.Warn("[SsvPresetMigration] Resuming migration — migratable preset entries remain after a previous run.");
            }
            else if (!HasPendingMigrationWork(settings))
            {
                WriteMigrationMarker(pluginUserDataPath, 0, null, null, "No preset duplicates found.");
                return;
            }

            GlobalProgressOptions progressOptions = new GlobalProgressOptions(
                string.Format("{0} - {1}", pluginName, ResourceProvider.GetString("LOCSsvPresetMigrationTitle")))
            {
                Cancelable = false,
                IsIndeterminate = true
            };

            _ = API.Instance.Dialogs.ActivateGlobalProgress(
                progress => RunMigrationWithProgress(settings, pluginUserDataPath, pluginName, saveSettings, progress),
                progressOptions);
        }

        /// <summary>
        /// Runs backup, preset consolidation, settings save, and marker write under a progress dialog.
        /// </summary>
        /// <param name="settings">Current plugin settings.</param>
        /// <param name="pluginUserDataPath">Plugin user data folder.</param>
        /// <param name="pluginName">Plugin display name.</param>
        /// <param name="saveSettings">Persists settings after cleanup.</param>
        /// <param name="progress">Progress dialog arguments.</param>
        /// <returns>Migration result.</returns>
        public static SsvPresetMigrationResult RunMigrationWithProgress(
            ScreenshotsVisualizerSettings settings,
            string pluginUserDataPath,
            string pluginName,
            Action<ScreenshotsVisualizerSettings> saveSettings,
            GlobalProgressActionArgs progress)
        {
            if (progress != null)
            {
                progress.Text = ResourceProvider.GetString("LOCSsvPresetMigrationCreatingBackup");
            }

            SsvArchiveSettingsBackupResult backup = SsvArchiveSettingsMigration.TryCreateSettingsBackupZip(
                pluginUserDataPath,
                pluginName,
                MigrationArchiveNameFormat);
            if (!backup.Success)
            {
                return FailedMigration(pluginName, backup.ErrorMessage, progress);
            }

            try
            {
                SsvPresetDuplicateAnalysis analysis = SsvPresetFolderHelper.AnalyzePersistedPresetDuplicates(settings);

                if (progress != null)
                {
                    progress.Text = ResourceProvider.GetString("LOCSsvPresetMigrationConsolidating");
                }

                SsvPresetMigrationResult cleanup = SsvPresetFolderHelper.ApplyPresetGlobalMigration(settings, analysis);
                if (cleanup.TotalChanges <= 0)
                {
                    return FailedMigration(
                        pluginName,
                        ResourceProvider.GetString("LOCSsvPresetMigrationNoChangesApplied"),
                        progress);
                }

                if (progress != null)
                {
                    progress.Text = ResourceProvider.GetString("LOCSsvPresetMigrationSavingSettings");
                }

                saveSettings?.Invoke(settings);

                WriteMigrationMarker(
                    pluginUserDataPath,
                    cleanup.TotalChanges,
                    backup.ArchivePath,
                    cleanup,
                    null);

                if (progress != null)
                {
                    progress.Text = ResourceProvider.GetString("LOCSsvPresetMigrationCompleted");
                }

                Logger.Info(string.Format(
                    "[SsvPresetMigration] Completed — globals added {0}, legacy global removed {1}, per-game removed {2}, empty games removed {3}, backup '{4}'.",
                    cleanup.GlobalPresetsAdded,
                    cleanup.LegacyGlobalRemoved,
                    cleanup.PerGameDuplicatesRemoved,
                    cleanup.EmptyGameSettingsRemoved,
                    backup.ArchivePath));

                CommonPluginsShared.Common.LogDebug(true, string.Format(
                    "[SsvPresetMigration] Completed — {0} change(s).",
                    cleanup.TotalChanges));

                cleanup.Success = true;
                cleanup.ArchivePath = backup.ArchivePath;
                return cleanup;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[SsvPresetMigration] Migration failed after backup.");
                return FailedMigration(pluginName, ex.Message, progress);
            }
        }

        /// <summary>
        /// Writes the one-shot migration marker file.
        /// </summary>
        /// <param name="pluginUserDataPath">Plugin user data folder.</param>
        /// <param name="totalChanges">Total number of persisted changes applied.</param>
        /// <param name="archivePath">Backup archive path, if any.</param>
        /// <param name="cleanup">Optional cleanup breakdown.</param>
        /// <param name="note">Optional note stored in the marker file.</param>
        public static void WriteMigrationMarker(
            string pluginUserDataPath,
            int totalChanges,
            string archivePath,
            SsvPresetMigrationResult cleanup,
            string note)
        {
            if (string.IsNullOrEmpty(pluginUserDataPath))
            {
                return;
            }

            string markerPath = GetMigrationMarkerPath(pluginUserDataPath);
            string content = string.Format(
                "Completed at {0:u}.{1}Total changes: {2}.{1}Backup: {3}",
                DateTime.UtcNow,
                Environment.NewLine,
                totalChanges,
                archivePath ?? "—");

            if (cleanup != null)
            {
                content += string.Format(
                    "{0}Globals added: {1}.{0}Legacy global removed: {2}.{0}Per-game removed: {3}.{0}Empty games removed: {4}.",
                    Environment.NewLine,
                    cleanup.GlobalPresetsAdded,
                    cleanup.LegacyGlobalRemoved,
                    cleanup.PerGameDuplicatesRemoved,
                    cleanup.EmptyGameSettingsRemoved);
            }

            if (!string.IsNullOrEmpty(note))
            {
                content += Environment.NewLine + "Note: " + note;
            }

            File.WriteAllText(markerPath, content);
        }

        private static SsvPresetMigrationResult FailedMigration(
            string pluginName,
            string message,
            GlobalProgressActionArgs progress)
        {
            Logger.Warn(string.Format("[SsvPresetMigration] Migration failed: {0}", message));

            if (progress != null)
            {
                progress.Text = string.Format(
                    ResourceProvider.GetString("LOCSsvPresetMigrationFailed"),
                    message);
            }

            if (!string.IsNullOrEmpty(pluginName))
            {
                API.Instance.Notifications.Add(new NotificationMessage(
                    string.Format("{0}-PresetMigration", pluginName),
                    string.Format("{0}\r\n{1}", pluginName, string.Format(
                        ResourceProvider.GetString("LOCSsvPresetMigrationFailed"),
                        message)),
                    NotificationType.Error));
            }

            return new SsvPresetMigrationResult
            {
                Success = false,
                ErrorMessage = message
            };
        }
    }
}
