using CommonPluginsStores;
using CommonPluginsShared.IO;
using Playnite.SDK;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

namespace ScreenshotsVisualizer.Services
{
    /// <summary>
    /// Resolves folder paths and file patterns for screenshot sources.
    /// This service centralizes the same expansion logic used by scan and preview flows.
    /// </summary>
    public class SsvPathResolver
    {
        private static readonly ILogger _logger = LogManager.GetLogger();

        /// Remove only illegal characters, do not compress spaces.
        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Unnamed";

            var invalid = Path.GetInvalidFileNameChars();
            var result = string.Concat(name.Split(invalid)).Trim();

            return string.IsNullOrWhiteSpace(result) ? "Unnamed" : result;
        }

        private static string GetSafePathPreserveSpaces(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            try
            {
                char[] invalidPathChars = Path.GetInvalidPathChars();
                foreach (char c in invalidPathChars)
                {
                    path = path.Replace(c.ToString(), "");
                }

                return Path.GetFullPath(path);
            }
            catch
            {
                return path;
            }
        }

        /// <summary>
        /// Resolves the configured screenshot folder path for a game.
        /// </summary>
        /// <param name="game">The game used for variable expansion.</param>
        /// <param name="folderSettings">The source settings containing the configured path.</param>
        /// <returns>The expanded and sanitized folder path.</returns>
        public string ResolvePath(Game game, FolderSettings folderSettings)
        {
            if (game == null || folderSettings == null || string.IsNullOrEmpty(folderSettings.ScreenshotsFolder))
            {
                return string.Empty;
            }

            string originalPath = folderSettings.ScreenshotsFolder;
            string originalGameName = game.Name;
            string sanitizedGameName = SanitizeFileName(originalGameName);

            _logger.Debug($"[SsvPathResolver] ResolvePath START:");
            _logger.Debug($"  ├─ Original game name: '{originalGameName}'");
            _logger.Debug($"  ├─ Sanitized game name: '{sanitizedGameName}'");
            _logger.Debug($"  ├─ Original folder pattern: '{originalPath}'");

            // First, manually replace {Name} with the cleaned-up name.
            string pathAfterNameReplace = originalPath.Replace("{Name}", sanitizedGameName);
            _logger.Debug($"  ├─ After {{Name}} replace: '{pathAfterNameReplace}'");

            // Then, let PlayniteTools expand the other variables.
            string expandedPath = PlayniteTools.StringExpandWithStores(game, pathAfterNameReplace);
            _logger.Debug($"  ├─ After StringExpandWithStores: '{expandedPath}'");

            // Clean up using a custom path (keep spaces, do not compress).
            string safePath = GetSafePathPreserveSpaces(expandedPath);
            _logger.Debug($"  └─ Final safe path: '{safePath}'");

            return safePath;
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

            string originalPattern = folderSettings.FilePattern;
            string originalGameName = game.Name;
            string sanitizedGameName = SanitizeFileName(originalGameName);

            _logger.Debug($"[SsvPathResolver] ResolveFilePatternRegex START:");
            _logger.Debug($"  ├─ Original game name: '{originalGameName}'");
            _logger.Debug($"  ├─ Sanitized game name: '{sanitizedGameName}'");
            _logger.Debug($"  ├─ Original file pattern: '{originalPattern}'");

            // First, replace {Name} with the cleaned-up name.
            string patternAfterNameReplace = originalPattern.Replace("{Name}", sanitizedGameName);
            _logger.Debug($"  ├─ After {{Name}} replace: '{patternAfterNameReplace}'");

            // Then let PlayniteTools expand the other variables.
            string expandedPattern = PlayniteTools.StringExpandWithStores(game, patternAfterNameReplace);
            _logger.Debug($"  ├─ After StringExpandWithStores: '{expandedPattern}'");

            string escapedPattern = EscapeRegexSpecialChars(expandedPattern);
            _logger.Debug($"  ├─ After EscapeRegexSpecialChars: '{escapedPattern}'");

            string patternWithWildcards = escapedPattern
                .Replace("\\*", ".*")
                .Replace("\\{digit\\}", @"\d+")
                .Replace("\\{DateModified\\}", @"[0-9]{4}[-_][0-9]{2}[-_][0-9]{2}")
                .Replace("\\{DateTimeModified\\}", @"[0-9]{4}[-_][0-9]{2}[-_][0-9]{2}[ -_][0-9]{2}[-_][0-9]{2}[-_][0-9]{2}");
            _logger.Debug($"  ├─ After wildcard replacements: '{patternWithWildcards}'");

            string gameNameForRegex = sanitizedGameName;
            // Replace each space with [ ]* (matches any number of spaces)
            string safeGameNamePattern = Regex.Escape(gameNameForRegex).Replace("\\ ", "[ ]*");
            _logger.Debug($"  ├─ Game name regex: '{safeGameNamePattern}' (from '{gameNameForRegex}')");

            string finalPattern = patternWithWildcards.Replace(Regex.Escape(gameNameForRegex), safeGameNamePattern);
            _logger.Debug($"  ├─ After game name regex replace: '{finalPattern}'");

            string anchoredPattern = "^" + finalPattern + "$";
            _logger.Debug($"  └─ Final anchored regex: '{anchoredPattern}'");

            return anchoredPattern;
        }

        private static string EscapeRegexSpecialChars(string input)
        {
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
