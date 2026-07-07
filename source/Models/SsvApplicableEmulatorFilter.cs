namespace ScreenshotsVisualizer.Models
{
    /// <summary>
    /// Optional emulator constraint for a global screenshot source entry.
    /// Used only on <see cref="FolderSettings"/> stored in <c>GlobalScreenshotSources</c>.
    /// </summary>
    public enum SsvApplicableEmulatorFilter
    {
        /// <summary>No emulator filter — any game may match when other criteria pass.</summary>
        None = 0,

        /// <summary>Only games detected as using RetroArch (<c>PlayniteTools.GameUseRetroArch</c>).</summary>
        RetroArch = 1,

        /// <summary>Only games detected as using ScummVM (<c>PlayniteTools.GameUseScummVM</c>).</summary>
        ScummVM = 2
    }
}
