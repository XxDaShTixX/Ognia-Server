namespace Ognia.Server.Models
{
    public class TrackMetadataModel
    {
        public string Name { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
    }
}
