using Newtonsoft.Json;
using CommonPluginsShared.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenshotsVisualizer.Models
{
    public class GameScreenshots : PluginDataBaseGame<Screenshot>
    {
        private List<Screenshot> _Items = new List<Screenshot>();
        public override List<Screenshot> Items
        {
            get
            {
                return _Items;
            }

            set
            {
                _Items = value;
                OnPropertyChanged();
            }
        }

        public string ScreenshotsFolder { get; set; }

        [JsonIgnore]
        public bool InSettings { get; set; }
    }
}
