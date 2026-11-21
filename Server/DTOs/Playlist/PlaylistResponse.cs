namespace Ognia.Server.DTOs.Playlist
{
    public class PlaylistResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int PlaylistTracksCount { get; set; }
        public List<string> ThumbnailUrls { get; set; } = new();
        public List<CoverThumbnailResponse> CoverThumbnails { get; set; } = new();
    }
}
