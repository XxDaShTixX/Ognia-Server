namespace Ognia.Server.DTOs.Track
{
    public class TrackResponse
    {
        public int Id { get; set; }
        public string SourceName { get; set; } = string.Empty;
        public string SourceId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public int Position { get; set; }
    }
}
