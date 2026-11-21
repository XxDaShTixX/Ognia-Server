namespace Ognia.Server.DTOs.Track
{
    public class TrackUsageResponse
    {
        /// <summary>
        /// Number of the current user's playlists that reference the track.
        /// Zero means the track is orphaned and its local file can be deleted.
        /// </summary>
        public int PlaylistCount { get; set; }
    }
}
