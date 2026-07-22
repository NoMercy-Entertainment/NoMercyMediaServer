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

using System.ComponentModel.DataAnnotations.Schema;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NoMercy.Api.DTOs.Media.Components;
using NoMercy.Api.DTOs.Music;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.TvShows;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.Controllers.V1.Media;

[ApiController]
[Tags(tags: "Media Search")]
[ApiVersion(version: 1.0)]
[Authorize(Policy = "MediaAccess")]
[Route(template: "api/v{version:apiVersion}/search")]
public class SearchController : BaseController
{
    private readonly IMusicRepository _musicRepository;
    private readonly ILibraryRepository _libraryRepository;

    public SearchController(IMusicRepository musicService, ILibraryRepository libraryRepository)
    {
        _musicRepository = musicService;
        _libraryRepository = libraryRepository;
    }

    [HttpGet(template: "music")]
    [ResponseCache(NoStore = true)]
    public async Task<IActionResult> SearchMusic(
        [FromQuery] SearchQueryRequest request,
        CancellationToken ct = default
    )
    {
        string country = Country();
        string normalizedQuery = request.Query.NormalizeSearch();

        (List<Artist> artists, List<Album> albums, List<Playlist> playlists, List<Track> songs) =
            await FetchMusicSearchResultsAsync(normalizedQuery: normalizedQuery, ct: ct);

        Track? topTrack = songs.FirstOrDefault();
        Artist? topArtist = artists.FirstOrDefault();
        Album? topAlbum = albums.FirstOrDefault();

        TopResultCardData? topResultData =
            topTrack != null ? new(track: topTrack)
            : topArtist != null ? new(artist: topArtist)
            : topAlbum != null ? new TopResultCardData(album: topAlbum)
            : null;

        List<TrackRowData> songResults = songs
            .Take(count: 6)
            .Select(selector: track => new TrackRowData(track: track, country: country))
            .ToList();

        return Ok(
            value: ComponentResponse.From(components:
                [
                    Component
                        .Container()
                        .WithId(id: "search-results")
                        .WithItems(items:
                            [
                                Component
                                    .TopResultCard(data: topResultData!)
                                    .WithId(id: "top-result")
                                    .WithTitle(title: "Top Result".Localize())
                                    .Build(),
                                Component
                                    .List()
                                    .WithId(id: "tracks")
                                    .WithProperties(
                                        properties: new()
                                        {
                                            { "paddingTop", 0 },
                                            { "paddingBottom", 0 },
                                            { "paddingStart", 0 },
                                            { "paddingEnd", 0 },
                                        }
                                    )
                                    .WithTitle(title: "Tracks".Localize())
                                    .WithItems(
                                        builders: songResults.Select(selector: track =>
                                            Component
                                                .TrackRow(data: track)
                                                .WithProperties(
                                                    properties: new()
                                                    {
                                                        { "paddingTop", 0 },
                                                        { "paddingBottom", 0 },
                                                        { "paddingStart", 0 },
                                                        { "paddingEnd", 0 },
                                                    }
                                                )
                                                .WithDisplayList(displayList: songResults)
                                        )
                                    )
                            ]
                        ),
                    Component
                        .Carousel()
                        .WithId(id: "artists")
                        .WithTitle(title: "Artist".Localize())
                        .WithItems(
                            items: artists
                                .GroupBy(keySelector: artist => artist.Id)
                                .Select(selector: group => group.First())
                                .Select(selector: item => Component.MusicCard(data: new ArtistsResponseItemDto(artist: item)))
                        ),
                    Component
                        .Carousel()
                        .WithId(id: "albums")
                        .WithTitle(title: "Albums".Localize())
                        .WithItems(
                            items: albums
                                .GroupBy(keySelector: album => album.Id)
                                .Select(selector: group => group.First())
                                .Select(selector: item => Component.MusicCard(data: new ArtistsResponseItemDto(album: item)))
                        ),
                    Component
                        .Carousel()
                        .WithId(id: "playlists")
                        .WithTitle(title: "Playlists".Localize())
                        .WithItems(
                            items: playlists
                                .GroupBy(keySelector: playlist => playlist.Id)
                                .Select(selector: group => group.First())
                                .Select(selector: item => Component.MusicCard(data: new PlaylistResponseItemDto(playlist: item)))
                        )
                ]
            )
        );
    }

    [HttpGet(template: "music/tv")]
    public async Task<IActionResult> SearchTvMusic(
        [FromQuery] SearchQueryRequest request,
        CancellationToken ct = default
    )
    {
        string normalizedQuery = request.Query.NormalizeSearch();

        (List<Artist> artists, List<Album> albums, List<Playlist> _, List<Track> _) =
            await FetchMusicSearchResultsAsync(normalizedQuery: normalizedQuery, ct: ct);

        List<ComponentEnvelope> musicCards =
        [
            .. artists
                .GroupBy(keySelector: artist => artist.Id)
                .Select(selector: group => group.First())
                .OrderBy(keySelector: artist => artist.Name)
                .Select(selector: item => Component.MusicCard(data: new ArtistsResponseItemDto(artist: item))),
            .. albums
                .GroupBy(keySelector: album => album.Id)
                .Select(selector: group => group.First())
                .OrderBy(keySelector: album => album.Name)
                .Select(selector: item => Component.MusicCard(data: new AlbumsResponseItemDto(album: item))),
        ];

        return Ok(
            value: ComponentResponse.From(
                component: Component
                    .Grid()
                    .WithId(id: "tv-music-search")
                    .WithProperties(properties: new() { { "columns", 4 }, { "spacing", 16 } })
                    .WithItems(items: musicCards)
                    .Build()
            )
        );
    }

    [HttpGet(template: "video")]
    [ResponseCache(NoStore = true)]
    public async Task<IActionResult> SearchVideo(
        [FromQuery] SearchQueryRequest request,
        CancellationToken ct = default
    )
    {
        string country = Country();
        string normalizedQuery = request.Query.NormalizeSearch();

        return Ok(
            value: ComponentResponse.From(component: await BuildVideoSearchGridAsync(normalizedQuery: normalizedQuery, country: country, ct: ct))
        );
    }

    [HttpGet(template: "video/tv")]
    public async Task<IActionResult> SearchTvVideo(
        [FromQuery] SearchQueryRequest request,
        CancellationToken ct = default
    )
    {
        string country = Country();
        string normalizedQuery = request.Query.NormalizeSearch();

        return Ok(
            value: ComponentResponse.From(component: await BuildVideoSearchGridAsync(normalizedQuery: normalizedQuery, country: country, ct: ct))
        );
    }

    private async Task<(
        List<Artist> Artists,
        List<Album> Albums,
        List<Playlist> Playlists,
        List<Track> Songs
    )> FetchMusicSearchResultsAsync(string normalizedQuery, CancellationToken ct)
    {
        // Step 1: Get IDs sequentially (MusicRepository uses a single scoped DbContext, not thread-safe).
        // Cap each category: a broad query otherwise fans thousands of full entity graphs through
        // SearchMusicFullDataAsync. Only the top result, six tracks, and the carousels render.
        const int resultCap = UiLimits.SearchResultsPerCategory;
        List<Guid> artistIds = (await _musicRepository.SearchArtistIdsAsync(normalizedQuery: normalizedQuery, ct: ct))
            .Take(count: resultCap)
            .ToList();
        List<Guid> albumIds = (await _musicRepository.SearchAlbumIdsAsync(normalizedQuery: normalizedQuery, ct: ct))
            .Take(count: resultCap)
            .ToList();
        List<Guid> playlistIds = (
            await _musicRepository.SearchPlaylistIdsAsync(normalizedQuery: normalizedQuery, ct: ct)
        )
            .Take(count: resultCap)
            .ToList();
        List<Guid> trackIds = (await _musicRepository.SearchTrackIdsAsync(normalizedQuery: normalizedQuery, ct: ct))
            .Take(count: resultCap)
            .ToList();

        // Step 2: Query full data using the IDs in parallel (repository owns the fan-out)
        MusicSearchFullData fullData = await _musicRepository.SearchMusicFullDataAsync(
            artistIds: artistIds,
            albumIds: albumIds,
            playlistIds: playlistIds,
            trackIds: trackIds,
            ct: ct
        );

        List<Artist> artists = fullData.Artists;
        List<Album> albums = fullData.Albums;
        List<Playlist> playlists = fullData.Playlists;
        List<Track> songs = fullData.Songs;

        if (albums.Count > 0)
            foreach (Album album in albums)
                if (album.AlbumTrack.Count > 0)
                    foreach (
                        IEnumerable<Artist> artist in album
                            .AlbumTrack.Select(selector: albumTrack =>
                                albumTrack.Track.ArtistTrack.Select(selector: artistTrack =>
                                    artistTrack.Artist
                                )
                            )
                            .ToList()
                    )
                        artists.AddRange(collection: artist);

        if (playlists.Count > 0)
            foreach (Playlist playlist in playlists)
                if (playlist.Tracks.Count > 0)
                    foreach (
                        IEnumerable<Artist> artist in playlist
                            .Tracks.Select(selector: playlistTrack =>
                                playlistTrack.Track.ArtistTrack.Select(selector: artistTrack =>
                                    artistTrack.Artist
                                )
                            )
                            .ToList()
                    )
                        artists.AddRange(collection: artist);

        if (songs.Count > 0)
            foreach (Track song in songs)
            {
                if (song.ArtistTrack.Count > 0)
                    artists.AddRange(collection: song.ArtistTrack.Select(selector: artistTrack => artistTrack.Artist));
                if (song.AlbumTrack.Count > 0)
                    albums.AddRange(collection: song.AlbumTrack.Select(selector: albumTrack => albumTrack.Album));
            }

        return (artists, albums, playlists, songs);
    }

    private async Task<ComponentEnvelope> BuildVideoSearchGridAsync(
        string normalizedQuery,
        string country,
        CancellationToken ct
    )
    {
        VideoSearchResults videoResults = await _libraryRepository.SearchVideoByTitleAsync(
            normalizedQuery: normalizedQuery,
            ct: ct
        );

        List<CardData> cardItems = videoResults
            .Tvs.Concat<dynamic>(second: videoResults.Movies)
            .OrderBy(keySelector: item => item is Tv tv ? tv.Title : ((Movie)item).Title)
            .Select(selector: item => new CardData(item, country))
            .ToList();

        return Component
            .Grid()
            .WithItems(items: cardItems.Select(selector: item => Component.Card().WithData(data: item).Build()))
            .Build();
    }

    [NotMapped]
    public class SearchQueryRequest
    {
        [JsonProperty(propertyName: "query")]
        public string Query { get; set; } = string.Empty;

        [JsonProperty(propertyName: "type")]
        public string? Type { get; set; }
    }
}
