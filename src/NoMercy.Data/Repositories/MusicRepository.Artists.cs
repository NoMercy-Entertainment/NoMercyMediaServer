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
using NoMercy.Data.Extensions;
using NoMercy.Database;
using NoMercy.Database.Models.Music;

namespace NoMercy.Data.Repositories;

public partial class MusicRepository
{
    #region Artist Queries

    public async Task<Artist?> GetArtistAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        // Explicit include set covering every navigation the DTO touches.
        // Any missing leaf triggers lazy-load per-track and times out artists
        // like Ed Sheeran. `MusicPlays` is deliberately excluded; DTO's
        // `favorite_tracks` is returned empty and a dedicated endpoint will
        // replace it later.
        Artist? artist = await mediaContext
            .Artists.AsNoTracking()
            .AsSplitQuery()
            .Where(predicate: artist => artist.Id == id)
            .ForUser(userId: userId)
            .Include(navigationPropertyPath: artist => artist.Library)
            .Include(navigationPropertyPath: artist => artist.ArtistUser.Where(au => au.UserId == userId))
            .Include(navigationPropertyPath: artist => artist.Translations)
            .Include(navigationPropertyPath: artist => artist.Images)
            .Include(navigationPropertyPath: artist => artist.AlbumArtist)
                .ThenInclude(navigationPropertyPath: albumArtist => albumArtist.Album)
                    .ThenInclude(navigationPropertyPath: album => album.AlbumUser.Where(au => au.UserId == userId))
            .Include(navigationPropertyPath: artist => artist.AlbumArtist)
                .ThenInclude(navigationPropertyPath: albumArtist => albumArtist.Album)
                    .ThenInclude(navigationPropertyPath: album => album.Images)
            .Include(navigationPropertyPath: artist => artist.ArtistTrack)
                .ThenInclude(navigationPropertyPath: at => at.Track)
                    .ThenInclude(navigationPropertyPath: track => track.AlbumTrack)
                        .ThenInclude(navigationPropertyPath: albumTrack => albumTrack.Album)
            .Include(navigationPropertyPath: artist => artist.ArtistTrack)
                .ThenInclude(navigationPropertyPath: at => at.Track)
                    .ThenInclude(navigationPropertyPath: track => track.TrackUser.Where(tu => tu.UserId == userId))
            .Include(navigationPropertyPath: artist => artist.ArtistMusicGenre)
                .ThenInclude(navigationPropertyPath: amg => amg.MusicGenre)
            .FirstOrDefaultAsync(cancellationToken: ct);

        if (artist is null)
            return null;

        // Hydrate Track.ArtistTrack with collaborator artists. The cycle
        // Artist -> ArtistTrack -> Track -> ArtistTrack -> Artist can't be
        // expressed in a single Include under AsNoTracking, so we fetch the
        // collaborator rows in a separate flat query and reattach them.
        List<Guid> trackIds = artist.ArtistTrack.Select(selector: at => at.TrackId).Distinct().ToList();

        if (trackIds.Count <= 0)
            return artist;

        List<ArtistTrack> collabs = await mediaContext
            .ArtistTrack.AsNoTracking()
            .Where(predicate: at => trackIds.Contains(at.TrackId))
            .Include(navigationPropertyPath: at => at.Artist)
            .ToListAsync(cancellationToken: ct);

        Dictionary<Guid, List<ArtistTrack>> byTrackId = collabs
            .GroupBy(keySelector: at => at.TrackId)
            .ToDictionary(keySelector: g => g.Key, elementSelector: g => g.DistinctBy(keySelector: at => at.ArtistId).ToList());

        foreach (ArtistTrack at in artist.ArtistTrack)
        {
            if (byTrackId.TryGetValue(key: at.TrackId, value: out List<ArtistTrack>? list))
            {
                at.Track.ArtistTrack = list;
            }
        }

        return artist;
    }

    public async Task<List<Artist>> GetArtists(
        Guid userId,
        string letter,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Artists.AsNoTracking()
            .ForUser(userId: userId)
            .Where(predicate: artist =>
                (letter == "_" || letter == "#")
                    ? !AlphaLetters.Any(p =>
                        (artist.TitleSort ?? artist.Name).ToLower().StartsWith(p)
                    )
                    : (artist.TitleSort ?? artist.Name).ToLower().StartsWith(letter.ToLower())
            )
            .Include(navigationPropertyPath: artist => artist.ArtistUser.Where(au => au.UserId == userId))
            .Include(navigationPropertyPath: artist => artist.Translations)
            .Include(navigationPropertyPath: artist => artist.Images.Where(image => image.Type == "background"))
            .Include(navigationPropertyPath: artist => artist.ArtistMusicGenre)
                .ThenInclude(navigationPropertyPath: amg => amg.MusicGenre)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task LikeArtistAsync(
        Guid userId,
        Artist artist,
        bool liked,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        if (liked)
        {
            await mediaContext
                .ArtistUser.Upsert(entity: new(artistId: artist.Id, userId: userId))
                .On(match: m => new { m.ArtistId, m.UserId })
                .WhenMatched(updater: m => new() { ArtistId = m.ArtistId, UserId = m.UserId })
                .RunAsync();
        }
        else
        {
            ArtistUser? artistUser = await mediaContext.ArtistUser.FirstOrDefaultAsync(
                predicate: au => au.ArtistId == artist.Id && au.UserId == userId,
                cancellationToken: ct
            );

            if (artistUser is not null)
            {
                mediaContext.ArtistUser.Remove(entity: artistUser);
                await mediaContext.SaveChangesAsync(cancellationToken: ct);
            }
        }
    }

    #endregion

    #region Projection Methods — Artist Cards

    public async Task<List<ArtistCardDto>> GetArtistCardsAsync(
        Guid userId,
        string letter,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        List<ArtistCardDto> cards = await mediaContext
            .Artists.AsNoTracking()
            .ForUser(userId: userId)
            .Where(predicate: artist =>
                (letter == "_" || letter == "#")
                    ? !AlphaLetters.Any(p =>
                        (artist.TitleSort ?? artist.Name).ToLower().StartsWith(p)
                    )
                    : (artist.TitleSort ?? artist.Name).ToLower().StartsWith(letter.ToLower())
            )
            .Where(predicate: artist => artist.ArtistTrack.Any())
            .OrderBy(keySelector: artist => artist.TitleSort ?? artist.Name)
            .ThenBy(keySelector: artist => artist.Id)
            .Select(selector: artist => new ArtistCardDto
            {
                Id = artist.Id,
                Name = artist.Name,
                Cover = artist.Cover,
                Disambiguation = artist.Disambiguation,
                Description = artist.Description,
                ColorPalette = artist._colorPalette ?? string.Empty,
                LibraryId = artist.LibraryId,
                Folder = artist.Folder,
                TrackCount = artist.ArtistTrack.Count(),
                ThumbImagePath = artist
                    .Images.Where(image => image.Type == "thumb")
                    .Select(image => image.FilePath)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken: ct);

        return cards
            .DistinctBy(keySelector: c => c.Id)
            .DistinctBy(keySelector: c => c.Name.Trim().ToLowerInvariant())
            .ToList();
    }

    /// <summary>
    /// Returns every artist in the library as cards, ordered by name. Used by
    /// the TV lolomo layout to build one carousel per first-letter bucket in
    /// a single query instead of 27 round-trips.
    /// </summary>
    public async Task<List<ArtistCardDto>> GetAllArtistCardsAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        List<ArtistCardDto> cards = await mediaContext
            .Artists.AsNoTracking()
            .ForUser(userId: userId)
            .Where(predicate: artist => artist.ArtistTrack.Any())
            .OrderBy(keySelector: artist => artist.TitleSort ?? artist.Name)
            .ThenBy(keySelector: artist => artist.Id)
            .Select(selector: artist => new ArtistCardDto
            {
                Id = artist.Id,
                Name = artist.Name,
                Cover = artist.Cover,
                Disambiguation = artist.Disambiguation,
                Description = artist.Description,
                ColorPalette = artist._colorPalette ?? string.Empty,
                LibraryId = artist.LibraryId,
                Folder = artist.Folder,
                TrackCount = artist.ArtistTrack.Count(),
                ThumbImagePath = artist
                    .Images.Where(image => image.Type == "thumb")
                    .Select(image => image.FilePath)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken: ct);

        return cards
            .DistinctBy(keySelector: c => c.Id)
            .DistinctBy(keySelector: c => c.Name.Trim().ToLowerInvariant())
            .ToList();
    }

    public async Task<List<ArtistCardDto>> GetLatestArtistCardsAsync(
        int take = 36,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Artists.AsNoTracking()
            .Where(predicate: artist => !string.IsNullOrEmpty(artist.Cover) && artist.ArtistTrack.Any())
            .OrderByDescending(keySelector: artist => artist.CreatedAt)
            .ThenBy(keySelector: artist => artist.Id)
            .Select(selector: artist => new ArtistCardDto
            {
                Id = artist.Id,
                Name = artist.Name,
                Cover = artist.Cover,
                Disambiguation = artist.Disambiguation,
                Description = artist.Description,
                ColorPalette = artist._colorPalette ?? string.Empty,
                LibraryId = artist.LibraryId,
                Folder = artist.Folder,
                TrackCount = artist.ArtistTrack.Count(),
                ThumbImagePath = artist
                    .Images.Where(image => image.Type == "thumb")
                    .Select(image => image.FilePath)
                    .FirstOrDefault(),
            })
            .Take(count: take)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<List<ArtistCardDto>> GetFavoriteArtistCardsAsync(
        Guid userId,
        int take = 36,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .ArtistUser.AsNoTracking()
            .Where(predicate: artistUser => artistUser.UserId == userId)
            .OrderBy(keySelector: artistUser => artistUser.Artist.Name)
            .ThenBy(keySelector: artistUser => artistUser.Artist.Id)
            .Select(selector: artistUser => new ArtistCardDto
            {
                Id = artistUser.Artist.Id,
                Name = artistUser.Artist.Name,
                Cover = artistUser.Artist.Cover,
                Disambiguation = artistUser.Artist.Disambiguation,
                Description = artistUser.Artist.Description,
                ColorPalette = artistUser.Artist._colorPalette ?? string.Empty,
                LibraryId = artistUser.Artist.LibraryId,
                Folder = artistUser.Artist.Folder,
                TrackCount = artistUser.Artist.ArtistTrack.Count(),
                ThumbImagePath = artistUser
                    .Artist.Images.Where(image => image.Type == "thumb")
                    .Select(image => image.FilePath)
                    .FirstOrDefault(),
            })
            .Take(count: take)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<List<ArtistCardDto>> GetArtistCardsByIdsAsync(
        List<Guid> artistIds,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Artists.AsNoTracking()
            .Where(predicate: artist => artistIds.Contains(artist.Id))
            .Select(selector: artist => new ArtistCardDto
            {
                Id = artist.Id,
                Name = artist.Name,
                Cover = artist.Cover,
                Disambiguation = artist.Disambiguation,
                Description = artist.Description,
                ColorPalette = artist._colorPalette ?? string.Empty,
                LibraryId = artist.LibraryId,
                Folder = artist.Folder,
                TrackCount = artist.ArtistTrack.Count(),
                ThumbImagePath = artist
                    .Images.Where(image => image.Type == "thumb")
                    .Select(image => image.FilePath)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken: ct);
    }

    #endregion
}
