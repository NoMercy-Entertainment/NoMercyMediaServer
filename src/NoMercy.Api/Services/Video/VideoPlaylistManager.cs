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

using NoMercy.Api.DTOs.Media;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.NmSystem.Domain;

namespace NoMercy.Api.Services.Video;

public class VideoPlaylistManager
{
    private readonly IMovieRepository _movieRepository;
    private readonly ITvShowRepository _tvShowRepository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly ISpecialRepository _specialRepository;
    private readonly MediaContext _mediaContext;

    public VideoPlaylistManager(
        MediaContext mediaContext,
        IMovieRepository movieRepository,
        ICollectionRepository collectionRepository,
        ISpecialRepository specialRepository,
        ITvShowRepository tvShowRepository
    )
    {
        _movieRepository = movieRepository;
        _tvShowRepository = tvShowRepository;
        _collectionRepository = collectionRepository;
        _specialRepository = specialRepository;
        _mediaContext = mediaContext;
    }

    public async Task<(
        VideoPlaylistResponseDto? item,
        List<VideoPlaylistResponseDto> playlist
    )> GetPlaylist(
        Guid userId,
        string type,
        dynamic listId,
        int? itemId,
        string language,
        string country
    )
    {
        return type switch
        {
            MediaTypes.SpecialMediaType => await GetSpecialItems(
                userId: userId,
                listId: listId,
                itemId: itemId,
                language: language,
                country: country
            ),
            MediaTypes.CollectionMediaType => await GetCollectionItems(
                userId: userId,
                listId: listId,
                itemId: itemId,
                language: language,
                country: country
            ),
            MediaTypes.TvMediaType => await GetTvItems(userId: userId, listId: listId, itemId: itemId, language: language, country: country),
            MediaTypes.MovieMediaType => await GetMovieItems(userId: userId, listId: listId, itemId: itemId, language: language, country: country),
            _ => throw new ArgumentException(message: "Invalid playlist type", paramName: nameof(type)),
        };
    }

    public (
        List<VideoPlaylistResponseDto> before,
        List<VideoPlaylistResponseDto> after
    ) SplitPlaylist(List<VideoPlaylistResponseDto> playlist, int currentTrackId)
    {
        int index = playlist.FindIndex(match: p => p.Id == currentTrackId);
        if (index == -1)
            return ([], playlist);

        List<VideoPlaylistResponseDto> before = playlist.GetRange(index: 0, count: index);
        List<VideoPlaylistResponseDto> after = playlist.GetRange(
            index: index + 1,
            count: playlist.Count - index - 1
        );

        return (before, after);
    }

    private async Task<(
        VideoPlaylistResponseDto? item,
        List<VideoPlaylistResponseDto> playlist
    )> GetSpecialItems(Guid userId, dynamic listId, int? itemId, string language, string country)
    {
        Special? special = await _specialRepository.GetSpecialPlaylistAsync(
            userId: userId,
            id: Ulid.Parse(listId),
            language: language,
            country: country
        );

        List<VideoPlaylistResponseDto> playlist =
            special
                ?.Items.OrderBy(keySelector: item => item.Order)
                .Select(
                    selector: (item, index) =>
                        item.EpisodeId is not null
                            ? new(
                                episode: item.Episode ?? new Episode(),
                                playlistType: MediaTypes.SpecialMediaType,
                                playlistId: listId,
                                country: country,
                                index: index
                            )
                            : new VideoPlaylistResponseDto(
                                movie: item.Movie ?? new Movie(),
                                playlistType: MediaTypes.SpecialMediaType,
                                playlistId: listId,
                                country: country,
                                index: index
                            )
                )
                .ToList()
            ?? [];

        VideoPlaylistResponseDto? item = playlist.FirstOrDefault(predicate: p => p.Id == itemId);

        if (item is null && playlist.Any(predicate: p => p.Progress?.Date is not null))
        {
            item = playlist.OrderByDescending(keySelector: p => p.Progress?.Date).FirstOrDefault();
        }
        if (item is null && playlist.Count != 0)
        {
            item = playlist.FirstOrDefault();
        }

        return (item, playlist);
    }

    private async Task<(
        VideoPlaylistResponseDto? item,
        List<VideoPlaylistResponseDto> playlist
    )> GetCollectionItems(Guid userId, dynamic listId, int? itemId, string language, string country)
    {
        Collection? collection = await _collectionRepository.GetCollectionPlaylistAsync(
            userId: userId,
            id: int.Parse(listId),
            language: language,
            country: country
        );

        List<VideoPlaylistResponseDto> playlist =
            collection
                ?.CollectionMovies.Select(
                    selector: (movie, index) =>
                        new VideoPlaylistResponseDto(
                            movie: movie.Movie,
                            playlistType: MediaTypes.CollectionMediaType,
                            playlistId: listId,
                            country: country,
                            index: index + 1,
                            collection: collection
                        )
                )
                .ToList()
            ?? [];

        VideoPlaylistResponseDto? item = playlist.FirstOrDefault(predicate: p => p.Id == itemId);

        if (item is null && playlist.Any(predicate: p => p.Progress?.Date is not null))
        {
            item = playlist.OrderByDescending(keySelector: p => p.Progress?.Date).FirstOrDefault();
        }
        if (item is null && playlist.Count != 0)
        {
            item = playlist.FirstOrDefault();
        }

        return (item, playlist);
    }

    private async Task<(
        VideoPlaylistResponseDto? item,
        List<VideoPlaylistResponseDto> playlist
    )> GetTvItems(Guid userId, dynamic listId, int? itemId, string language, string country)
    {
        Tv? tv = await _tvShowRepository.GetPlaylistAsync(
            userId: userId,
            id: int.Parse(listId),
            language: language,
            country: country
        );

        VideoPlaylistResponseDto[] episodes =
            tv?.Seasons.Where(predicate: season => season.SeasonNumber > 0)
                .SelectMany(selector: season => season.Episodes)
                .Select(selector: episode => new VideoPlaylistResponseDto(
                    episode: episode,
                    playlistType: MediaTypes.TvMediaType,
                    playlistId: listId,
                    country: country
                ))
                .ToArray()
            ?? [];

        VideoPlaylistResponseDto[] extras =
            tv?.Seasons.Where(predicate: season => season.SeasonNumber == 0)
                .SelectMany(selector: season => season.Episodes)
                .Select(selector: episode => new VideoPlaylistResponseDto(
                    episode: episode,
                    playlistType: MediaTypes.TvMediaType,
                    playlistId: listId,
                    country: country
                ))
                .ToArray()
            ?? [];

        List<VideoPlaylistResponseDto> playlist = episodes.Concat(second: extras).ToList();

        VideoPlaylistResponseDto? item = playlist.FirstOrDefault(predicate: p => p.Id == itemId);

        if (item is null && playlist.Any(predicate: p => p.Progress?.Date is not null))
        {
            item = playlist.OrderByDescending(keySelector: p => p.Progress?.Date).FirstOrDefault();
        }
        if (item is null && playlist.Count != 0)
        {
            item = playlist.FirstOrDefault();
        }

        return (item, playlist);
    }

    private async Task<(
        VideoPlaylistResponseDto? item,
        List<VideoPlaylistResponseDto> playlist
    )> GetMovieItems(Guid userId, dynamic listId, int? itemId, string language, string country)
    {
        List<Movie> movies = await _movieRepository.GetMoviePlaylistAsync(
            userId: userId,
            id: int.Parse(listId),
            language: language,
            country: country
        );
        List<VideoPlaylistResponseDto> playlist = movies
            .Select(selector: movie => new VideoPlaylistResponseDto(
                movie: movie,
                playlistType: MediaTypes.MovieMediaType,
                playlistId: int.Parse(listId),
                country: country
            ))
            .ToList();

        VideoPlaylistResponseDto? item =
            playlist.FirstOrDefault(predicate: p => p.Id == itemId) ?? playlist.FirstOrDefault();

        return (item, playlist);
    }
}
