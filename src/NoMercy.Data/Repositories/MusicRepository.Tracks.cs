// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Music;
using NoMercy.NmSystem.NewtonSoftConverters;

namespace NoMercy.Data.Repositories;

public partial class MusicRepository
{
    #region Track Queries

    public async Task<Track?> GetTrackAsync(Guid id, CancellationToken ct = default)
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Tracks.AsNoTracking()
            .Where(predicate: track => track.Id == id)
            .Include(navigationPropertyPath: track => track.AlbumTrack)
                .ThenInclude(navigationPropertyPath: albumTrack => albumTrack.Album)
                    .ThenInclude(navigationPropertyPath: album => album.AlbumArtist)
                        .ThenInclude(navigationPropertyPath: albumArtist => albumArtist.Artist)
            .Include(navigationPropertyPath: track => track.ArtistTrack)
                .ThenInclude(navigationPropertyPath: artistTrack => artistTrack.Artist)
            .FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<List<TrackUser>> GetTracks(Guid userId, CancellationToken ct = default)
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .TrackUser.AsNoTracking()
            .Where(predicate: tu => tu.UserId == userId)
            .Include(navigationPropertyPath: trackUser => trackUser.Track)
                .ThenInclude(navigationPropertyPath: track => track.AlbumTrack)
                    .ThenInclude(navigationPropertyPath: albumTrack => albumTrack.Album)
            .Include(navigationPropertyPath: trackUser => trackUser.Track)
                .ThenInclude(navigationPropertyPath: track => track.ArtistTrack)
                    .ThenInclude(navigationPropertyPath: artistTrack => artistTrack.Artist)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task LikeTrackAsync(
        Guid userId,
        Track track,
        bool liked,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        if (liked)
        {
            await mediaContext
                .TrackUser.Upsert(entity: new(trackId: track.Id, userId: userId))
                .On(match: m => new { m.TrackId, m.UserId })
                .WhenMatched(updater: m => new() { TrackId = m.TrackId, UserId = m.UserId })
                .RunAsync();
        }
        else
        {
            TrackUser? trackUser = await mediaContext.TrackUser.FirstOrDefaultAsync(
                predicate: tu => tu.TrackId == track.Id && tu.UserId == userId,
                cancellationToken: ct
            );

            if (trackUser is not null)
            {
                mediaContext.TrackUser.Remove(entity: trackUser);
                await mediaContext.SaveChangesAsync(cancellationToken: ct);
            }
        }
    }

    public async Task RecordPlaybackAsync(Guid trackId, Guid userId, CancellationToken ct = default)
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        await mediaContext.MusicPlays.AddAsync(entity: new(userId: userId, trackId: trackId), cancellationToken: ct);
        await mediaContext.SaveChangesAsync(cancellationToken: ct);
    }

    public async Task<Track?> GetTrackWithIncludesAsync(Guid id, CancellationToken ct = default)
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Tracks.AsNoTracking()
            .Where(predicate: track => track.Id == id)
            .Include(navigationPropertyPath: track => track.ArtistTrack)
                .ThenInclude(navigationPropertyPath: artistTrack => artistTrack.Artist)
            .Include(navigationPropertyPath: track => track.AlbumTrack)
                .ThenInclude(navigationPropertyPath: albumTrack => albumTrack.Album)
            .FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<Lyric[]?> UpdateTrackLyricsAsync(
        Track track,
        string lyricsJson,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        await mediaContext
            .Upsert(entity: track)
            .On(match: v => new { v.Id })
            .WhenMatched(updater: v => new() { _lyrics = lyricsJson })
            .RunAsync();

        return lyricsJson.FromJson<Lyric[]>();
    }

    public async Task UpdateTrackLyricsOffsetAsync(
        Track track,
        int? offsetMs,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        await mediaContext
            .Upsert(entity: track)
            .On(match: v => new { v.Id })
            .WhenMatched(updater: v => new() { LyricsOffset = offsetMs })
            .RunAsync();
    }

    #endregion
}
