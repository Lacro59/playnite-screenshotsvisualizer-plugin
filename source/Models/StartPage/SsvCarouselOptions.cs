using CommonPluginsShared.Models;
using System.Collections.Generic;

namespace ScreenshotsVisualizer.Models.StartPage
{
    public class SsvCarouselOptions : ObservableObject
    {
        private double _margin = 10;
        public double Margin { get => _margin; set => SetValue(ref _margin, value); }

        private bool _enableAllRandom = true;
        public bool EnableAllRandom { get => _enableAllRandom; set => SetValue(ref _enableAllRandom, value); }

        private bool _enableLowerResolution = true;
        public bool EnableLowerResolution { get => _enableLowerResolution; set => SetValue(ref _enableLowerResolution, value); }

        private bool _enableAutoChange = true;
        public bool EnableAutoChange { get => _enableAutoChange; set => SetValue(ref _enableAutoChange, value); }

        private int _time = 10;
        public int Time { get => _time; set => SetValue(ref _time, value); }

        private int _limitPerGame = 10;
        public int LimitPerGame { get => _limitPerGame; set => SetValue(ref _limitPerGame, value); }

        private int _limitGame = 0;
        public int LimitGame { get => _limitGame; set => SetValue(ref _limitGame, value); }

        private bool _onlyMostRecent = true;
        public bool OnlyMostRecent { get => _onlyMostRecent; set => SetValue(ref _onlyMostRecent, value); }

        private bool _onlyFavorite = false;
        public bool OnlyFavorite { get => _onlyFavorite; set => SetValue(ref _onlyFavorite, value); }

        private bool _withVideo = false;
        public bool WithVideo { get => _withVideo; set => SetValue(ref _withVideo, value); }

        private bool _addGameName = true;
        public bool AddGameName { get => _addGameName; set => SetValue(ref _addGameName, value); }

        private List<CheckElement> _sourcesList = new List<CheckElement>();
        public List<CheckElement> SourcesList { get => _sourcesList; set => SetValue(ref _sourcesList, value); }
    }
}