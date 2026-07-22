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
    #region Album Queries

    public async Task<Album?> GetAlbumAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        // Minimal include set — drops per-track TrackUser join and the
        // track -> artist -> translations fan-out which caused the same
        // timeout pattern as GetArtistAsync for large albums.
        return await mediaContext
            .Albums.AsNoTracking()
            .AsSplitQuery()
            .Where(predicate: album => album.Id == id)
            .ForUser(userId: userId)
            .Include(navigationPropertyPath: album => album.Library)
            .Include(navigationPropertyPath: album => album.AlbumUser.Where(au => au.UserId == userId))
            .Include(navigationPropertyPath: album => album.AlbumTrack)
                .ThenInclude(navigationPropertyPath: albumTrack => albumTrack.Track)
                    .ThenInclude(navigationPropertyPath: track => track.ArtistTrack)
                        .ThenInclude(navigationPropertyPath: artistTrack => artistTrack.Artist)
            .Include(navigationPropertyPath: album => album.AlbumArtist)
                .ThenInclude(navigationPropertyPath: albumArtist => albumArtist.Artist)
            .Include(navigationPropertyPath: album => album.Images)
            .Include(navigationPropertyPath: album => album.Translations)
            .Include(navigationPropertyPath: album => album.AlbumMusicGenre)
                .ThenInclude(navigationPropertyPath: amg => amg.MusicGenre)
            .FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<List<Album>> GetAlbums(
        Guid userId,
        string letter,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Albums.AsNoTracking()
            .ForUser(userId: userId)
            .Where(predicate: album =>
                (letter == "_" || letter == "#")
                    ? !AlphaLetters.Any(p =>
                        (album.TitleSort ?? album.Name).ToLower().StartsWith(p)
                    )
                    : (album.TitleSort ?? album.Name).ToLower().StartsWith(letter.ToLower())
            )
            .Include(navigationPropertyPath: album => album.AlbumUser.Where(au => au.UserId == userId))
            .Include(navigationPropertyPath: album => album.Translations)
            .Include(navigationPropertyPath: album => album.Images.Where(image => image.Type == "background"))
            .Include(navigationPropertyPath: album => album.AlbumMusicGenre)
                .ThenInclude(navigationPropertyPath: amg => amg.MusicGenre)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task LikeAlbumAsync(
        Guid userId,
        Album album,
        bool liked,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        if (liked)
        {
            await mediaContext
                .AlbumUser.Upsert(entity: new(albumId: album.Id, userId: userId))
                .On(match: m => new { m.AlbumId, m.UserId })
                .WhenMatched(updater: m => new() { AlbumId = m.AlbumId, UserId = m.UserId })
                .RunAsync();
        }
        else
        {
            AlbumUser? albumUser = await mediaContext.AlbumUser.FirstOrDefaultAsync(
                predicate: au => au.AlbumId == album.Id && au.UserId == userId,
                cancellationToken: ct
            );

            if (albumUser is not null)
            {
                mediaContext.AlbumUser.Remove(entity: albumUser);
                await mediaContext.SaveChangesAsync(cancellationToken: ct);
            }
        }
    }

    public async Task<List<AlbumTrack>> GetAlbumTracksForIdsAsync(
        List<Guid> albumIds,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .AlbumTrack.AsNoTracking()
            .Where(predicate: at => albumIds.Contains(at.AlbumId))
            .Include(navigationPropertyPath: at => at.Track)
            .ToListAsync(cancellationToken: ct);
    }

    #endregion

    #region Projection Methods — Album Cards

    public async Task<List<AlbumCardDto>> GetAlbumCardsAsync(
        Guid userId,
        string letter,
        string language,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Albums.AsNoTracking()
            .ForUser(userId: userId)
            .Where(predicate: album =>
                (letter == "_" || letter == "#")
                    ? !AlphaLetters.Any(p =>
                        (album.TitleSort ?? album.Name).ToLower().StartsWith(p)
                    )
                    : (album.TitleSort ?? album.Name).ToLower().StartsWith(letter.ToLower())
            )
            .Where(predicate: album => album.AlbumTrack.Any(at => at.Track.Duration != null))
            .OrderBy(keySelector: album => album.TitleSort ?? album.Name)
            .ThenBy(keySelector: album => album.Id)
            .Select(selector: album => new AlbumCardDto
            {
                Id = album.Id,
                Name = album.Name,
                Cover = album.Cover,
                Disambiguation = album.Disambiguation,
                Description = album.Description,
                ColorPalette = album._colorPalette ?? string.Empty,
                LibraryId = album.LibraryId,
                Folder = album.Folder,
                Year = album.Year,
                TrackCount = album.AlbumTrack.Count(at => at.Track.Duration != null),
                TranslatedDescription = album
                    .Translations.Where(t => t.Iso31661 == language)
                    .Select(t => t.Description)
                    .FirstOrDefault(),
                BackgroundImagePath = album
                    .Images.Where(image => image.Type == "background")
                    .Select(image => image.FilePath)
                    .FirstOrDefault(),
                BackgroundImageColorPalette = album
                    .Images.Where(image => image.Type == "background")
                    .Select(image => image._colorPalette)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken: ct);
    }

    /// <summary>
    /// Returns every album in the library as cards, ordered by name. Used by
    /// the TV lolomo layout to build one carousel per first-letter bucket.
    /// </summary>
    public async Task<List<AlbumCardDto>> GetAllAlbumCardsAsync(
        Guid userId,
        string language,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Albums.AsNoTracking()
            .ForUser(userId: userId)
            .Where(predicate: album => album.AlbumTrack.Any(at => at.Track.Duration != null))
            .OrderBy(keySelector: album => album.TitleSort ?? album.Name)
            .ThenBy(keySelector: album => album.Id)
            .Select(selector: album => new AlbumCardDto
            {
                Id = album.Id,
                Name = album.Name,
                Cover = album.Cover,
                Disambiguation = album.Disambiguation,
                Description = album.Description,
                ColorPalette = album._colorPalette ?? string.Empty,
                LibraryId = album.LibraryId,
                Folder = album.Folder,
                Year = album.Year,
                TrackCount = album.AlbumTrack.Count(at => at.Track.Duration != null),
                TranslatedDescription = album
                    .Translations.Where(t => t.Iso31661 == language)
                    .Select(t => t.Description)
                    .FirstOrDefault(),
                BackgroundImagePath = album
                    .Images.Where(image => image.Type == "background")
                    .Select(image => image.FilePath)
                    .FirstOrDefault(),
                BackgroundImageColorPalette = album
                    .Images.Where(image => image.Type == "background")
                    .Select(image => image._colorPalette)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<List<AlbumCardDto>> GetLatestAlbumCardsAsync(
        int take = 36,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Albums.AsNoTracking()
            .Where(predicate: album => !string.IsNullOrEmpty(album.Cover) && album.AlbumTrack.Any())
            .OrderByDescending(keySelector: album => album.CreatedAt)
            .ThenBy(keySelector: album => album.Id)
            .Select(selector: album => new AlbumCardDto
            {
                Id = album.Id,
                Name = album.Name,
                Cover = album.Cover,
                Disambiguation = album.Disambiguation,
                Description = album.Description,
                ColorPalette = album._colorPalette ?? string.Empty,
                LibraryId = album.LibraryId,
                Folder = album.Folder,
                Year = album.Year,
                TrackCount = album.AlbumTrack.Count(),
            })
            .Take(count: take)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<List<AlbumCardDto>> GetFavoriteAlbumCardsAsync(
        Guid userId,
        int take = 36,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .AlbumUser.AsNoTracking()
            .Where(predicate: albumUser => albumUser.UserId == userId)
            .OrderBy(keySelector: albumUser => albumUser.Album.Name)
            .ThenBy(keySelector: albumUser => albumUser.Album.Id)
            .Select(selector: albumUser => new AlbumCardDto
            {
                Id = albumUser.Album.Id,
                Name = albumUser.Album.Name,
                Cover = albumUser.Album.Cover,
                Disambiguation = albumUser.Album.Disambiguation,
                Description = albumUser.Album.Description,
                ColorPalette = albumUser.Album._colorPalette ?? string.Empty,
                LibraryId = albumUser.Album.LibraryId,
                Folder = albumUser.Album.Folder,
                Year = albumUser.Album.Year,
                TrackCount = albumUser.Album.AlbumTrack.Count(),
            })
            .Take(count: take)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<List<AlbumCardDto>> GetAlbumCardsByIdsAsync(
        List<Guid> albumIds,
        CancellationToken ct = default
    )
    {
        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await mediaContext
            .Albums.AsNoTracking()
            .Where(predicate: album => albumIds.Contains(album.Id))
            .Select(selector: album => new AlbumCardDto
            {
                Id = album.Id,
                Name = album.Name,
                Cover = album.Cover,
                Disambiguation = album.Disambiguation,
                Description = album.Description,
                ColorPalette = album._colorPalette ?? string.Empty,
                LibraryId = album.LibraryId,
                Folder = album.Folder,
                Year = album.Year,
                TrackCount = album.AlbumTrack.Count(),
            })
            .ToListAsync(cancellationToken: ct);
    }

    #endregion
}
