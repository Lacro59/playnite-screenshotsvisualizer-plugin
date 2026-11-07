using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace ScreenshotsVisualizer.Services
{
    /// <summary>
    /// Path-based inclusion filter for archive and conversion operations over configured folder sources.
    /// </summary>
    public static class SsvFolderOperationFilter
    {
        /// <summary>
        /// Resolves absolute directory roots for folders included in the archive / move operation.
        /// </summary>
        /// <param name="game">Playnite game used for path expansion.</param>
        /// <param name="folders">Effective folder settings for the game.</param>
        /// <param name="pathResolver">Path resolver used for expansion and sanitize variants.</param>
        /// <returns>Normalized absolute directory roots (may be empty).</returns>
        public static List<string> BuildIncludedArchiveRoots(
            Game game,
            IEnumerable<FolderSettings> folders,
            SsvPathResolver pathResolver)
        {
            return BuildIncludedRoots(game, folders, pathResolver, forArchive: true);
        }

        /// <summary>
        /// Resolves absolute directory roots for folders included in ImageMagick conversion.
        /// </summary>
        /// <param name="game">Playnite game used for path expansion.</param>
        /// <param name="folders">Effective folder settings for the game.</param>
        /// <param name="pathResolver">Path resolver used for expansion and sanitize variants.</param>
        /// <returns>Normalized absolute directory roots (may be empty).</returns>
        public static List<string> BuildIncludedConversionRoots(
            Game game,
            IEnumerable<FolderSettings> folders,
            SsvPathResolver pathResolver)
        {
            return BuildIncludedRoots(game, folders, pathResolver, forArchive: false);
        }

        /// <summary>
        /// Returns whether <paramref name="filePath"/> is under any of the included directory roots.
        /// </summary>
        /// <param name="filePath">Absolute or relative file path.</param>
        /// <param name="includedRoots">Normalized directory roots from <see cref="BuildIncludedArchiveRoots"/> or <see cref="BuildIncludedConversionRoots"/>.</param>
        /// <returns><c>true</c> when the file belongs to at least one included root.</returns>
        public static bool IsFileIncluded(string filePath, IList<string> includedRoots)
        {
            if (string.IsNullOrWhiteSpace(filePath) || includedRoots == null || includedRoots.Count == 0)
            {
                return false;
            }

            string fullFilePath;
            try
            {
                fullFilePath = Path.GetFullPath(filePath);
            }
            catch (Exception)
            {
                return false;
            }

            for (int i = 0; i < includedRoots.Count; i++)
            {
                if (IsPathUnderDirectory(fullFilePath, includedRoots[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<string> BuildIncludedRoots(
            Game game,
            IEnumerable<FolderSettings> folders,
            SsvPathResolver pathResolver,
            bool forArchive)
        {
            List<string> roots = new List<string>();
            if (game == null || folders == null || pathResolver == null)
            {
                return roots;
            }

            foreach (FolderSettings folder in folders)
            {
                if (folder == null || string.IsNullOrEmpty(folder.ScreenshotsFolder))
                {
                    continue;
                }

                if (forArchive)
                {
                    if (!folder.IncludeInArchive)
                    {
                        continue;
                    }
                }
                else if (!folder.IncludeInConversion)
                {
                    continue;
                }

                SsvPathResolution resolution = pathResolver.ResolveExistingPath(game, folder);
                string path = resolution.Exists ? resolution.Path : resolution.CanonicalPath;
                string normalized = NormalizeDirectoryPath(path);
                if (string.IsNullOrEmpty(normalized))
                {
                    continue;
                }

                bool alreadyPresent = false;
                for (int i = 0; i < roots.Count; i++)
                {
                    if (string.Equals(roots[i], normalized, StringComparison.OrdinalIgnoreCase))
                    {
                        alreadyPresent = true;
                        break;
                    }
                }

                if (!alreadyPresent)
                {
                    roots.Add(normalized);
                }
            }

            return roots;
        }

        private static string NormalizeDirectoryPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                string fullPath = Path.GetFullPath(path);
                return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static bool IsPathUnderDirectory(string fullFilePath, string normalizedDirectory)
        {
            if (string.IsNullOrEmpty(fullFilePath) || string.IsNullOrEmpty(normalizedDirectory))
            {
                return false;
            }

            if (string.Equals(fullFilePath, normalizedDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string prefixWithSeparator = normalizedDirectory + Path.DirectorySeparatorChar;
            if (fullFilePath.StartsWith(prefixWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string altPrefix = normalizedDirectory + Path.AltDirectorySeparatorChar;
            return fullFilePath.StartsWith(altPrefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
