using System.ComponentModel.DataAnnotations;

namespace Ognia.Server.Entities
{
    public class PlaylistModel
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;       // FK to AspNetUsers.Id
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<PlaylistTrackModel> PlaylistTracks { get; set; } = new List<PlaylistTrackModel>();
    }
}
