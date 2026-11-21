using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ognia.Server.DTOs.Track;
using Ognia.Server.Extensions;

namespace Ognia.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TracksController : ControllerBase
{
    private readonly AppDbContext _db;

    public TracksController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Renames track in the backend.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="request"></param>
    /// <returns></returns>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Rename(int id, [FromBody] RenameTrackRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Name is required." });

        var userId = User.GetUserId();

        var hasAccess = await _db.PlaylistTracks
            .AnyAsync(pt => pt.TrackId == id && pt.Playlist.UserId == userId);

        if (!hasAccess) return NotFound();

        var track = await _db.Tracks.FirstOrDefaultAsync(t => t.Id == id);
        if (track == null) return NotFound();

        var trimmed = request.Name.Trim();

        // Conflict is scoped to the user's playlists: the same track lives in many playlists,
        // so we reject only when a *different* track in *any* of this user's playlists that
        // already contains this track has the same name.
        var playlistIds = await _db.PlaylistTracks
            .Where(pt => pt.TrackId == id && pt.Playlist.UserId == userId)
            .Select(pt => pt.PlaylistId)
            .ToListAsync();

        var nameTaken = await _db.PlaylistTracks
            .Where(pt => playlistIds.Contains(pt.PlaylistId)
                         && pt.TrackId != id
                         && pt.Playlist.UserId == userId)
            .AnyAsync(pt => pt.Track.Name.ToLower() == trimmed.ToLower());

        if (nameTaken)
            return Conflict(new { message = $"A track named \"{trimmed}\" already exists in this playlist." });

        track.Name = trimmed;
        await _db.SaveChangesAsync();

        return Ok(new { id = track.Id, name = track.Name });
    }

    /// <summary>
    /// Returns how many of the current user's playlists reference the given track.
    /// Used by the client to decide whether a local file is still needed.
    /// </summary>
    [HttpGet("{id:int}/usage")]
    public async Task<IActionResult> GetUsage(int id)
    {
        var userId = User.GetUserId();

        var count = await _db.PlaylistTracks
            .Where(pt => pt.TrackId == id && pt.Playlist.UserId == userId)
            .CountAsync();

        return Ok(new TrackUsageResponse { PlaylistCount = count });
    }
}