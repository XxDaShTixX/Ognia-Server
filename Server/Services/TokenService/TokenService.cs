using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Ognia.Server.Entities;
using Ognia.Server.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Ognia.Server.Services.TokenService
{
    public class TokenService : ITokenService
    {
        private readonly AppDbContext _db;
        private readonly UserManager<AppUserModel> _userManager;
        private readonly JwtSettingsModel _jwtSettings;

        public TokenService(
            AppDbContext db,
            UserManager<AppUserModel> userManager,
            IOptions<JwtSettingsModel> jwtOptions)
        {
            _db = db;
            _userManager = userManager;
            _jwtSettings = jwtOptions.Value;
        }

        public string GenerateAccessToken(AppUserModel user)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email!),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task<string> IssueRefreshTokenAsync(string userId)
        {
            var rawToken = GenerateRawToken();
            var entity = new RefreshTokenModel
            {
                UserId = userId,
                TokenHash = HashToken(rawToken),
                ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays),
                CreatedAt = DateTime.UtcNow
            };

            _db.RefreshTokens.Add(entity);
            await _db.SaveChangesAsync();

            return rawToken;
        }

        public async Task<(AppUserModel User, string NewRefreshToken)?> ValidateAndRotateAsync(string rawToken)
        {
            if (string.IsNullOrWhiteSpace(rawToken)) return null;

            var hash = HashToken(rawToken);
            var existing = await _db.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.TokenHash == hash);

            if (existing == null || !existing.IsActive) return null;

            var user = await _userManager.FindByIdAsync(existing.UserId);
            if (user == null) return null;

            var newRawToken = GenerateRawToken();
            var newHash = HashToken(newRawToken);

            existing.RevokedAt = DateTime.UtcNow;
            existing.ReplacedByTokenHash = newHash;

            _db.RefreshTokens.Add(new RefreshTokenModel
            {
                UserId = existing.UserId,
                TokenHash = newHash,
                ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays),
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();

            return (user, newRawToken);
        }

        public async Task RevokeAsync(string rawToken)
        {
            if (string.IsNullOrWhiteSpace(rawToken)) return;

            var hash = HashToken(rawToken);
            var existing = await _db.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.TokenHash == hash);

            if (existing == null || existing.RevokedAt != null) return;

            existing.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        private static string GenerateRawToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(bytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        private static string HashToken(string rawToken)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
            return Convert.ToHexString(bytes);
        }
    }
}