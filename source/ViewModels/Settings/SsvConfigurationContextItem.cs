using Playnite.SDK;
using System;

namespace ScreenshotsVisualizer.ViewModels.Settings
{
    /// <summary>
    /// Represents one selectable entry in the configuration context list (global node or configured game).
    /// </summary>
    public class SsvConfigurationContextItem
    {
        /// <summary>
        /// Gets the singleton global context item.
        /// </summary>
        public static SsvConfigurationContextItem Global { get; } =
            new SsvConfigurationContextItem(SsvConfigurationContextKind.Global, null);

        private SsvConfigurationContextItem(SsvConfigurationContextKind kind, ConfiguredGameItem game)
        {
            Kind = kind;
            Game = game;
        }

        /// <summary>
        /// Gets the context kind.
        /// </summary>
        public SsvConfigurationContextKind Kind { get; }

        /// <summary>
        /// Gets the configured game when <see cref="Kind"/> is <see cref="SsvConfigurationContextKind.Game"/>; otherwise <c>null</c>.
        /// </summary>
        public ConfiguredGameItem Game { get; }

        /// <summary>
        /// Gets whether this item represents the global context.
        /// </summary>
        public bool IsGlobal => Kind == SsvConfigurationContextKind.Global;

        /// <summary>
        /// Gets the library source name for game contexts.
        /// </summary>
        public string SourceName => Game?.SourceName;

        /// <summary>
        /// Gets the library source icon glyph for game contexts.
        /// </summary>
        public string SourceIcon => Game?.SourceIcon;

        /// <summary>
        /// Gets the game cover icon path for game contexts.
        /// </summary>
        public string Icon => Game?.Icon;

        /// <summary>
        /// Gets the localized or game display name shown in the context list.
        /// </summary>
        public string DisplayName =>
            IsGlobal
                ? ResourceProvider.GetString("LOCSsvConfigContextGlobals")
                : Game?.Name ?? string.Empty;

        /// <summary>
        /// Creates a context item for a configured game.
        /// </summary>
        /// <param name="game">Configured game.</param>
        /// <returns>A game context item.</returns>
        public static SsvConfigurationContextItem ForGame(ConfiguredGameItem game)
        {
            if (game == null)
            {
                throw new ArgumentNullException(nameof(game));
            }

            return new SsvConfigurationContextItem(SsvConfigurationContextKind.Game, game);
        }
    }
}
