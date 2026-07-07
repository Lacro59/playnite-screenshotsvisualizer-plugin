using CommonPlayniteShared;
using Playnite.SDK;
using ScreenshotsVisualizer.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace ScreenshotsVisualizer.Services
{
    /// <summary>
    /// One-shot migration support for global archive configuration (backup ZIP, marker, cleanup).
    /// </summary>
    public static class SsvArchiveSettingsMigration
    {
        /// <summary>
        /// Marker file written in <c>PluginUserDataPath</c> after a successful migration (step 7).
        /// </summary>
        public const string MigrationMarkerFileName = ".archive-global-config-migration.done";

        private const string MigrationArchiveNameFormat = "{0}_archive-settings-migration_{1:yyyyMMdd_HHmmss}.zip";

        private static readonly ILogger Logger = LogManager.GetLogger();

        /// <summary>
        /// Returns the plugin settings file paths to include in a pre-migration backup.
        /// </summary>
        /// <param name="pluginUserDataPath">Plugin user data folder (<c>PluginUserDataPath</c>).</param>
        /// <returns>Existing settings files to archive (currently <c>config.json</c> only).</returns>
        public static string[] GetSettingsFilesToBackup(string pluginUserDataPath)
        {
            if (string.IsNullOrEmpty(pluginUserDataPath))
            {
                return new string[0];
            }

            string configPath = Path.Combine(pluginUserDataPath, PlaynitePaths.ConfigFileName);
            if (!File.Exists(configPath))
            {
                return new string[0];
            }

            return new[] { configPath };
        }

        /// <summary>
        /// Creates a ZIP backup of plugin settings under <paramref name="pluginUserDataPath"/>.
        /// </summary>
        /// <param name="pluginUserDataPath">Plugin user data folder; archive is stored in this directory.</param>
        /// <param name="pluginName">Plugin display name used in the archive file name.</param>
        /// <param name="archiveNameFormat">Optional <see cref="string.Format"/> pattern ({0} = plugin name, {1} = UTC timestamp).</param>
        /// <returns>Backup result with archive path or error details.</returns>
        public static SsvArchiveSettingsBackupResult TryCreateSettingsBackupZip(
            string pluginUserDataPath,
            string pluginName,
            string archiveNameFormat = null)
        {
            if (string.IsNullOrEmpty(pluginUserDataPath))
            {
                return Failed("Plugin user data path is not set.");
            }

            if (!Directory.Exists(pluginUserDataPath))
            {
                return Failed(string.Format("Plugin user data directory not found: '{0}'.", pluginUserDataPath));
            }

            string[] filesToBackup = GetSettingsFilesToBackup(pluginUserDataPath);
            if (filesToBackup.Length == 0)
            {
                return Failed(string.Format(
                    "Settings file '{0}' not found in '{1}'.",
                    PlaynitePaths.ConfigFileName,
                    pluginUserDataPath));
            }

            string format = string.IsNullOrEmpty(archiveNameFormat) ? MigrationArchiveNameFormat : archiveNameFormat;
            string archivePath = Path.Combine(
                pluginUserDataPath,
                string.Format(format, pluginName, DateTime.UtcNow));

            int archivedCount = CreateMigrationArchive(filesToBackup, archivePath);
            if (archivedCount != filesToBackup.Length)
            {
                return Failed(string.Format(
                    "Backup incomplete ({0}/{1} file(s)).",
                    archivedCount,
                    filesToBackup.Length));
            }

            Logger.Info(string.Format(
                "[SsvArchiveMigration] Settings backup created: '{0}' ({1} file(s)).",
                archivePath,
                archivedCount));

            CommonPluginsShared.Common.LogDebug(true, string.Format(
                "[SsvArchiveMigration] Settings backup created: '{0}'",
                archivePath));

            return new SsvArchiveSettingsBackupResult
            {
                Success = true,
                ArchivePath = archivePath,
                ArchivedFileCount = archivedCount
            };
        }

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
        /// Returns whether the one-shot archive configuration migration has already completed.
        /// </summary>
        /// <param name="pluginUserDataPath">Plugin user data folder.</param>
        /// <returns><c>true</c> when the marker file exists.</returns>
        public static bool IsMigrationCompleted(string pluginUserDataPath)
        {
            string markerPath = GetMigrationMarkerPath(pluginUserDataPath);
            return !string.IsNullOrEmpty(markerPath) && File.Exists(markerPath);
        }

        /// <summary>
        /// Schedules the one-shot archive settings migration on startup when needed.
        /// </summary>
        /// <param name="settings">Current plugin settings.</param>
        /// <param name="pluginUserDataPath">Plugin user data folder.</param>
        /// <param name="pluginName">Plugin display name.</param>
        /// <param name="saveSettings">Persists settings after cleanup.</param>
        /// <param name="onCompleted">Optional callback invoked when migration is skipped or finished (success or failure).</param>
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

            SsvArchiveDuplicateAnalysis analysis = SsvArchiveFolderHelper.AnalyzePersistedArchiveDuplicatesForMigration(settings);
            if (IsMigrationCompleted(pluginUserDataPath))
            {
                if (!analysis.HasGlobalArchiveReference || analysis.TotalRemovable == 0)
                {
                    onCompleted?.Invoke();
                    return;
                }

                Logger.Warn("[SsvArchiveMigration] Resuming migration — migratable archive entries remain after a previous run.");
            }
            else if (!analysis.HasGlobalArchiveReference)
            {
                WriteMigrationMarker(pluginUserDataPath, 0, null, "No global archive folder configured.");
                onCompleted?.Invoke();
                return;
            }

            if (analysis.TotalRemovable == 0)
            {
                WriteMigrationMarker(pluginUserDataPath, 0, null, "No archive duplicates found.");
                onCompleted?.Invoke();
                return;
            }

            GlobalProgressOptions progressOptions = new GlobalProgressOptions(
                string.Format("{0} - {1}", pluginName, ResourceProvider.GetString("LOCSsvArchiveMigrationTitle")))
            {
                Cancelable = false,
                IsIndeterminate = true
            };

            _ = API.Instance.Dialogs.ActivateGlobalProgress(
                progress =>
                {
                    RunMigrationWithProgress(settings, pluginUserDataPath, pluginName, saveSettings, progress);
                    onCompleted?.Invoke();
                },
                progressOptions);
        }

        /// <summary>
        /// Runs backup, duplicate cleanup, settings save, and marker write under a progress dialog.
        /// </summary>
        /// <param name="settings">Current plugin settings.</param>
        /// <param name="pluginUserDataPath">Plugin user data folder.</param>
        /// <param name="pluginName">Plugin display name.</param>
        /// <param name="saveSettings">Persists settings after cleanup.</param>
        /// <param name="progress">Progress dialog arguments.</param>
        /// <returns>Migration result.</returns>
        public static SsvArchiveMigrationResult RunMigrationWithProgress(
            ScreenshotsVisualizerSettings settings,
            string pluginUserDataPath,
            string pluginName,
            Action<ScreenshotsVisualizerSettings> saveSettings,
            GlobalProgressActionArgs progress)
        {
            if (progress != null)
            {
                progress.Text = ResourceProvider.GetString("LOCSsvArchiveMigrationCreatingBackup");
            }

            SsvArchiveSettingsBackupResult backup = TryCreateSettingsBackupZip(pluginUserDataPath, pluginName);
            if (!backup.Success)
            {
                return FailedMigration(pluginName, backup.ErrorMessage, progress);
            }

            try
            {
                if (progress != null)
                {
                    progress.Text = ResourceProvider.GetString("LOCSsvArchiveMigrationCleaningDuplicates");
                }

                int removed = SsvArchiveFolderHelper.RemovePersistedArchiveDuplicatesForMigration(settings);
                if (removed <= 0)
                {
                    return FailedMigration(
                        pluginName,
                        ResourceProvider.GetString("LOCSsvArchiveMigrationNoDuplicatesRemoved"),
                        progress);
                }

                if (progress != null)
                {
                    progress.Text = ResourceProvider.GetString("LOCSsvArchiveMigrationSavingSettings");
                }

                saveSettings?.Invoke(settings);

                WriteMigrationMarker(pluginUserDataPath, removed, backup.ArchivePath, null);

                if (progress != null)
                {
                    progress.Text = ResourceProvider.GetString("LOCSsvArchiveMigrationCompleted");
                }

                Logger.Info(string.Format(
                    "[SsvArchiveMigration] Completed — removed {0} duplicate(s), backup '{1}'.",
                    removed,
                    backup.ArchivePath));

                CommonPluginsShared.Common.LogDebug(true, string.Format(
                    "[SsvArchiveMigration] Completed — removed {0} duplicate(s).",
                    removed));

                return new SsvArchiveMigrationResult
                {
                    Success = true,
                    RemovedDuplicates = removed,
                    ArchivePath = backup.ArchivePath
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[SsvArchiveMigration] Migration failed after backup.");
                return FailedMigration(pluginName, ex.Message, progress);
            }
        }

        /// <summary>
        /// Writes the one-shot migration marker file.
        /// </summary>
        /// <param name="pluginUserDataPath">Plugin user data folder.</param>
        /// <param name="removedDuplicates">Number of duplicates removed.</param>
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
        }

        private static SsvArchiveMigrationResult FailedMigration(
            string pluginName,
            string message,
            GlobalProgressActionArgs progress)
        {
            Logger.Warn(string.Format("[SsvArchiveMigration] Migration failed: {0}", message));

            if (progress != null)
            {
                progress.Text = string.Format(
                    ResourceProvider.GetString("LOCSsvArchiveMigrationFailed"),
                    message);
            }

            if (!string.IsNullOrEmpty(pluginName))
            {
                API.Instance.Notifications.Add(new NotificationMessage(
                    string.Format("{0}-ArchiveMigration", pluginName),
                    string.Format("{0}\r\n{1}", pluginName, string.Format(
                        ResourceProvider.GetString("LOCSsvArchiveMigrationFailed"),
                        message)),
                    NotificationType.Error));
            }

            return new SsvArchiveMigrationResult
            {
                Success = false,
                ErrorMessage = message
            };
        }

        private static SsvArchiveSettingsBackupResult Failed(string message)
        {
            Logger.Warn(string.Format("[SsvArchiveMigration] Settings backup failed: {0}", message));
            return new SsvArchiveSettingsBackupResult
            {
                Success = false,
                ErrorMessage = message
            };
        }

        private static int CreateMigrationArchive(string[] files, string archivePath)
        {
            try
            {
                int archivedCount = 0;
                using (FileStream archiveStream = new FileStream(archivePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                using (ZipArchive archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, false))
                {
                    HashSet<string> usedEntryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (string file in files)
                    {
                        string entryName = Path.GetFileName(file);
                        while (!usedEntryNames.Add(entryName))
                        {
                            entryName = string.Format(
                                "{0}_{1}{2}",
                                Path.GetFileNameWithoutExtension(file),
                                Guid.NewGuid().ToString("N"),
                                Path.GetExtension(file));
                        }

                        ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                        using (Stream entryStream = entry.Open())
                        using (FileStream fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            fileStream.CopyTo(entryStream);
                        }

                        archivedCount++;
                    }
                }

                return archivedCount;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, string.Format(
                    "[SsvArchiveMigration] Failed to create archive '{0}'.",
                    archivePath));

                if (File.Exists(archivePath))
                {
                    try
                    {
                        File.Delete(archivePath);
                    }
                    catch (Exception deleteEx)
                    {
                        Logger.Error(deleteEx, string.Format(
                            "[SsvArchiveMigration] Failed to delete incomplete archive '{0}'.",
                            archivePath));
                    }
                }

                return 0;
            }
        }
    }
}
