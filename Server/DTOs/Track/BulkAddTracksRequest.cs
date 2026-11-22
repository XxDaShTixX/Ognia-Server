namespace Ognia.Server.DTOs.Track;

public class BulkAddTracksRequest
{
    public List<BulkAddTrackItem>? Urls { get; set; }
}

public class BulkAddTrackItem
{
    public string Url { get; set; } = string.Empty;
    public string? Name { get; set; }
    public TimeSpan? Duration { get; set; }
    public string? ThumbnailUrl { get; set; }
}