using ScreenshotsVisualizer.ViewModels.Settings;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace ScreenshotsVisualizer.Views.Settings
{
    /// <summary>
    /// Dialog for selecting a Playnite game that is not yet configured for screenshots.
    /// </summary>
    public partial class SsvSelectUnconfiguredGameView : UserControl
    {
        private readonly SsvGamesConfigurationViewModel _gamesConfiguration;
        private readonly SsvConfigurationContextViewModel _configurationContext;

        /// <summary>
        /// Initializes a new unconfigured game selection view.
        /// </summary>
        /// <param name="gamesConfiguration">Shared games configuration view model.</param>
        /// <param name="configurationContext">Configuration context view model updated after selection.</param>
        public SsvSelectUnconfiguredGameView(
            SsvGamesConfigurationViewModel gamesConfiguration,
            SsvConfigurationContextViewModel configurationContext)
        {
            _gamesConfiguration = gamesConfiguration;
            _configurationContext = configurationContext;
            DataContext = new ViewModel(gamesConfiguration.AvailableGames);
            InitializeComponent();
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            ConfirmSelection();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseDialog(false);
        }

        private void ListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ViewModel viewModel && viewModel.HasSelectedGame)
            {
                ConfirmSelection();
            }
        }

        private void ConfirmSelection()
        {
            if (!(DataContext is ViewModel viewModel) || viewModel.SelectedGame == null)
            {
                return;
            }

            AvailableGameItem selectedGame = viewModel.SelectedGame;
            _gamesConfiguration.AddConfiguredGame(selectedGame);
            _configurationContext.SelectGameById(selectedGame.Id);
            CloseDialog(true);
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

        /// <summary>
        /// View model for the unconfigured game selection dialog.
        /// </summary>
        private sealed class ViewModel : INotifyPropertyChanged
        {
            private readonly ObservableCollection<AvailableGameItem> _availableGames;
            private string _searchText = string.Empty;
            private AvailableGameItem _selectedGame;

            public event PropertyChangedEventHandler PropertyChanged;

            /// <summary>
            /// Initializes a new unconfigured game selection view model.
            /// </summary>
            /// <param name="availableGames">Games not yet configured for screenshots.</param>
            public ViewModel(ObservableCollection<AvailableGameItem> availableGames)
            {
                _availableGames = availableGames ?? throw new ArgumentNullException(nameof(availableGames));
                FilteredGamesView = CollectionViewSource.GetDefaultView(_availableGames);
                FilteredGamesView.Filter = FilterGame;

                _availableGames.CollectionChanged += (s, e) =>
                {
                    FilteredGamesView.Refresh();
                    OnPropertyChanged(nameof(AvailableGamesCount));
                    OnPropertyChanged(nameof(HasAvailableGames));
                    OnPropertyChanged(nameof(HasNoAvailableGames));
                };
            }

            /// <summary>
            /// Gets a filtered view of unconfigured games.
            /// </summary>
            public ICollectionView FilteredGamesView { get; }

            /// <summary>
            /// Gets or sets the search text applied to game names.
            /// </summary>
            public string SearchText
            {
                get => _searchText;
                set
                {
                    if (_searchText == value)
                    {
                        return;
                    }

                    _searchText = value;
                    OnPropertyChanged(nameof(SearchText));
                    FilteredGamesView.Refresh();
                }
            }

            /// <summary>
            /// Gets or sets the selected game in the dialog list.
            /// </summary>
            public AvailableGameItem SelectedGame
            {
                get => _selectedGame;
                set
                {
                    if (_selectedGame == value)
                    {
                        return;
                    }

                    _selectedGame = value;
                    OnPropertyChanged(nameof(SelectedGame));
                    OnPropertyChanged(nameof(HasSelectedGame));
                }
            }

            /// <summary>
            /// Gets the number of unconfigured games before search filtering.
            /// </summary>
            public int AvailableGamesCount => _availableGames.Count;

            /// <summary>
            /// Gets whether at least one unconfigured game exists.
            /// </summary>
            public bool HasAvailableGames => _availableGames.Count > 0;

            /// <summary>
            /// Gets whether no unconfigured game remains.
            /// </summary>
            public bool HasNoAvailableGames => !HasAvailableGames;

            /// <summary>
            /// Gets whether a game is selected for confirmation.
            /// </summary>
            public bool HasSelectedGame => SelectedGame != null;

            private bool FilterGame(object item)
            {
                if (string.IsNullOrEmpty(SearchText))
                {
                    return true;
                }

                if (item is AvailableGameItem game)
                {
                    return game.Name.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0;
                }

                return true;
            }

            private void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
