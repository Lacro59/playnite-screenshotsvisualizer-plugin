using CommonPluginsShared;
using Playnite.SDK;
using Playnite.SDK.Data;
using ScreenshotsVisualizer.Models;
using ScreenshotsVisualizer.Views.Settings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace ScreenshotsVisualizer.ViewModels.Settings
{
    /// <summary>
    /// View model for ImageMagick path and custom conversion command list management in settings.
    /// </summary>
    public class SsvImageConversionSettingsViewModel : ObservableObject
    {
        private ScreenshotsVisualizerSettings _settings;
        private SsvImageConversionCustomCmdItem _selectedCommand;

        /// <summary>
        /// Initializes a new image conversion settings view model.
        /// </summary>
        public SsvImageConversionSettingsViewModel()
        {
            Commands = new ObservableCollection<SsvImageConversionCustomCmdItem>();
            AddCommand = new RelayCommand(OpenAddEditor);
            EditCommand = new RelayCommand<object>(EditCommandFromParameter);
            RemoveCommand = new RelayCommand<object>(RemoveCommandFromParameter, CanRemoveCommandFromParameter);
        }

        /// <summary>
        /// Gets the editable conversion command list.
        /// </summary>
        public ObservableCollection<SsvImageConversionCustomCmdItem> Commands { get; }

        /// <summary>
        /// Gets the command that opens the add-profile dialog.
        /// </summary>
        public RelayCommand AddCommand { get; }

        /// <summary>
        /// Gets the command that opens the edit-profile dialog.
        /// </summary>
        public RelayCommand<object> EditCommand { get; }

        /// <summary>
        /// Gets the command that removes the selected or parameterized profile.
        /// </summary>
        public RelayCommand<object> RemoveCommand { get; }

        /// <summary>
        /// Gets or sets the selected conversion profile in the list.
        /// </summary>
        public SsvImageConversionCustomCmdItem SelectedCommand
        {
            get => _selectedCommand;
            set
            {
                if (_selectedCommand != value)
                {
                    _selectedCommand = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasSelectedCommand));
                }
            }
        }

        /// <summary>
        /// Gets whether a conversion profile is selected.
        /// </summary>
        public bool HasSelectedCommand => SelectedCommand != null;

        /// <summary>
        /// Gets whether the conversion command list is empty.
        /// </summary>
        public bool HasNoCommands => Commands.Count == 0;

        /// <summary>
        /// Loads conversion commands from plugin settings.
        /// </summary>
        /// <param name="settings">Current plugin settings instance.</param>
        public void LoadFrom(ScreenshotsVisualizerSettings settings)
        {
            _settings = settings;
            Commands.Clear();

            if (settings?.CustomConversionCmds != null)
            {
                foreach (SsvImageConversionCustomCmd command in settings.CustomConversionCmds.Where(x => x != null))
                {
                    Commands.Add(new SsvImageConversionCustomCmdItem(Serialization.GetClone(command)));
                }
            }

            SelectedCommand = null;
            NotifyListChanged();
        }

        /// <summary>
        /// Writes the edited conversion commands back to plugin settings.
        /// </summary>
        /// <param name="settings">Plugin settings instance to update.</param>
        public void ApplyToSettings(ScreenshotsVisualizerSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            settings.CustomConversionCmds = Commands.Select(x => x.ToModel()).ToList();
        }

        /// <summary>
        /// Opens the editor dialog for the selected conversion profile.
        /// </summary>
        public void EditSelectedCommand()
        {
            if (SelectedCommand != null)
            {
                OpenEditor(SelectedCommand);
            }
        }

        private void OpenAddEditor()
        {
            OpenEditor(null);
        }

        private void EditCommandFromParameter(object parameter)
        {
            SsvImageConversionCustomCmdItem item = parameter as SsvImageConversionCustomCmdItem ?? SelectedCommand;
            if (item != null)
            {
                OpenEditor(item);
            }
        }

        private void RemoveCommandFromParameter(object parameter)
        {
            SsvImageConversionCustomCmdItem item = parameter as SsvImageConversionCustomCmdItem ?? SelectedCommand;
            if (item == null || !Commands.Contains(item))
            {
                return;
            }

            _ = Commands.Remove(item);
            if (SelectedCommand == item)
            {
                SelectedCommand = null;
            }

            NotifyListChanged();
        }

        private bool CanRemoveCommandFromParameter(object parameter)
        {
            SsvImageConversionCustomCmdItem item = parameter as SsvImageConversionCustomCmdItem ?? SelectedCommand;
            return item != null && Commands.Contains(item);
        }

        private void OpenEditor(SsvImageConversionCustomCmdItem targetEntry)
        {
            bool isAdd = targetEntry == null;
            SsvImageConversionCustomCmdItem workingCopy = isAdd
                ? new SsvImageConversionCustomCmdItem()
                : targetEntry.Clone();

            SsvImageConversionCustomCmdEditorView view = new SsvImageConversionCustomCmdEditorView(
                targetEntry,
                workingCopy,
                Commands,
                GetImageMagickPath(),
                NotifyListChanged);

            string titleKey = isAdd ? "LOCSsvImageConversionAddTitle" : "LOCSsvImageConversionEditTitle";
            Window window = PlayniteUiHelper.CreateExtensionWindow(
                ResourceProvider.GetString(titleKey),
                view,
                new WindowOptions
                {
                    Width = 560,
                    Height = 540,
                    MinWidth = 500,
                    MinHeight = 460,
                    CanBeResizable = true
                });
            window.ResizeMode = ResizeMode.CanResize;
            _ = window.ShowDialog();
        }

        private string GetImageMagickPath()
        {
            return _settings?.ImageMagickPath ?? string.Empty;
        }

        private void NotifyListChanged()
        {
            OnPropertyChanged(nameof(HasNoCommands));
        }
    }
}
