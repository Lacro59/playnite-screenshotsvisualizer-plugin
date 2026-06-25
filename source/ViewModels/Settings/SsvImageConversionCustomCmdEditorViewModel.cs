using Playnite.SDK;
using ScreenshotsVisualizer.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ScreenshotsVisualizer.ViewModels.Settings
{
    /// <summary>
    /// View model for the ImageMagick conversion profile editor dialog.
    /// </summary>
    public class SsvImageConversionCustomCmdEditorViewModel : INotifyPropertyChanged
    {
        private readonly string _imageMagickPath;

        /// <summary>
        /// Initializes a new editor view model.
        /// </summary>
        /// <param name="workingCopy">Editable conversion profile copy bound to the form.</param>
        /// <param name="imageMagickPath">Configured ImageMagick executable path used for preview.</param>
        public SsvImageConversionCustomCmdEditorViewModel(
            SsvImageConversionCustomCmdItem workingCopy,
            string imageMagickPath)
        {
            WorkingCopy = workingCopy ?? new SsvImageConversionCustomCmdItem();
            _imageMagickPath = imageMagickPath ?? string.Empty;
            WorkingCopy.PropertyChanged += WorkingCopy_PropertyChanged;
            UpdateCommandPreview();
        }

        /// <summary>
        /// Gets the editable conversion profile.
        /// </summary>
        public SsvImageConversionCustomCmdItem WorkingCopy { get; }

        /// <summary>
        /// Gets the live ImageMagick command preview.
        /// </summary>
        public string CommandPreview { get; private set; } = string.Empty;

        private void WorkingCopy_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            UpdateCommandPreview();
        }

        /// <summary>
        /// Refreshes the command preview from the current form values.
        /// </summary>
        public void UpdateCommandPreview()
        {
            CommandPreview = ImageMagickCommandBuilder.GetCommandPreview(
                _imageMagickPath,
                WorkingCopy.ToModel());
            NotifyPropertyChanged(nameof(CommandPreview));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
