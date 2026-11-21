using System.ComponentModel.DataAnnotations;

namespace Ognia.Server.Entities
{
    /// <summary>
    /// Playlist-Track relationship.
    /// </summary>
    public class PlaylistTrackModel
    {
        public int Id { get; set; }

        public int PlaylistId { get; set; }

        public PlaylistModel Playlist { get; set; } = null!;

        public int TrackId { get; set; }

        public TrackModel Track { get; set; } = null!;

        /// <summary>
        /// Order of track within the playlist
        /// </summary>
        public int Position { get; set; }                        // order within the playlist
        
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }
}
