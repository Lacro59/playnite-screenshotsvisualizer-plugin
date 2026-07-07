namespace ScreenshotsVisualizer.ViewModels.Settings
{
    /// <summary>
    /// Identifies whether the active configuration context is global or per-game.
    /// </summary>
    public enum SsvConfigurationContextKind
    {
        /// <summary>
        /// Global screenshot sources shared across all games.
        /// </summary>
        Global,

        /// <summary>
        /// Per-game screenshot sources.
        /// </summary>
        Game
    }
}
