namespace Ognia.Server.Entities
{
    public class TrackModel
    {
        public int Id { get; set; }
        
        /// <summary>
        /// Youtube, Spotify, etc.
        /// </summary>
        public string SourceName { get; set; } = string.Empty;
        
        /// <summary>
        /// Unique identifier for the track within its source.
        /// </summary>
        public string SourceId { get; set; } = string.Empty;
        
        /// <summary>
        /// Name of track.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// URL to the thumbnail.
        /// </summary>
        public string ThumbnailUrl { get; set; } = string.Empty;

        /// <summary>
        /// Length of track. EF maps TimeSpan to SQLite TEXT
        /// </summary>
        public TimeSpan Duration { get; set; }                   
    }
}
