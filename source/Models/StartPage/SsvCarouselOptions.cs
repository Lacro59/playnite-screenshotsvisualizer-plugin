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
        private double _Margin = 10;
        public double Margin { get => _Margin; set => SetValue(ref _Margin, value); }


        private bool _EnableAllRandom = true;
        public bool EnableAllRandom { get => _EnableAllRandom; set => SetValue(ref _EnableAllRandom, value); }
    
        private bool _EnableLowerRezolution = true;
        public bool EnableLowerRezolution { get => _EnableLowerRezolution; set => SetValue(ref _EnableLowerRezolution, value); }
    
        private bool _EnableAutoChange = true;
        public bool EnableAutoChange { get => _EnableAutoChange; set => SetValue(ref _EnableAutoChange, value); }
    
        private int _Time = 10;
        public int Time { get => _Time; set => SetValue(ref _Time, value); }
    
        private int _LimitPerGame = 10;
        public int LimitPerGame { get => _LimitPerGame; set => SetValue(ref _LimitPerGame, value); }
    
        private int _LimitGame = 0;
        public int LimitGame { get => _LimitGame; set => SetValue(ref _LimitGame, value); }
    
        private bool _OnlyMostRecent = true;
        public bool OnlyMostRecent { get => _OnlyMostRecent; set => SetValue(ref _OnlyMostRecent, value); }
    
        private bool _OnlyFavorite = false;
        public bool OnlyFavorite { get => _OnlyFavorite; set => SetValue(ref _OnlyFavorite, value); }
    
        private List<CheckElement> _SourcesList = new List<CheckElement>();
        public List<CheckElement> SourcesList { get => _SourcesList; set => SetValue(ref _SourcesList, value); }
    }
}
