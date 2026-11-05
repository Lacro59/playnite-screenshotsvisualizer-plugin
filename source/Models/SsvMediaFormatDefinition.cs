namespace ScreenshotsVisualizer.Models
{
    /// <summary>
    /// Describes a supported screenshot media extension and how it is handled.
    /// </summary>
    public sealed class SsvMediaFormatDefinition
    {
        /// <summary>
        /// Initializes a new media format definition.
        /// </summary>
        /// <param name="extension">Lower-case extension including the leading dot.</param>
        /// <param name="kind">Image or video category.</param>
        /// <param name="enabledByDefault">Whether the extension is enabled when settings are first created.</param>
        /// <param name="isEssential">Whether the extension must remain enabled when settings are persisted.</param>
        /// <param name="decodeEngine">Primary decode or media pipeline for this format.</param>
        /// <param name="codecHintResourceKey">Localization key describing codec requirements for tooltips.</param>
        public SsvMediaFormatDefinition(
            string extension,
            SsvMediaFormatKind kind,
            bool enabledByDefault,
            bool isEssential,
            SsvMediaDecodeEngine decodeEngine,
            string codecHintResourceKey)
        {
            Extension = extension;
            Kind = kind;
            EnabledByDefault = enabledByDefault;
            IsEssential = isEssential;
            DecodeEngine = decodeEngine;
            CodecHintResourceKey = codecHintResourceKey;
        }

        /// <summary>
        /// Gets the lower-case file extension including the leading dot.
        /// </summary>
        public string Extension { get; }

        /// <summary>
        /// Gets whether the format is an image or a video.
        /// </summary>
        public SsvMediaFormatKind Kind { get; }

        /// <summary>
        /// Gets whether the format is enabled for new installations by default.
        /// </summary>
        public bool EnabledByDefault { get; }

        /// <summary>
        /// Gets whether the format cannot be disabled by the user.
        /// </summary>
        public bool IsEssential { get; }

        /// <summary>
        /// Gets the primary decode or media pipeline for this format.
        /// </summary>
        public SsvMediaDecodeEngine DecodeEngine { get; }

        /// <summary>
        /// Gets the localization resource key for codec requirement hints.
        /// </summary>
        public string CodecHintResourceKey { get; }
    }
}
