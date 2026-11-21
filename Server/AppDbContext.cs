using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Ognia.Server.Entities;
using Ognia.Server.Models;

namespace Ognia.Server
{
    public class AppDbContext : IdentityDbContext<AppUserModel>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<TrackModel> Tracks => Set<TrackModel>();
        public DbSet<PlaylistModel> Playlists => Set<PlaylistModel>();
        public DbSet<PlaylistTrackModel> PlaylistTracks => Set<PlaylistTrackModel>();
        public DbSet<RefreshTokenModel> RefreshTokens => Set<RefreshTokenModel>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<PlaylistTrackModel>()
                .HasIndex(pt => new { pt.Id, pt.TrackId })
                .IsUnique(); // only if duplicates are disallowed

            builder.Entity<PlaylistTrackModel>()
                .HasOne(pt => pt.Playlist)
                .WithMany(p => p.PlaylistTracks)
                .HasForeignKey(pt => pt.PlaylistId);

            builder.Entity<PlaylistTrackModel>()
                .HasOne(pt => pt.Track)
                .WithMany() // no back-nav needed unless you add one
                .HasForeignKey(pt => pt.TrackId);

            builder.Entity<TrackModel>()
                .HasIndex(t => new { t.SourceName, t.SourceId })
                .IsUnique();

            builder.Entity<RefreshTokenModel>()
                .HasIndex(rt => rt.TokenHash)
                .IsUnique();

            builder.Entity<RefreshTokenModel>()
                .HasIndex(rt => rt.UserId);
        }
    }
}
