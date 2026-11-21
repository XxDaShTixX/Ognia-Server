namespace Ognia.Server.Entities
{
    public class RefreshTokenModel
    {
        public int Id { get; set; }

        /// <summary>
        /// FK to AspNetUsers.Id
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Store the HASH, never the raw token
        /// </summary>
        public string TokenHash { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? RevokedAt { get; set; }

        /// <summary>
        /// For rotation chains
        /// </summary>
        public string? ReplacedByTokenHash { get; set; }

        public bool IsActive => RevokedAt == null && ExpiresAt > DateTime.UtcNow;
    }
}
