using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ongen
{
    public class SourceGame
    {
        public string Name { get; set; }
        public int Id { get; set; }
        public string Directory { get; set; }
        public string ToCfg { get; set; }
        public string LibraryName { get; set; }
        public bool VoiceFadeOut { get; set; } = true;
        public string ExeName { get; set; } = "hl2";

        public string FileExtension { get; set; } = ".wav";
        public int SampleRate { get; set; } = 11025;
        public int Bits { get; set; } = 16;
        public int Channels { get; set; } = 1;

        public int PollInterval { get; set; } = 100;

        public ObservableCollection<Track> Tracks { get; set; } = new();
        public List<string> Blacklist { get; set; } = new()
        {
            "ongen", 
            "ongen_listtracks", 
            "list", 
            "tracks", 
            "la", 
            "ongen_play", 
            "ongen_play_on", 
            "ongen_play_off", 
            "ongen_updatecfg", 
            "ongen_curtrack", 
            "ongen_saycurtrack", 
            "ongen_sayteamcurtrack"
        };
    }

    public class Track
    {
        public bool Loaded { get; set; } = false;
        public string Name { get; set; }
        public List<string> Tags { get; set; } = new();
        public string Hotkey { get; set; } = null;
        public int Volume { get; set; } = 100;
        public int StartPos { get; set; }
        public int EndPos { get; set; }
        public bool Trimmed { get; set; } = false;
    }
}
