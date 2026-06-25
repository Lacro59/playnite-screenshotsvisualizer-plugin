using CommonPluginsControls.Controls;
using CommonPluginsShared;
using CommonPluginsShared.UI;
using Playnite.SDK;
using ScreenshotsVisualizer.ViewModels.Settings;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace ScreenshotsVisualizer.Views.Settings
{
    /// <summary>
    /// Dialog for adding or editing a screenshot folder source in the active configuration context.
    /// </summary>
    public partial class SsvFolderSourceEditorView : UserControl
    {
        private readonly FolderEntryItem _targetEntry;
        private readonly ObservableCollection<FolderEntryItem> _activeSources;
        private readonly Action _onSourcesChanged;
        private readonly FolderEntryItem _workingCopy;
        private readonly SsvFolderSourceEditorViewModel _viewModel;
        private readonly bool _isAddMode;

        /// <summary>
        /// Initializes a new folder source editor view.
        /// </summary>
        /// <param name="targetEntry">Existing entry to edit, or <c>null</c> when adding a new source.</param>
        /// <param name="activeSources">Collection that receives new entries in add mode.</param>
        /// <param name="onSourcesChanged">Callback invoked after a successful save.</param>
        public SsvFolderSourceEditorView(
            FolderEntryItem targetEntry,
            ObservableCollection<FolderEntryItem> activeSources,
            Guid? preferredGameId,
            Action onSourcesChanged)
        {
            _targetEntry = targetEntry;
            _activeSources = activeSources ?? throw new ArgumentNullException(nameof(activeSources));
            _onSourcesChanged = onSourcesChanged;
            _isAddMode = targetEntry == null;
            _workingCopy = _isAddMode
                ? new FolderEntryItem()
                : new FolderEntryItem(targetEntry.ToModel());

            _viewModel = new SsvFolderSourceEditorViewModel(_workingCopy, preferredGameId);
            DataContext = _viewModel;
            InitializeComponent();
        }

        /// <summary>
        /// Gets whether the editor was opened to add a new source.
        /// </summary>
        public bool IsAddMode => _isAddMode;

        private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
        {
            string selectedFolder = API.Instance.Dialogs.SelectFolder();
            if (!string.IsNullOrEmpty(selectedFolder))
            {
                _workingCopy.ScreenshotsFolder = selectedFolder;
            }
        }

        private void ReplaceDigitButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_workingCopy.FilePattern))
            {
                return;
            }

            _workingCopy.FilePattern = Regex.Replace(_workingCopy.FilePattern, @"\d+", "{digit}");
        }

        private void SelectVariableButton_Click(object sender, RoutedEventArgs e)
        {
            SelectVariable viewExtension = new SelectVariable();
            Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(
                ResourceProvider.GetString("LOCCommonSelectVariable"),
                viewExtension);
            windowExtension.ResizeMode = ResizeMode.CanResize;
            _ = windowExtension.ShowDialog();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_workingCopy.ScreenshotsFolder))
            {
                string titleKey = _isAddMode ? "LOCSsvConfigAddSourceTitle" : "LOCSsvConfigEditSourceTitle";
                _ = API.Instance.Dialogs.ShowErrorMessage(
                    ResourceProvider.GetString("LOCSsvConfigSourcePathRequired"),
                    ResourceProvider.GetString(titleKey));
                return;
            }

            if (_isAddMode)
            {
                _activeSources.Add(new FolderEntryItem(_workingCopy.ToModel()));
            }
            else
            {
                _targetEntry.ApplyFrom(_workingCopy);
            }

            _onSourcesChanged?.Invoke();
            CloseDialog(true);
        }

        private void OpenResolvedFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null || string.IsNullOrEmpty(_viewModel.ResolvedPath) || !Directory.Exists(_viewModel.ResolvedPath))
            {
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = _viewModel.ResolvedPath,
                UseShellExecute = true
            });
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
