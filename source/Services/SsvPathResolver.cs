using CommonPluginsStores;
using CommonPluginsShared.IO;
using Playnite.SDK;
using Playnite.SDK.Models;
using ScreenshotsVisualizer.Models;
using System.Text;

namespace ScreenshotsVisualizer.Services
{
    /// <summary>
    /// Resolves folder paths and file patterns for screenshot sources.
    /// This service centralizes the same expansion logic used by scan and preview flows.
    /// </summary>
    public class SsvPathResolver
    {
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

            string pathFolder = PlayniteTools.StringExpandWithStores(game, folderSettings.ScreenshotsFolder);
            return PathValidator.GetSafePath(pathFolder, false);
        }

        /// <summary>
        /// Builds the regex pattern used to match screenshot file names for a game.
        /// </summary>
        /// <param name="game">The game used for variable expansion.</param>
        /// <param name="folderSettings">The source settings containing the configured pattern.</param>
        /// <returns>The resolved regex pattern string, or empty when pattern matching is disabled.</returns>
        public string ResolveFilePatternRegex(Game game, FolderSettings folderSettings)
        {
            if (game == null || folderSettings == null || !folderSettings.UsedFilePattern || string.IsNullOrEmpty(folderSettings.FilePattern))
            {
                return string.Empty;
            }

            string pattern = PlayniteTools.StringExpandWithStores(game, folderSettings.FilePattern);
            pattern = EscapeRegexSpecialChars(pattern);
            pattern = pattern.Replace("\\*", ".*");
            pattern = pattern.Replace("\\{digit\\}", @"\d*");
            pattern = pattern.Replace("\\{DateModified\\}", @"[0-9]{4}[-_][0-9]{2}[-_][0-9]{2}");
            pattern = pattern.Replace("\\{DateTimeModified\\}", @"[0-9]{4}[-_][0-9]{2}[-_][0-9]{2}[ -_][0-9]{2}[-_][0-9]{2}[-_][0-9]{2}");

            string gameName = API.Instance.ExpandGameVariables(game, "{Name}");
            string safeGameNamePattern = PathValidator.GetSafePathName(gameName).Replace(" ", "[ ]*");
            return pattern.Replace(gameName, safeGameNamePattern);
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
