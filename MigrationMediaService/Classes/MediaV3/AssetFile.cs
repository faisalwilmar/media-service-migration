using MigrationMediaService.Classes.MediaV3;
using System.Collections.Generic;

namespace MigrationMediaService.Classes.MediaV3
{
    public class AssetFile
    {
        public List<Source> Sources { get; set; }
        public List<VideoTrack> VideoTracks { get; set; }
        public List<AudioTrack> AudioTracks { get; set; }
        public string Name { get; set; }
        public long Size { get; set; }
        public string Duration { get; set; }
    }
}
