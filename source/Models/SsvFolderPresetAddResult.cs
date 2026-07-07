namespace ScreenshotsVisualizer.Models
{
    /// <summary>
    /// Result of adding a built-in folder preset to global screenshot sources.
    /// </summary>
    public enum SsvFolderPresetAddResult
    {
        /// <summary>The preset was added to global sources.</summary>
        Added,

        /// <summary>An equivalent global source entry already exists.</summary>
        AlreadyExists,

        /// <summary>The preset identifier is not registered in the catalog.</summary>
        UnknownPreset
    }
}
