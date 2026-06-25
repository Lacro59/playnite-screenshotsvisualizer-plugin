using Playnite.SDK;
using ScreenshotsVisualizer.ViewModels.Settings;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ScreenshotsVisualizer.Views.Settings
{
    /// <summary>
    /// Dialog for adding or editing an ImageMagick conversion profile.
    /// </summary>
    public partial class SsvImageConversionCustomCmdEditorView : UserControl
    {
        private readonly SsvImageConversionCustomCmdItem _targetEntry;
        private readonly ObservableCollection<SsvImageConversionCustomCmdItem> _commands;
        private readonly Action _onCommandsChanged;
        private readonly SsvImageConversionCustomCmdItem _workingCopy;
        private readonly bool _isAddMode;

        /// <summary>
        /// Initializes a new conversion profile editor view.
        /// </summary>
        /// <param name="targetEntry">Existing entry to edit, or <c>null</c> when adding.</param>
        /// <param name="workingCopy">Editable copy bound to the form.</param>
        /// <param name="commands">Collection that receives new entries in add mode.</param>
        /// <param name="imageMagickPath">Configured ImageMagick path used for command preview.</param>
        /// <param name="onCommandsChanged">Callback invoked after a successful save.</param>
        public SsvImageConversionCustomCmdEditorView(
            SsvImageConversionCustomCmdItem targetEntry,
            SsvImageConversionCustomCmdItem workingCopy,
            ObservableCollection<SsvImageConversionCustomCmdItem> commands,
            string imageMagickPath,
            Action onCommandsChanged)
        {
            _targetEntry = targetEntry;
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _onCommandsChanged = onCommandsChanged;
            _isAddMode = targetEntry == null;
            _workingCopy = workingCopy ?? throw new ArgumentNullException(nameof(workingCopy));

            DataContext = new SsvImageConversionCustomCmdEditorViewModel(_workingCopy, imageMagickPath);
            InitializeComponent();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_workingCopy.Name))
            {
                string titleKey = _isAddMode ? "LOCSsvImageConversionAddTitle" : "LOCSsvImageConversionEditTitle";
                _ = API.Instance.Dialogs.ShowErrorMessage(
                    ResourceProvider.GetString("LOCSsvImageConversionNameRequired"),
                    ResourceProvider.GetString(titleKey));
                return;
            }

            if (string.IsNullOrWhiteSpace(_workingCopy.OutputFormat))
            {
                string titleKey = _isAddMode ? "LOCSsvImageConversionAddTitle" : "LOCSsvImageConversionEditTitle";
                _ = API.Instance.Dialogs.ShowErrorMessage(
                    ResourceProvider.GetString("LOCSsvImageConversionOutputFormatRequired"),
                    ResourceProvider.GetString(titleKey));
                return;
            }

            if (_isAddMode)
            {
                _commands.Add(_workingCopy.Clone());
            }
            else
            {
                _targetEntry.ApplyFrom(_workingCopy);
            }

            _onCommandsChanged?.Invoke();
            CloseDialog(true);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CloseDialog(false);
        }

        private void CloseDialog(bool dialogResult)
        {
            Window window = Window.GetWindow(this);
            if (window == null)
            {
                return;
            }

            window.DialogResult = dialogResult;
            window.Close();
        }
    }
}
