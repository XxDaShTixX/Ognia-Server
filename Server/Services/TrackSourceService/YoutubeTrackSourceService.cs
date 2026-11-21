using Ognia.Server.Enums;
using Ognia.Server.Models;
using YoutubeExplode;

namespace Ognia.Server.Services.TrackSourceService
{
    public class YouTubeTrackSourceService : ITrackSourceService
    {
        private readonly YoutubeClient _client = new();

        public TrackSource SourceType => TrackSource.YouTube;

        public bool CanHandle(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            return url.Contains("youtube.com", StringComparison.OrdinalIgnoreCase)
                || url.Contains("youtu.be", StringComparison.OrdinalIgnoreCase);
        }

        public string? ExtractId(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;

            var host = uri.Host.ToLowerInvariant();

            if (host is "youtu.be")
            {
                var id = uri.AbsolutePath.Trim('/');
                return IsValidId(id) ? id : null;
            }

            if (host is "youtube.com" or "www.youtube.com" or "m.youtube.com" or "music.youtube.com")
            {
                if (uri.AbsolutePath == "/watch")
                {
                    var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                    var id = query["v"];
                    return IsValidId(id) ? id : null;
                }

                var segments = uri.AbsolutePath.Trim('/').Split('/');
                if (segments.Length >= 2 && segments[0] is "embed" or "v" or "shorts")
                    return IsValidId(segments[1]) ? segments[1] : null;
            }

            return null;
        }

        public bool IsPlaylistUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;

            var host = uri.Host.ToLowerInvariant();
            if (host is not ("youtube.com" or "www.youtube.com" or "m.youtube.com" or "music.youtube.com"))
                return false;

            if (uri.AbsolutePath == "/playlist") return true;
            if (uri.AbsolutePath == "/watch")
            {
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                return !string.IsNullOrWhiteSpace(query["list"]);
            }
            return false;
        }

        public async Task<List<string>?> ExpandPlaylistAsync(string url, CancellationToken ct = default)
        {
            if (!IsPlaylistUrl(url)) return null;

            var playlistId = ExtractPlaylistId(url);
            if (string.IsNullOrWhiteSpace(playlistId)) return null;

            var urls = new List<string>();
            await foreach (var video in _client.Playlists.GetVideosAsync(playlistId, ct))
            {
                if (!string.IsNullOrWhiteSpace(video.Id))
                    urls.Add($"https://www.youtube.com/watch?v={video.Id}");
            }
            return urls;
        }

        private static string? ExtractPlaylistId(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var id = query["list"];
            return string.IsNullOrWhiteSpace(id) ? null : id;
        }

        public async Task<TrackMetadataModel> FetchMetadataAsync(string url, CancellationToken ct = default)
        {
            var video = await _client.Videos.GetAsync(url, ct);

            return new TrackMetadataModel
            {
                Name = video.Title,
                Duration = video.Duration ?? TimeSpan.Zero,
                ThumbnailUrl = video.Thumbnails
                    .OrderByDescending(t => t.Resolution.Width)
                    .Select(t => t.Url)
                    .FirstOrDefault() ?? string.Empty
            };
        }

        private static bool IsValidId(string? id) =>
            id is { Length: 11 } && id.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');
    }
}
