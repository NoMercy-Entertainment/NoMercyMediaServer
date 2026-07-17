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
            .Where(library => library.LibraryUsers.Any(u => u.UserId == userId))
            .Include(library => library.FolderLibraries)
                .ThenInclude(fl => fl.Folder)
                    .ThenInclude(f => f.EncodingPresetFolders)
                        .ThenInclude(link => link.Preset)
            .Include(library => library.LanguageLibraries)
                .ThenInclude(ll => ll.Language)
            .Include(library => library.LibraryMovies)
            .Include(library => library.LibraryTvs)
            .OrderBy(library => library.Order)
            .ToListAsync();
    }

    public Task<List<Playlist>> GetSetupPlaylistsAsync(Guid userId)
    {
        return _mediaContext
            .Playlists.AsNoTracking()
            .Where(playlist => playlist.UserId == userId)
            .ToListAsync();
    }

    public async Task<ScreensaverDto> GetSetupScreensaverContent(Guid userId)
    {
        HashSet<Image> data = await _homeRepository.GetScreensaverImagesAsync(userId);

        // Logo lookups built once. The old per-backdrop FirstOrDefault over a lazy
        // logo filter re-scanned every image for each backdrop (O(backdrops x images)),
        // seconds of CPU on a large library. Index the logos by title id instead.
        Dictionary<int, Image> logoByTv = data.Where(image =>
                image is { Type: "logo", TvId: not null }
            )
            .GroupBy(image => image.TvId!.Value)
            .ToDictionary(group => group.Key, group => group.First());
        Dictionary<int, Image> logoByMovie = data.Where(image =>
                image is { Type: "logo", MovieId: not null }
            )
            .GroupBy(image => image.MovieId!.Value)
            .ToDictionary(group => group.Key, group => group.First());

        IEnumerable<ScreensaverDataDto> tvCollection = data.Where(image =>
                image is { TvId: not null, Type: "backdrop" }
            )
            .DistinctBy(image => image.TvId)
            .Select(image => new ScreensaverDataDto(
                image,
                logoByTv.GetValueOrDefault(image.TvId!.Value)
            ));

        IEnumerable<ScreensaverDataDto> movieCollection = data.Where(image =>
                image is { MovieId: not null, Type: "backdrop" }
            )
            .DistinctBy(image => image.MovieId)
            .Select(image => new ScreensaverDataDto(
                image,
                logoByMovie.GetValueOrDefault(image.MovieId!.Value)
            ));

        return new()
        {
            Data = tvCollection
                .Concat(movieCollection)
                .Where(image => image.Meta?.Logo != null)
                .Randomize(),
        };
    }
}
