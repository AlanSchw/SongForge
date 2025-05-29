using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;

namespace MusicCreate
{
    public class TrackItem
    {
        public string TrackName { get; set; }
        public string TrackInstrument { get; set; }
        public ImageSource TrackIconSource { get; set; }
        public ObservableCollection<Canvas> CanvasItems { get; set; } = new ObservableCollection<Canvas>();

        public TrackItem(string trackName, string trackInstrument, ImageSource trackIconSource)
        {
            TrackName = trackName;
            TrackInstrument = trackInstrument;
            TrackIconSource = trackIconSource;
            //CanvasItems = canvasItems;
        }
    }
}
