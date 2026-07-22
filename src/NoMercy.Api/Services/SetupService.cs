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
using NoMercy.Api.DTOs.Media;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Music;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.Services;

public class SetupService
{
    private readonly MediaContext _mediaContext;
    private readonly ILibraryRepository _libraryRepository;
    private readonly IHomeRepository _homeRepository;

    public SetupService(
        IHomeRepository homeRepository,
        ILibraryRepository libraryRepository,
        MediaContext mediaContext
    )
    {
        _homeRepository = homeRepository;
        _libraryRepository = libraryRepository;
        _mediaContext = mediaContext;
    }

    public Task<List<Library>> GetSetupLibraries(Guid userId)
    {
        return _mediaContext
            .Libraries.AsNoTracking()
            .Where(predicate: library => library.LibraryUsers.Any(u => u.UserId == userId))
            .Include(navigationPropertyPath: library => library.FolderLibraries)
                .ThenInclude(navigationPropertyPath: fl => fl.Folder)
                    .ThenInclude(navigationPropertyPath: f => f.EncodingPresetFolders)
                        .ThenInclude(navigationPropertyPath: link => link.Preset)
            .Include(navigationPropertyPath: library => library.LanguageLibraries)
                .ThenInclude(navigationPropertyPath: ll => ll.Language)
            .Include(navigationPropertyPath: library => library.LibraryMovies)
            .Include(navigationPropertyPath: library => library.LibraryTvs)
            .OrderBy(keySelector: library => library.Order)
            .ToListAsync();
    }

    public Task<List<Playlist>> GetSetupPlaylistsAsync(Guid userId)
    {
        return _mediaContext
            .Playlists.AsNoTracking()
            .Where(predicate: playlist => playlist.UserId == userId)
            .ToListAsync();
    }

    public async Task<ScreensaverDto> GetSetupScreensaverContent(Guid userId)
    {
        HashSet<Image> data = await _homeRepository.GetScreensaverImagesAsync(userId: userId);

        // Logo lookups built once. The old per-backdrop FirstOrDefault over a lazy
        // logo filter re-scanned every image for each backdrop (O(backdrops x images)),
        // seconds of CPU on a large library. Index the logos by title id instead.
        Dictionary<int, Image> logoByTv = data.Where(predicate: image =>
                image is { Type: "logo", TvId: not null }
            )
            .GroupBy(keySelector: image => image.TvId!.Value)
            .ToDictionary(keySelector: group => group.Key, elementSelector: group => group.First());
        Dictionary<int, Image> logoByMovie = data.Where(predicate: image =>
                image is { Type: "logo", MovieId: not null }
            )
            .GroupBy(keySelector: image => image.MovieId!.Value)
            .ToDictionary(keySelector: group => group.Key, elementSelector: group => group.First());

        IEnumerable<ScreensaverDataDto> tvCollection = data.Where(predicate: image =>
                image is { TvId: not null, Type: "backdrop" }
            )
            .DistinctBy(keySelector: image => image.TvId)
            .Select(selector: image => new ScreensaverDataDto(
                image: image,
                logo: logoByTv.GetValueOrDefault(key: image.TvId!.Value)
            ));

        IEnumerable<ScreensaverDataDto> movieCollection = data.Where(predicate: image =>
                image is { MovieId: not null, Type: "backdrop" }
            )
            .DistinctBy(keySelector: image => image.MovieId)
            .Select(selector: image => new ScreensaverDataDto(
                image: image,
                logo: logoByMovie.GetValueOrDefault(key: image.MovieId!.Value)
            ));

        return new()
        {
            Data = tvCollection
                .Concat(second: movieCollection)
                .Where(predicate: image => image.Meta?.Logo != null)
                .Randomize(),
        };
    }
}
