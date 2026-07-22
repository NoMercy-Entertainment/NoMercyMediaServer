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
using NoMercy.Data.DTOs;
using NoMercy.Database;
using NoMercy.Database.Models.Music;

namespace NoMercy.Data.Repositories;

public partial class MusicRepository
{
    #region Playlist Queries

    public async Task<List<CarouselResponseItemDto>> GetCarouselPlaylistsAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Playlists.AsNoTracking()
            .Where(predicate: playlist => playlist.UserId == userId)
            .Include(navigationPropertyPath: playlist => playlist.Tracks)
                .ThenInclude(navigationPropertyPath: trackUser => trackUser.Track)
            .OrderBy(keySelector: playlist => playlist.Name)
            .ThenBy(keySelector: playlist => playlist.Id)
            .Select(selector: playlist => new CarouselResponseItemDto(playlist))
            .Take(count: 36)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<Playlist?> GetPlaylistAsync(
        Guid userId,
        Guid id,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Playlists.AsNoTracking()
            .Where(predicate: playlist => playlist.Id == id)
            .Where(predicate: playlist => playlist.UserId == userId)
            .Include(navigationPropertyPath: playlist => playlist.Tracks)
                .ThenInclude(navigationPropertyPath: trackUser => trackUser.Track)
                    .ThenInclude(navigationPropertyPath: track => track.AlbumTrack)
                        .ThenInclude(navigationPropertyPath: albumTrack => albumTrack.Album)
            .Include(navigationPropertyPath: playlist => playlist.Tracks)
                .ThenInclude(navigationPropertyPath: trackUser => trackUser.Track)
                    .ThenInclude(navigationPropertyPath: track => track.ArtistTrack)
                        .ThenInclude(navigationPropertyPath: artistTrack => artistTrack.Artist)
            .FirstOrDefaultAsync(cancellationToken: ct);
    }

    #endregion

    #region Playlist Management

    // Rooted directly at PlaylistTrack (never through Playlist.Tracks) — an
    // AsNoTracking() Include that leaves this root type and comes back to it via a
    // collection navigation (e.g. Playlist.Tracks, the inverse of this same FK) is
    // a genuine cycle to EF Core's no-tracking validator and throws unconditionally:
    // "The Include path 'Playlist->Tracks' results in a cycle." This shape never
    // revisits PlaylistTrack, so it can't recreate that cycle.
    public async Task<List<PlaylistTrack>> GetPlaylistTracksAsync(
        Guid userId,
        Guid playlistId,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .PlaylistTrack.AsNoTracking()
            .Where(predicate: pt => pt.PlaylistId == playlistId && pt.Playlist.UserId == userId)
            .Include(navigationPropertyPath: pt => pt.Track)
                .ThenInclude(navigationPropertyPath: track => track.AlbumTrack)
                    .ThenInclude(navigationPropertyPath: albumTrack => albumTrack.Album)
                        .ThenInclude(navigationPropertyPath: album => album.AlbumArtist)
                            .ThenInclude(navigationPropertyPath: albumArtist => albumArtist.Artist)
                                .ThenInclude(navigationPropertyPath: artist => artist.Images)
            .Include(navigationPropertyPath: pt => pt.Track)
                .ThenInclude(navigationPropertyPath: track => track.ArtistTrack)
                    .ThenInclude(navigationPropertyPath: artistTrack => artistTrack.Artist)
            .Include(navigationPropertyPath: pt => pt.Track)
                .ThenInclude(navigationPropertyPath: track => track.TrackUser)
            .ToListAsync(cancellationToken: ct);
    }

    // Three flat queries, never one rooted through Album.AlbumTrack (that revisits this same
    // entity type — the identical no-tracking cycle shape as the playlist/artist bugs above;
    // this call site ran tracked, dodging the crash, but still forced the same split-query
    // correlation to re-derive the join per branch under SQLite).
    //
    // Step 1 roots at AlbumTrack directly (WHERE AlbumId = @albumId, indexed) — that alone
    // already yields every track on the album, so there is no need to detour through
    // Album.AlbumTrack at all. Track.ArtistTrack is a different entity type than the root
    // and can be included directly without recreating the cycle.
    //
    // Step 2 roots at Track (not AlbumTrack) to fetch which OTHER albums each track also
    // belongs to: Track.AlbumTrack is the same entity type as step 1's root, and chaining it
    // from an AlbumTrack root would revisit that type — the identical no-tracking cycle
    // shape as the playlist bug, just one hop deeper. Rooting at Track instead avoids it.
    // Deliberately stops at the join row — it does NOT chase ThenInclude(Album).Images/
    // Translations here, because every track on THIS album shares the same handful of
    // albums; doing so joined Images/Translations once PER TRACK ROW instead of once per
    // distinct album, which measured 15-19 real seconds (nearly all of it kernel/IO time,
    // not CPU — confirmed via a raw sqlite3 execution of the exact generated SQL, reproduced
    // consistently on a second run, ruling out a cold-cache/cold-JIT explanation) against
    // the real dev DB for a 38-track album with a shared Translations table carrying
    // 500k+ rows across every entity type.
    //
    // Step 3 roots at Album directly (WHERE Id IN distinctAlbumIds — typically just one row)
    // to fetch Images/Translations exactly once per distinct album, however many tracks
    // reference it, then attaches the result onto each AlbumTrack.Album in memory.
    public async Task<List<AlbumTrack>> GetAlbumTracksAsync(
        Guid userId,
        Guid albumId,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        List<AlbumTrack> albumTracks = await mediaContext
            .AlbumTrack.AsNoTracking()
            .Where(predicate: at => at.AlbumId == albumId)
            .Include(navigationPropertyPath: at => at.Track)
                .ThenInclude(navigationPropertyPath: track => track.ArtistTrack)
                    .ThenInclude(navigationPropertyPath: artistTrack => artistTrack.Artist)
            .Include(navigationPropertyPath: at => at.Track)
                .ThenInclude(navigationPropertyPath: track => track.TrackUser)
            .ToListAsync(cancellationToken: ct);

        if (albumTracks.Count == 0)
            return albumTracks;

        List<Guid> trackIds = albumTracks.Select(selector: at => at.TrackId).Distinct().ToList();
        List<Track> tracksWithAlbumLinks = await mediaContext
            .Tracks.AsNoTracking()
            .Where(predicate: track => trackIds.Contains(track.Id))
            .Include(navigationPropertyPath: track => track.AlbumTrack)
            .ToListAsync(cancellationToken: ct);

        List<Guid> distinctAlbumIds = tracksWithAlbumLinks
            .SelectMany(selector: track => track.AlbumTrack)
            .Select(selector: at => at.AlbumId)
            .Distinct()
            .ToList();

        List<Album> albumsWithDetails = await mediaContext
            .Albums.AsNoTracking()
            .Where(predicate: album => distinctAlbumIds.Contains(album.Id))
            .Include(navigationPropertyPath: album => album.Images)
            .Include(navigationPropertyPath: album => album.Translations)
            .ToListAsync(cancellationToken: ct);

        Dictionary<Guid, Album> albumById = albumsWithDetails.ToDictionary(keySelector: album => album.Id);

        foreach (Track track in tracksWithAlbumLinks)
        foreach (AlbumTrack albumTrack in track.AlbumTrack)
            if (albumById.TryGetValue(key: albumTrack.AlbumId, value: out Album? album))
                albumTrack.Album = album;

        Dictionary<Guid, ICollection<AlbumTrack>> albumsByTrackId =
            tracksWithAlbumLinks.ToDictionary(keySelector: track => track.Id, elementSelector: track => track.AlbumTrack);

        foreach (AlbumTrack albumTrack in albumTracks)
            if (
                albumsByTrackId.TryGetValue(key: albumTrack.TrackId, value: out ICollection<AlbumTrack>? albums)
            )
                albumTrack.Track.AlbumTrack = albums;

        return albumTracks;
    }

    // Three flat queries, never one rooted through Artist.ArtistTrack (that combines three
    // chained one-to-many collections — ArtistTrack, AlbumTrack, Translations — behind a
    // single-row filter, which forces SQLite's split-query correlation to re-derive the
    // same 5-table join per branch; that shape measured 80-100s for a 153-track artist).
    //
    // Step 1 roots at ArtistTrack directly (WHERE ArtistId = @artistId, indexed), so every
    // branch off it is one collection hop from an already-flat, already-filtered result.
    // Deliberately stops at the AlbumTrack join row — it does NOT chase
    // ThenInclude(Album).Translations here, for the identical reason as the album fix:
    // tracks on one artist very often share the same handful of albums, and chasing
    // Album.Translations per track re-derives it once per track instead of once per
    // distinct album. This redundancy is what actually measured 80-100s live for a
    // 153-track artist — worse than the album case because an artist's tracks span many
    // albums, so this join re-derives EVERY one of them repeatedly, not just one.
    //
    // Step 2 roots at Track (not ArtistTrack) to fetch the full artist-credit list per
    // track: Track.ArtistTrack is the same entity type as step 1's root, and chaining it
    // from an ArtistTrack root would revisit that type — the identical no-tracking cycle
    // shape as the playlist bug above, just one hop deeper. Rooting at Track instead avoids
    // it, and the two result sets are attached in memory.
    //
    // Step 3 roots at Album directly (WHERE Id IN distinctAlbumIds) to fetch Translations
    // exactly once per distinct album, however many of this artist's tracks reference it,
    // then attaches the result onto each AlbumTrack.Album in memory — mirrors
    // GetAlbumTracksAsync's step 3 exactly.
    public async Task<List<ArtistTrack>> GetArtistTracksAsync(
        Guid userId,
        Guid artistId,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        List<ArtistTrack> artistTracks = await mediaContext
            .ArtistTrack.AsNoTracking()
            .Where(predicate: at => at.ArtistId == artistId)
            .Include(navigationPropertyPath: at => at.Artist)
                .ThenInclude(navigationPropertyPath: artist => artist.Images)
            .Include(navigationPropertyPath: at => at.Track)
                .ThenInclude(navigationPropertyPath: track => track.AlbumTrack)
            .Include(navigationPropertyPath: at => at.Track)
                .ThenInclude(navigationPropertyPath: track => track.TrackUser)
            .ToListAsync(cancellationToken: ct);

        if (artistTracks.Count == 0)
            return artistTracks;

        List<Guid> trackIds = artistTracks.Select(selector: at => at.TrackId).Distinct().ToList();
        List<Track> tracksWithCredits = await mediaContext
            .Tracks.AsNoTracking()
            .Where(predicate: track => trackIds.Contains(track.Id))
            .Include(navigationPropertyPath: track => track.ArtistTrack)
                .ThenInclude(navigationPropertyPath: artistTrack => artistTrack.Artist)
            .ToListAsync(cancellationToken: ct);

        Dictionary<Guid, ICollection<ArtistTrack>> creditsByTrackId =
            tracksWithCredits.ToDictionary(keySelector: track => track.Id, elementSelector: track => track.ArtistTrack);

        foreach (ArtistTrack artistTrack in artistTracks)
            if (
                creditsByTrackId.TryGetValue(
                    key: artistTrack.TrackId,
                    value: out ICollection<ArtistTrack>? credits
                )
            )
                artistTrack.Track.ArtistTrack = credits;

        List<Guid> distinctAlbumIds = artistTracks
            .SelectMany(selector: at => at.Track.AlbumTrack)
            .Select(selector: albumTrack => albumTrack.AlbumId)
            .Distinct()
            .ToList();

        List<Album> albumsWithTranslations = await mediaContext
            .Albums.AsNoTracking()
            .Where(predicate: album => distinctAlbumIds.Contains(album.Id))
            .Include(navigationPropertyPath: album => album.Translations)
            .ToListAsync(cancellationToken: ct);

        Dictionary<Guid, Album> albumById = albumsWithTranslations.ToDictionary(keySelector: album => album.Id);

        foreach (ArtistTrack artistTrack in artistTracks)
        foreach (AlbumTrack albumTrack in artistTrack.Track.AlbumTrack)
            if (albumById.TryGetValue(key: albumTrack.AlbumId, value: out Album? album))
                albumTrack.Album = album;

        return artistTracks;
    }

    // Two flat queries, never one rooted through Genre.MusicGenreTracks (that revisits this
    // same entity type — the identical no-tracking cycle shape as the playlist/artist/album
    // bugs above; this call site ran tracked, dodging the crash, but still forced the same
    // split-query correlation to re-derive the join three times over, once per branch).
    //
    // Step 1 roots directly at MusicGenreTrack (WHERE GenreId = @genreId, indexed) — already
    // yields every track in the genre, so there is no need to detour through
    // Genre.MusicGenreTracks at all. ArtistTrack.Artist and TrackUser hang off Track (a
    // different entity type than the root) and can be included directly without recreating
    // the cycle — unlike the playlist/artist/album fixes, revisiting MusicGenreTrack was
    // never the issue here, so no second query is needed just to dodge that. Deliberately
    // stops at the AlbumTrack join row for the SAME reason as the album/artist fixes: many
    // tracks in a genre share the same handful of albums, and chasing
    // Album.AlbumArtist.Artist.Images / Album.Translations per track re-derives them once
    // per track instead of once per distinct album.
    //
    // Step 2 roots at Album directly (WHERE Id IN distinctAlbumIds) to fetch
    // AlbumArtist.Artist.Images and Translations exactly once per distinct album, however
    // many tracks in the genre reference it, then attaches the result onto each
    // AlbumTrack.Album in memory — mirrors GetAlbumTracksAsync's step 3 exactly.
    public async Task<List<MusicGenreTrack>> GetGenreTracksAsync(
        Guid userId,
        Guid genreId,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        List<MusicGenreTrack> genreTracks = await mediaContext
            .MusicGenreTrack.AsNoTracking()
            .Where(predicate: mgt =>
                mgt.Genre.AlbumMusicGenres.Any(g =>
                    g.Album.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
                || mgt.Genre.ArtistMusicGenres.Any(g =>
                    g.Artist.Library.LibraryUsers.Any(u => u.UserId == userId)
                )
            )
            .Where(predicate: mgt => mgt.GenreId == genreId)
            .Include(navigationPropertyPath: mgt => mgt.Track)
                .ThenInclude(navigationPropertyPath: track => track.AlbumTrack)
            .Include(navigationPropertyPath: mgt => mgt.Track)
                .ThenInclude(navigationPropertyPath: track => track.ArtistTrack)
                    .ThenInclude(navigationPropertyPath: artistTrack => artistTrack.Artist)
            .Include(navigationPropertyPath: mgt => mgt.Track)
                .ThenInclude(navigationPropertyPath: track => track.TrackUser)
            .ToListAsync(cancellationToken: ct);

        if (genreTracks.Count == 0)
            return genreTracks;

        List<Guid> distinctAlbumIds = genreTracks
            .SelectMany(selector: mgt => mgt.Track.AlbumTrack)
            .Select(selector: albumTrack => albumTrack.AlbumId)
            .Distinct()
            .ToList();

        List<Album> albumsWithDetails = await mediaContext
            .Albums.AsNoTracking()
            .Where(predicate: album => distinctAlbumIds.Contains(album.Id))
            .Include(navigationPropertyPath: album => album.AlbumArtist)
                .ThenInclude(navigationPropertyPath: albumArtist => albumArtist.Artist)
                    .ThenInclude(navigationPropertyPath: artist => artist.Images)
            .Include(navigationPropertyPath: album => album.Translations)
            .ToListAsync(cancellationToken: ct);

        Dictionary<Guid, Album> albumById = albumsWithDetails.ToDictionary(keySelector: album => album.Id);

        foreach (MusicGenreTrack genreTrack in genreTracks)
        foreach (AlbumTrack albumTrack in genreTrack.Track.AlbumTrack)
            if (albumById.TryGetValue(key: albumTrack.AlbumId, value: out Album? album))
                albumTrack.Album = album;

        return genreTracks;
    }

    #endregion

    #region Projection Methods — Playlist Cards

    public async Task<List<PlaylistCardDto>> GetPlaylistCardsAsync(
        Guid userId,
        int take = 36,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Playlists.AsNoTracking()
            .Where(predicate: playlist => playlist.UserId == userId)
            .OrderBy(keySelector: playlist => playlist.Name)
            .ThenBy(keySelector: playlist => playlist.Id)
            .Select(selector: playlist => new PlaylistCardDto
            {
                Id = playlist.Id,
                Name = playlist.Name,
                Cover = playlist.Cover,
                Description = playlist.Description,
                ColorPalette = playlist._colorPalette ?? string.Empty,
                TrackCount = playlist.Tracks.Count(),
            })
            .Take(count: take)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<List<PlaylistCardDto>> GetPlaylistCardsByIdsAsync(
        List<Guid> playlistIds,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Playlists.AsNoTracking()
            .Where(predicate: playlist => playlistIds.Contains(playlist.Id))
            .Select(selector: playlist => new PlaylistCardDto
            {
                Id = playlist.Id,
                Name = playlist.Name,
                Cover = playlist.Cover,
                Description = playlist.Description,
                ColorPalette = playlist._colorPalette ?? string.Empty,
                TrackCount = playlist.Tracks.Count(),
            })
            .ToListAsync(cancellationToken: ct);
    }

    #endregion
}
