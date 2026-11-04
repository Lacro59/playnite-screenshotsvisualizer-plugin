using CommonPlayniteShared.Common;
using CommonPluginsShared.Extensions;
using CommonPluginsShared.IO;
using CommonPluginsStores;
using Playnite.SDK;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ScreenshotsVisualizer.Services
{
    /// <summary>
    /// Which sanitization variant produced a resolved screenshot folder path.
    /// </summary>
    public enum SsvPathResolveVariant
    {
        /// <summary>Playnite <see cref="Paths.GetSafePathName"/> (invalid → spaces).</summary>
        Canonical = 0,

        /// <summary>Invalid filename characters replaced with underscores.</summary>
        Underscore = 1,

        /// <summary>Invalid filename characters removed.</summary>
        Strip = 2,

        /// <summary>No existing directory matched any variant.</summary>
        None = 3
    }

    /// <summary>
    /// Result of resolving a configured screenshot folder for a game.
    /// </summary>
    public sealed class SsvPathResolution
    {
        /// <summary>Gets or sets the path to use (existing match, or canonical when missing).</summary>
        public string Path { get; set; }

        /// <summary>Gets or sets the canonical sanitized path (always <see cref="SsvPathResolveVariant.Canonical"/>).</summary>
        public string CanonicalPath { get; set; }

        /// <summary>Gets or sets which variant matched an existing directory.</summary>
        public SsvPathResolveVariant Variant { get; set; }

        /// <summary>Gets or sets whether <see cref="Path"/> exists on disk when <see cref="Variant"/> is not <see cref="SsvPathResolveVariant.None"/>.</summary>
        public bool Exists { get; set; }
    }

    /// <summary>
    /// Resolves folder paths and file patterns for screenshot sources.
    /// This service centralizes the same expansion logic used by scan and preview flows.
    /// </summary>
    public class SsvPathResolver
    {
        /// <summary>
        /// Resolves the configured screenshot folder path for a game (canonical sanitize only).
        /// Prefer <see cref="ResolveExistingPath"/> when looking up an on-disk folder.
        /// </summary>
        /// <param name="game">The game used for variable expansion.</param>
        /// <param name="folderSettings">The source settings containing the configured path.</param>
        /// <returns>The expanded and sanitized folder path.</returns>
        public string ResolvePath(Game game, FolderSettings folderSettings)
        {
            return ResolveCanonicalPath(game, folderSettings);
        }

        /// <summary>
        /// Expands and sanitizes the folder with <see cref="Paths.GetSafePathName"/> (spaces).
        /// Use this for create/move destinations.
        /// </summary>
        /// <param name="game">The game used for variable expansion.</param>
        /// <param name="folderSettings">The source settings containing the configured path.</param>
        /// <returns>The canonical sanitized path, or empty when inputs are missing.</returns>
        public string ResolveCanonicalPath(Game game, FolderSettings folderSettings)
        {
            string expanded = ExpandFolderTemplate(game, folderSettings);
            if (expanded.IsNullOrEmpty())
            {
                return string.Empty;
            }

            return PathValidator.GetSafePath(expanded, false);
        }

        /// <summary>
        /// Resolves a folder that exists on disk: canonical (spaces), then underscore, then strip.
        /// When none exist, <see cref="SsvPathResolution.Path"/> is the canonical path and <see cref="SsvPathResolution.Exists"/> is false.
        /// </summary>
        /// <param name="game">The game used for variable expansion.</param>
        /// <param name="folderSettings">The source settings containing the configured path.</param>
        /// <returns>Resolution details for scan, preview, and menu open-folder flows.</returns>
        public SsvPathResolution ResolveExistingPath(Game game, FolderSettings folderSettings)
        {
            string expanded = ExpandFolderTemplate(game, folderSettings);
            if (expanded.IsNullOrEmpty())
            {
                return new SsvPathResolution
                {
                    Path = string.Empty,
                    CanonicalPath = string.Empty,
                    Variant = SsvPathResolveVariant.None,
                    Exists = false
                };
            }

            string canonical = PathValidator.GetSafePath(expanded, false);
            if (!canonical.IsNullOrEmpty() && Directory.Exists(canonical))
            {
                return new SsvPathResolution
                {
                    Path = canonical,
                    CanonicalPath = canonical,
                    Variant = SsvPathResolveVariant.Canonical,
                    Exists = true
                };
            }

            string underscore = BuildSanitizedPath(expanded, SanitizeNameUnderscore);
            if (!underscore.IsNullOrEmpty()
                && !Paths.AreEqual(underscore, canonical)
                && Directory.Exists(underscore))
            {
                return new SsvPathResolution
                {
                    Path = underscore,
                    CanonicalPath = canonical,
                    Variant = SsvPathResolveVariant.Underscore,
                    Exists = true
                };
            }

            string strip = BuildSanitizedPath(expanded, SanitizeNameStrip);
            if (!strip.IsNullOrEmpty()
                && !Paths.AreEqual(strip, canonical)
                && !Paths.AreEqual(strip, underscore)
                && Directory.Exists(strip))
            {
                return new SsvPathResolution
                {
                    Path = strip,
                    CanonicalPath = canonical,
                    Variant = SsvPathResolveVariant.Strip,
                    Exists = true
                };
            }

            return new SsvPathResolution
            {
                Path = canonical ?? string.Empty,
                CanonicalPath = canonical ?? string.Empty,
                Variant = SsvPathResolveVariant.None,
                Exists = false
            };
        }

        /// <summary>
        /// Builds the regex pattern used to match screenshot file names for a game.
        /// </summary>
        /// <param name="game">The game used for variable expansion.</param>
        /// <param name="folderSettings">The source settings containing the configured pattern.</param>
        /// <returns>The anchored regex pattern string for a full file name match, or empty when pattern matching is disabled.</returns>
        public string ResolveFilePatternRegex(Game game, FolderSettings folderSettings)
        {
            if (game == null || folderSettings == null || !folderSettings.UsedFilePattern || string.IsNullOrEmpty(folderSettings.FilePattern))
            {
                return string.Empty;
            }

            string gameName = API.Instance.ExpandGameVariables(game, "{Name}") ?? string.Empty;
            string pattern = PlayniteTools.StringExpandWithStores(game, folderSettings.FilePattern);
            pattern = EscapeRegexSpecialChars(pattern);
            pattern = pattern.Replace("\\*", ".*");
            pattern = pattern.Replace("\\{digit\\}", @"\d+");
            pattern = pattern.Replace("\\{DateModified\\}", @"[0-9]{4}[-_][0-9]{2}[-_][0-9]{2}");
            pattern = pattern.Replace("\\{DateTimeModified\\}", @"[0-9]{4}[-_][0-9]{2}[-_][0-9]{2}[ -_][0-9]{2}[-_][0-9]{2}[-_][0-9]{2}");

            if (!gameName.IsNullOrEmpty())
            {
                string escapedGameName = EscapeRegexSpecialChars(gameName);
                string nameAlternation = BuildGameNameAlternation(gameName);
                if (!escapedGameName.IsNullOrEmpty() && pattern.IndexOf(escapedGameName, StringComparison.Ordinal) >= 0)
                {
                    pattern = pattern.Replace(escapedGameName, nameAlternation);
                }
                else
                {
                    string escapedSafe = EscapeRegexSpecialChars(Paths.GetSafePathName(gameName));
                    if (!escapedSafe.IsNullOrEmpty() && pattern.IndexOf(escapedSafe, StringComparison.Ordinal) >= 0)
                    {
                        pattern = pattern.Replace(escapedSafe, nameAlternation);
                    }
                }
            }

            return "^" + pattern + "$";
        }

        private static string ExpandFolderTemplate(Game game, FolderSettings folderSettings)
        {
            if (game == null || folderSettings == null || string.IsNullOrEmpty(folderSettings.ScreenshotsFolder))
            {
                return string.Empty;
            }

            return PlayniteTools.StringExpandWithStores(game, folderSettings.ScreenshotsFolder);
        }

        private static string BuildGameNameAlternation(string gameName)
        {
            List<string> alternatives = new List<string>();
            AddFlexibleNameAlternative(alternatives, Paths.GetSafePathName(gameName));
            AddFlexibleNameAlternative(alternatives, SanitizeNameUnderscore(gameName));
            AddFlexibleNameAlternative(alternatives, SanitizeNameStrip(gameName));

            if (alternatives.Count == 0)
            {
                return ToFlexibleNamePattern(gameName);
            }

            if (alternatives.Count == 1)
            {
                return alternatives[0];
            }

            return "(?:" + string.Join("|", alternatives) + ")";
        }

        private static void AddFlexibleNameAlternative(List<string> alternatives, string name)
        {
            if (name.IsNullOrEmpty())
            {
                return;
            }

            string flexible = ToFlexibleNamePattern(name);
            if (flexible.IsNullOrEmpty())
            {
                return;
            }

            if (!alternatives.Any(x => string.Equals(x, flexible, StringComparison.Ordinal)))
            {
                alternatives.Add(flexible);
            }
        }

        private static string ToFlexibleNamePattern(string name)
        {
            return EscapeRegexSpecialChars(name).Replace(" ", "[ ]*");
        }

        private static string BuildSanitizedPath(string path, Func<string, string> sanitizeName)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            path = Paths.FixSeparators(path);
            List<string> pathFolders = path.Split('\\').ToList();
            string pathReturn = string.Empty;

            for (int i = 0; i < pathFolders.Count; i++)
            {
                string folder = pathFolders[i];

                if (pathReturn.IsNullOrEmpty())
                {
                    pathReturn += folder;
                }
                else
                {
                    pathReturn += "\\" + sanitizeName(folder);
                }
            }

            return pathReturn;
        }

        /// <summary>
        /// Replaces invalid filename characters with underscores, collapses spaces and repeated underscores.
        /// </summary>
        /// <param name="filename">Raw file or folder name segment.</param>
        /// <returns>Sanitized segment.</returns>
        internal static string SanitizeNameUnderscore(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                return filename;
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            char[] buffer = new char[filename.Length];
            int len = 0;
            for (int i = 0; i < filename.Length; i++)
            {
                char c = filename[i];
                if (Array.IndexOf(invalid, c) >= 0)
                {
                    c = '_';
                }

                buffer[len++] = c;
            }

            string result = new string(buffer, 0, len);
            result = Regex.Replace(result, @"\s+", " ").Trim();
            result = Regex.Replace(result, "_{2,}", "_");
            return result;
        }

        /// <summary>
        /// Removes invalid filename characters, then collapses whitespace.
        /// </summary>
        /// <param name="filename">Raw file or folder name segment.</param>
        /// <returns>Sanitized segment.</returns>
        internal static string SanitizeNameStrip(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                return filename;
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            char[] buffer = new char[filename.Length];
            int len = 0;
            for (int i = 0; i < filename.Length; i++)
            {
                char c = filename[i];
                if (Array.IndexOf(invalid, c) < 0)
                {
                    buffer[len++] = c;
                }
            }

            return Regex.Replace(new string(buffer, 0, len), @"\s+", " ").Trim();
        }

        private static string EscapeRegexSpecialChars(string input)
        {
            if (input == null)
            {
                return string.Empty;
            }

            string specialChars = @".^$*+?(){}[]|\";
            StringBuilder escapedString = new StringBuilder();

            foreach (char c in input)
            {
                if (specialChars.IndexOf(c) >= 0)
                {
                    _ = escapedString.Append('\\');
                }

                _ = escapedString.Append(c);
            }

            return escapedString.ToString();
        }
    }
}
