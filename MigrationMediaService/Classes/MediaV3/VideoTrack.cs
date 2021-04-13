namespace MigrationMediaService.Classes.MediaV3 {
    public class VideoTrack
    {
        public long Id { get; set; }
        public string FourCc { get; set; }
        public string Profile { get; set; }
        public string Level { get; set; }
        public long Width { get; set; }
        public long Height { get; set; }
        public long DisplayAspectRatioNumerator { get; set; }
        public long DisplayAspectRatioDenominator { get; set; }
        public long Framerate { get; set; }
        public long Bitrate { get; set; }
        public long TargetBitrate { get; set; }
    }
}
