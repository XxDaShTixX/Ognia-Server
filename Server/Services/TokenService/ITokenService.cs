using Ognia.Server.Models;

namespace Ognia.Server.Services.TokenService
{
    public interface ITokenService
    {
        /// <summary>Generates a signed JWT access token for the given user.</summary>
        string GenerateAccessToken(AppUserModel user);

        /// <summary>
        /// Creates a new refresh token for the user, persists its hash,
        /// and returns the RAW token (the only time the raw value leaves the server).
        /// </summary>
        Task<string> IssueRefreshTokenAsync(string userId);

        /// <summary>
        /// Validates a raw refresh token, revokes it, issues a replacement,
        /// and returns the owning user plus the new raw refresh token.
        /// Returns null if the token is missing, unknown, revoked, or expired.
        /// </summary>
        Task<(AppUserModel User, string NewRefreshToken)?> ValidateAndRotateAsync(string rawToken);

        /// <summary>
        /// Revokes a raw refresh token if it exists. Idempotent.
        /// </summary>
        Task RevokeAsync(string rawToken);
    }
}