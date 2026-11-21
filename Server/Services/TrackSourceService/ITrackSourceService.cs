using Ognia.Server.Enums;
using Ognia.Server.Extensions;
using Ognia.Server.Models;

namespace Ognia.Server.Services.TrackSourceService
{
    public interface ITrackSourceService
    {
        /// <summary>
        /// The source this implementation handles
        /// </summary>
        TrackSource SourceType { get; }

        /// <summary>
        /// The canonical string for this source, derived from SourceType.
        /// </summary>
        string SourceName => SourceType.ToWireName();

        /// <summary>
        /// True if this source can handle the given URL.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        bool CanHandle(string url);

        /// <summary>
        /// Extracts the source-specific identifier from a URL.
        /// Returns null if the URL is not valid for this source.
        /// </summary>
        string? ExtractId(string url);

        /// <summary>
        /// True if the URL points to a playlist for this source rather than a single track.
        /// </summary>
        bool IsPlaylistUrl(string url);

        /// <summary>
        /// Expands a playlist URL into individual track URLs for this source.
        /// Returns null if the URL is not a supported playlist.
        /// </summary>
        Task<List<string>?> ExpandPlaylistAsync(string url, CancellationToken ct = default);

        /// <summary>
        /// Fetch metadata (title, duration) for the given source ID.
        /// </summary>
        /// <param name="sourceId"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task<TrackMetadataModel> FetchMetadataAsync(string sourceId, CancellationToken ct = default);
    }
}
