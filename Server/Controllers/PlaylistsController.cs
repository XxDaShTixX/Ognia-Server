using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ognia.Server.DTOs.Playlist;
using Ognia.Server.DTOs.Track;
using Ognia.Server.Entities;
using Ognia.Server.Extensions;
using Ognia.Server.Models;
using Ognia.Server.Services.TrackSourceService;

namespace Ognia.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PlaylistsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ITrackSourceService _trackSource;
        private enum AddOutcome { Added, Skipped, Invalid }

        public PlaylistsController(
            AppDbContext db, 
            ITrackSourceService trackSource)
        {
            _db = db;
            _trackSource = trackSource;
        }

        /// <summary>
        /// Retrieves all playlists created by the logged in user.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var userId = User.GetUserId();

            var rows = await _db.Playlists
                .Where(p => p.UserId == userId)
                .OrderBy(p => p.CreatedAt)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.UserId,
                    p.CreatedAt,
                    PlaylistTracksCount = p.PlaylistTracks.Count,
                    Tracks = p.PlaylistTracks
                        .OrderBy(pt => pt.Position)
                        .Select(pt => new
                        {
                            pt.Track.ThumbnailUrl,
                            pt.Track.SourceName,
                            pt.Track.SourceId
                        })
                        .ToList()
                })
                .ToListAsync();

            var playlists = rows.Select(p => new PlaylistResponse
            {
                Id = p.Id,
                Name = p.Name,
                UserId = p.UserId,
                CreatedAt = p.CreatedAt,
                PlaylistTracksCount = p.PlaylistTracksCount,
                ThumbnailUrls = p.Tracks.Take(4).Select(t => t.ThumbnailUrl).ToList(),
                CoverThumbnails = p.Tracks.Take(4).Select(t => new CoverThumbnailResponse
                {
                    Url = t.ThumbnailUrl,
                    SourceName = t.SourceName,
                    SourceId = t.SourceId
                }).ToList()
            }).ToList();

            return Ok(playlists);
        }

        /// <summary>
        /// Create a new playlist.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreatePlaylistRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { message = "Name is required." });

            var playlist = new PlaylistModel
            {
                Name = request.Name.Trim(),
                UserId = User.GetUserId()
            };

            _db.Playlists.Add(playlist);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAll), new PlaylistResponse
            {
                Id = playlist.Id,
                Name = playlist.Name,
                UserId = playlist.UserId,
                CreatedAt = playlist.CreatedAt,
                PlaylistTracksCount = 0,
                ThumbnailUrls = new List<string>(),
                CoverThumbnails = new List<CoverThumbnailResponse>()
            });
        }

        /// <summary>
        /// Rename a playlist.
        /// </summary>
        /// <param name="id">PlaylistId</param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Rename(int id, [FromBody] RenamePlaylistRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { message = "Name is required." });

            var userId = User.GetUserId();
            var playlist = await _db.Playlists.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
            if (playlist == null) return NotFound();

            playlist.Name = request.Name.Trim();
            await _db.SaveChangesAsync();

            return Ok(new { id = playlist.Id, name = playlist.Name });
        }

        /// <summary>
        /// Delete a playlist
        /// </summary>
        /// <param name="id">PlaylistId</param>
        /// <returns></returns>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = User.GetUserId();
            var playlist = await _db.Playlists
                .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
            if (playlist == null) return NotFound();

            // Capture the track IDs before the cascade removes the links.
            var trackIds = await _db.PlaylistTracks
                .Where(pt => pt.PlaylistId == id)
                .Select(pt => pt.TrackId)
                .ToListAsync();

            _db.Playlists.Remove(playlist);
            await _db.SaveChangesAsync();

            foreach (var trackId in trackIds)
                await DeleteTrackIfOrphanedAsync(trackId);

            return NoContent();
        }

        /// <summary>
        /// Get a playlist by id.
        /// </summary>
        /// <param name="id">PlaylistId</param>
        /// <returns></returns>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var userId = User.GetUserId();

            var playlist = await _db.Playlists
                .Where(p => p.Id == id && p.UserId == userId)
                .Select(p => new PlaylistDetailResponse
                {
                    Id = p.Id,
                    Name = p.Name,
                    CreatedAt = p.CreatedAt,
                    Tracks = p.PlaylistTracks
                        .OrderBy(pt => pt.Position)
                        .Select(pt => new TrackResponse
                        {
                            Id = pt.Track.Id,
                            SourceName = pt.Track.SourceName,
                            SourceId = pt.Track.SourceId,
                            Name = pt.Track.Name,
                            ThumbnailUrl = pt.Track.ThumbnailUrl,
                            Duration = pt.Track.Duration,
                            Position = pt.Position
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (playlist == null) return NotFound();
            return Ok(playlist);
        }

        /// <summary>
        /// Adds a track to an existing playlist.
        /// </summary>
        /// <param name="id">PlaylistId</param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("{id:int}/tracks")]
        public async Task<IActionResult> AddTrack(int id, [FromBody] AddTrackRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Url))
                return BadRequest(new { message = "URL is required." });

            var userId = User.GetUserId();

            var playlist = await _db.Playlists
                .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
            if (playlist == null) return NotFound();

            // Playlist URL: expand and add each video as its own track.
            // Playlist URL: expand and add each video as its own track.
            if (_trackSource.IsPlaylistUrl(request.Url))
            {
                var expanded = await _trackSource.ExpandPlaylistAsync(request.Url);
                if (expanded == null || expanded.Count == 0)
                    return BadRequest(new { message = "Playlist is empty or could not be read." });

                var plSourceName = _trackSource.SourceType.ToWireName();
                using var sem = new SemaphoreSlim(8);
                var metadataTasks = expanded.Select(async videoUrl =>
                {
                    var sid = _trackSource.ExtractId(videoUrl);
                    if (string.IsNullOrWhiteSpace(sid))
                        return (SourceId: (string?)null, Metadata: (TrackMetadataModel?)null);

                    await sem.WaitAsync();
                    try
                    {
                        var md = await _trackSource.FetchMetadataAsync(sid);
                        return (SourceId: sid, Metadata: (TrackMetadataModel?)md);
                    }
                    catch
                    {
                        return (SourceId: sid, Metadata: (TrackMetadataModel?)null);
                    }
                    finally
                    {
                        sem.Release();
                    }
                }).ToList();

                var fetched = await Task.WhenAll(metadataTasks);

                var plAdded = 0;
                var plSkipped = 0;

                foreach (var (sid, md) in fetched)
                {
                    if (string.IsNullOrWhiteSpace(sid)) { plSkipped++; continue; }

                    var plTrack = await _db.Tracks
                        .FirstOrDefaultAsync(t => t.SourceName == plSourceName && t.SourceId == sid);

                    if (plTrack == null)
                    {
                        if (md == null) { plSkipped++; continue; }

                        plTrack = new TrackModel
                        {
                            SourceName = _trackSource.SourceName,
                            SourceId = sid,
                            Name = md.Name,
                            ThumbnailUrl = md.ThumbnailUrl,
                            Duration = md.Duration
                        };
                        _db.Tracks.Add(plTrack);
                        await _db.SaveChangesAsync();
                    }

                    var plAlreadyLinked = await _db.PlaylistTracks
                        .AnyAsync(pt => pt.PlaylistId == id && pt.TrackId == plTrack.Id);
                    if (plAlreadyLinked) { plSkipped++; continue; }

                    // Deduplicate the display name within this playlist.
                    plTrack.Name = await GetUniqueTrackNameAsync(id, plTrack.Name);

                    var plNextPosition = (await _db.PlaylistTracks
                        .Where(pt => pt.PlaylistId == id)
                        .MaxAsync(pt => (int?)pt.Position) ?? -1) + 1;

                    _db.PlaylistTracks.Add(new PlaylistTrackModel
                    {
                        PlaylistId = id,
                        TrackId = plTrack.Id,
                        Position = plNextPosition
                    });
                    await _db.SaveChangesAsync();
                    plAdded++;
                }

                return Ok(new { added = plAdded, skipped = plSkipped });
            }

            // Single-track path (existing logic continues below)
            var sourceId = _trackSource.ExtractId(request.Url);
            if (string.IsNullOrWhiteSpace(sourceId))
                return BadRequest(new { message = "Could not extract a valid identifier from the URL." });

            var sourceName = _trackSource.SourceType.ToWireName();
            var track = await _db.Tracks
                .FirstOrDefaultAsync(t => t.SourceName == sourceName && t.SourceId == sourceId);

            if (track == null)
            {
                TrackMetadataModel metadata;
                try
                {
                    metadata = await _trackSource.FetchMetadataAsync(sourceId);
                }
                catch (Exception ex)
                {
                    return BadRequest(new { message = $"Could not fetch track metadata: {ex.Message}" });
                }

                track = new TrackModel
                {
                    SourceName = _trackSource.SourceName,
                    SourceId = sourceId,
                    Name = metadata.Name,
                    ThumbnailUrl = metadata.ThumbnailUrl,
                    Duration = metadata.Duration
                };
                _db.Tracks.Add(track);
                await _db.SaveChangesAsync();
            }

            var alreadyLinked = await _db.PlaylistTracks
                .AnyAsync(pt => pt.PlaylistId == id && pt.TrackId == track.Id);
            if (alreadyLinked)
                return Conflict(new { message = "Track already in this playlist." });

            // Make the track's display name unique within this playlist by appending " (2)", " (3)", …
            track.Name = await GetUniqueTrackNameAsync(id, track.Name);

            var nextPosition = (await _db.PlaylistTracks
                .Where(pt => pt.PlaylistId == id)
                .MaxAsync(pt => (int?)pt.Position) ?? -1) + 1;

            _db.PlaylistTracks.Add(new PlaylistTrackModel
            {
                PlaylistId = id,
                TrackId = track.Id,
                Position = nextPosition
            });
            await _db.SaveChangesAsync();

            return Ok(new TrackResponse
            {
                Id = track.Id,
                SourceName = track.SourceName,
                SourceId = track.SourceId,
                Name = track.Name,
                ThumbnailUrl = track.ThumbnailUrl,
                Duration = track.Duration,
                Position = nextPosition
            });
        }

        /// <summary>
        /// Adds many track URLs to a playlist in one transaction, preserving request order.
        /// Every input URL lands in exactly one of Added / Skipped / Invalid.
        /// </summary>
        [HttpPost("{id:int}/tracks/bulk")]
        public async Task<IActionResult> AddTracksBulk(int id, [FromBody] BulkAddTracksRequest request)
        {
            if (request?.Urls == null || request.Urls.Count == 0)
                return BadRequest(new { message = "Urls is required and must not be empty." });

            var userId = User.GetUserId();

            var playlistExists = await _db.Playlists
                .AnyAsync(p => p.Id == id && p.UserId == userId);
            if (!playlistExists) return NotFound();

            var response = new BulkAddTracksResponse();

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var nextPosition = (await _db.PlaylistTracks
                    .Where(pt => pt.PlaylistId == id)
                    .MaxAsync(pt => (int?)pt.Position) ?? -1) + 1;

                foreach (var item in request.Urls)
                {
                    var (outcome, newPos) = await TryAddOneAsync(id, item, nextPosition);
                    nextPosition = newPos;
                    switch (outcome)
                    {
                        case AddOutcome.Added: response.Added.Add(item.Url); break;
                        case AddOutcome.Skipped: response.Skipped.Add(item.Url); break;
                        default: response.Invalid.Add(item.Url); break;
                    }
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }

            return Ok(response);
        }

        /// <summary>
        /// Adds a single item to the playlist using the same rules as the single-add endpoint.
        /// If item.Name + item.Duration + item.ThumbnailUrl are supplied, no metadata fetch
        /// occurs for the item itself (playlist expansions still fetch per video).
        /// Returns the outcome plus the next free position.
        /// </summary>
        private async Task<(AddOutcome Outcome, int NextPosition)> TryAddOneAsync(
            int playlistId, BulkAddTrackItem item, int nextPosition)
        {
            var url = item.Url;
            if (string.IsNullOrWhiteSpace(url)) return (AddOutcome.Invalid, nextPosition);
            if (!_trackSource.CanHandle(url)) return (AddOutcome.Invalid, nextPosition);

            // Playlist URL path — expansion is server-side and per-video metadata is not
            // supplied by the client (client can only pre-fill single-track entries).
            if (_trackSource.IsPlaylistUrl(url))
            {
                var expanded = await _trackSource.ExpandPlaylistAsync(url);
                if (expanded == null || expanded.Count == 0) return (AddOutcome.Invalid, nextPosition);

                var anyAdded = false;
                var pos = nextPosition;
                foreach (var videoUrl in expanded)
                {
                    var sid = _trackSource.ExtractId(videoUrl);
                    if (string.IsNullOrWhiteSpace(sid)) continue;

                    var (outcome, newPos) = await TryUpsertAndLinkAsync(
                        playlistId, sid, pos,
                        suppliedName: null, suppliedDuration: null, suppliedThumbnail: null);
                    pos = newPos;
                    if (outcome == AddOutcome.Added) anyAdded = true;
                }

                return (anyAdded ? AddOutcome.Added : AddOutcome.Skipped, pos);
            }

            // Single-track path
            var sourceId = _trackSource.ExtractId(url);
            if (string.IsNullOrWhiteSpace(sourceId)) return (AddOutcome.Invalid, nextPosition);

            return await TryUpsertAndLinkAsync(
                playlistId, sourceId, nextPosition,
                suppliedName: item.Name,
                suppliedDuration: item.Duration,
                suppliedThumbnail: item.ThumbnailUrl);
        }

        /// <summary>
        /// Core per-video-id logic. Uses supplied metadata when all of name+duration+thumbnail
        /// are present; otherwise fetches from the source. Does not call SaveChanges
        /// (except when a brand-new TrackModel needs an Id before linking).
        /// </summary>
        private async Task<(AddOutcome Outcome, int NextPosition)> TryUpsertAndLinkAsync(
            int playlistId, string sourceId, int nextPosition,
            string? suppliedName, TimeSpan? suppliedDuration, string? suppliedThumbnail)
        {
            var sourceName = _trackSource.SourceType.ToWireName();
            var track = await _db.Tracks
                .FirstOrDefaultAsync(t => t.SourceName == sourceName && t.SourceId == sourceId);

            if (track == null)
            {
                string name;
                string thumbnail;
                TimeSpan duration;

                var haveAll = !string.IsNullOrWhiteSpace(suppliedName)
                              && suppliedDuration.HasValue
                              && suppliedThumbnail != null;

                if (haveAll)
                {
                    name = suppliedName!.Trim();
                    duration = suppliedDuration!.Value;
                    thumbnail = suppliedThumbnail!;
                }
                else
                {
                    TrackMetadataModel metadata;
                    try
                    {
                        metadata = await _trackSource.FetchMetadataAsync(sourceId);
                    }
                    catch
                    {
                        return (AddOutcome.Invalid, nextPosition);
                    }
                    name = metadata.Name;
                    duration = metadata.Duration;
                    thumbnail = metadata.ThumbnailUrl;
                }

                track = new TrackModel
                {
                    SourceName = _trackSource.SourceName,
                    SourceId = sourceId,
                    Name = name,
                    ThumbnailUrl = thumbnail,
                    Duration = duration
                };
                _db.Tracks.Add(track);
                await _db.SaveChangesAsync();
            }

            var alreadyLinked = await _db.PlaylistTracks
                .AnyAsync(pt => pt.PlaylistId == playlistId && pt.TrackId == track.Id);
            if (alreadyLinked) return (AddOutcome.Skipped, nextPosition);

            track.Name = await GetUniqueTrackNameAsync(playlistId, track.Name);

            _db.PlaylistTracks.Add(new PlaylistTrackModel
            {
                PlaylistId = playlistId,
                TrackId = track.Id,
                Position = nextPosition
            });

            return (AddOutcome.Added, nextPosition + 1);
        }

        /// <summary>
        /// Remove track from playlist.
        /// </summary>
        /// <param name="playlistId"></param>
        /// <param name="trackId"></param>
        /// <returns></returns>
        [HttpDelete("{playlistId:int}/tracks/{trackId:int}")]
        public async Task<IActionResult> RemoveTrack(int playlistId, int trackId)
        {
            var userId = User.GetUserId();

            var playlistExists = await _db.Playlists
                .AnyAsync(p => p.Id == playlistId && p.UserId == userId);
            if (!playlistExists) return NotFound();

            var link = await _db.PlaylistTracks
                .FirstOrDefaultAsync(pt => pt.PlaylistId == playlistId && pt.TrackId == trackId);
            if (link == null) return NotFound();

            _db.PlaylistTracks.Remove(link);
            await _db.SaveChangesAsync();

            await DeleteTrackIfOrphanedAsync(trackId);

            return NoContent();
        }

        /// <summary>
        /// Reorders tracks in a playlist. Body must contain every track currently
        /// in the playlist, in the new order.
        /// </summary>
        [HttpPut("{id:int}/tracks/order")]
        public async Task<IActionResult> ReorderTracks(int id, [FromBody] ReorderTracksRequest request)
        {
            var userId = User.GetUserId();

            var playlistExists = await _db.Playlists
                .AnyAsync(p => p.Id == id && p.UserId == userId);
            if (!playlistExists) return NotFound();

            var links = await _db.PlaylistTracks
                .Where(pt => pt.PlaylistId == id)
                .ToListAsync();

            if (links.Count != request.TrackIds.Count)
                return BadRequest(new { message = "Track list does not match playlist contents." });

            var byTrackId = links.ToDictionary(l => l.TrackId);
            for (var i = 0; i < request.TrackIds.Count; i++)
            {
                if (!byTrackId.TryGetValue(request.TrackIds[i], out var link))
                    return BadRequest(new { message = $"Track {request.TrackIds[i]} is not in this playlist." });
                link.Position = i;
            }

            await _db.SaveChangesAsync();
            return NoContent();
        }

        /// <summary>
        /// Deletes the track row if no PlaylistTrack references it.
        /// </summary>
        private async Task DeleteTrackIfOrphanedAsync(int trackId)
        {
            var stillReferenced = await _db.PlaylistTracks.AnyAsync(pt => pt.TrackId == trackId);
            if (stillReferenced) return;

            var track = await _db.Tracks.FindAsync(trackId);
            if (track != null)
            {
                _db.Tracks.Remove(track);
                await _db.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Returns a display name that does not collide (case-insensitively) with any other
        /// track already in the given playlist. Appends " (2)", " (3)", … when needed.
        /// </summary>
        private async Task<string> GetUniqueTrackNameAsync(int playlistId, string baseName)
        {
            var existing = await _db.PlaylistTracks
                .Where(pt => pt.PlaylistId == playlistId)
                .Select(pt => pt.Track.Name)
                .ToListAsync();

            var used = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
            if (!used.Contains(baseName)) return baseName;

            for (var n = 2; n < int.MaxValue; n++)
            {
                var candidate = $"{baseName} ({n})";
                if (!used.Contains(candidate)) return candidate;
            }

            // Unreachable in practice.
            return $"{baseName} ({Guid.NewGuid():N})";
        }

        /// <summary>
        /// Adds a single track URL to the given playlist. Returns true if added,
        /// false if the URL is unsupported, unparseable, or already present.
        /// Does not validate playlist ownership — callers must do that first.
        /// </summary>
        private async Task<bool> TryAddSingleTrackAsync(int playlistId, string url)
        {
            if (!_trackSource.CanHandle(url)) return false;

            var sourceId = _trackSource.ExtractId(url);
            if (string.IsNullOrWhiteSpace(sourceId)) return false;

            var sourceName = _trackSource.SourceType.ToWireName();
            var track = await _db.Tracks
                .FirstOrDefaultAsync(t => t.SourceName == sourceName && t.SourceId == sourceId);

            if (track == null)
            {
                TrackMetadataModel metadata;
                try
                {
                    metadata = await _trackSource.FetchMetadataAsync(sourceId);
                }
                catch (Exception)
                {
                    // Unavailable, private, region-locked, or deleted video — skip it.
                    return false;
                }

                track = new TrackModel
                {
                    SourceName = _trackSource.SourceName,
                    SourceId = sourceId,
                    Name = metadata.Name,
                    ThumbnailUrl = metadata.ThumbnailUrl,
                    Duration = metadata.Duration
                };
                _db.Tracks.Add(track);
                await _db.SaveChangesAsync();
            }

            var alreadyLinked = await _db.PlaylistTracks
                .AnyAsync(pt => pt.PlaylistId == playlistId && pt.TrackId == track.Id);
            if (alreadyLinked) return false;

            var nextPosition = (await _db.PlaylistTracks
                .Where(pt => pt.PlaylistId == playlistId)
                .MaxAsync(pt => (int?)pt.Position) ?? -1) + 1;

            _db.PlaylistTracks.Add(new PlaylistTrackModel
            {
                PlaylistId = playlistId,
                TrackId = track.Id,
                Position = nextPosition
            });
            await _db.SaveChangesAsync();
            return true;
        }
    }
}
