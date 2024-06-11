using CommonPluginsShared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenshotsVisualizer.Models.StartPage
{
    public class SsvCarouselOptions : ObservableObject
    {
        private double margin = 10;
        public double Margin { get => margin; set => SetValue(ref margin, value); }


        private bool enableAllRandom = true;
        public bool EnableAllRandom { get => enableAllRandom; set => SetValue(ref enableAllRandom, value); }

        private bool enableLowerRezolution = true;
        public bool EnableLowerRezolution { get => enableLowerRezolution; set => SetValue(ref enableLowerRezolution, value); }

        private bool enableAutoChange = true;
        public bool EnableAutoChange { get => enableAutoChange; set => SetValue(ref enableAutoChange, value); }

        private int time = 10;
        public int Time { get => time; set => SetValue(ref time, value); }

        private int limitPerGame = 10;
        public int LimitPerGame { get => limitPerGame; set => SetValue(ref limitPerGame, value); }

        private int limitGame = 0;
        public int LimitGame { get => limitGame; set => SetValue(ref limitGame, value); }

        private bool onlyMostRecent = true;
        public bool OnlyMostRecent { get => onlyMostRecent; set => SetValue(ref onlyMostRecent, value); }

        private bool onlyFavorite = false;
        public bool OnlyFavorite { get => onlyFavorite; set => SetValue(ref onlyFavorite, value); }

        private bool withVideo = false;
        public bool WithVideo { get => withVideo; set => SetValue(ref withVideo, value); }

        private bool addGameName = true;
        public bool AddGameName { get => addGameName; set => SetValue(ref addGameName, value); }

        private List<CheckElement> sourcesList = new List<CheckElement>();
        public List<CheckElement> SourcesList { get => sourcesList; set => SetValue(ref sourcesList, value); }
    }
}
