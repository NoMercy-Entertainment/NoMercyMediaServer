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
using NoMercy.Api.DTOs.Media;
using NoMercy.Api.DTOs.Media.Components;
using NoMercy.Authorization;
using NoMercy.Data.Repositories;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.Controllers.V1.Music;

[ApiController]
[ApiVersion(version: 1.0)]
[Tags(tags: "Music")]
[Authorize(Policy = "MediaAccess")]
[Route(template: "api/v{version:apiVersion}/music")]
public class MusicController : BaseController
{
    private readonly IMusicRepository _musicRepository;

    public MusicController(IMusicRepository musicService)
    {
        _musicRepository = musicService;
    }

    [HttpGet]
    [Route(template: "")]
    [Route(template: "start")]
    public async Task<IActionResult> Index([FromQuery] PageRequestDto request)
    {
        Guid userId = User.UserId();

        // Run 3 groups of 3 queries in parallel using separate DbContext instances
        MusicStartPageData data = await _musicRepository.GetMusicStartPageAsync(userId: userId);

        List<ComponentEnvelope> items = [];
        List<ComponentEnvelope> items2 = [];

        // Add favorite home cards
        if (data.TopArtist is not null && request.Version != "lolomo")
        {
            TopMusicDto favoriteArtist = new(item: data.TopArtist);
            items2.Add(
                item: Component
                    .MusicHomeCard(data: new(topMusic: favoriteArtist))
                    .WithId(id: "favorite-artist")
                    .WithTitle(title: "Most listened artist".Localize())
            );
        }

        if (data.TopAlbum is not null && request.Version != "lolomo")
        {
            TopMusicDto favoriteAlbum = new(item: data.TopAlbum);
            items2.Add(
                item: Component
                    .MusicHomeCard(data: new(topMusic: favoriteAlbum))
                    .WithId(id: "favorite-album")
                    .WithTitle(title: "Most listened album".Localize())
            );
        }

        if (data.TopPlaylist is not null && request.Version != "lolomo")
        {
            TopMusicDto favoritePlaylist = new(item: data.TopPlaylist);
            items2.Add(
                item: Component
                    .MusicHomeCard(data: new(topMusic: favoritePlaylist))
                    .WithId(id: "favorite-playlist")
                    .WithTitle(title: "Most listened playlist".Localize())
            );
        }

        items.Add(item: Component.Container().WithItems(items: items2));

        // Add carousels
        items.Add(
            item: Component
                .Carousel()
                .WithId(id: "favorite-artists")
                .WithTitle(title: "Favorite Artists".Localize())
                .WithNavigation(previousId: "", nextId: "favorite-albums")
                .WithItems(
                    items: data.FavoriteArtists.Select(selector: item =>
                        Component.MusicCard(data: new MusicCardData(artist: item))
                    )
                )
        );

        items.Add(
            item: Component
                .Carousel()
                .WithId(id: "favorite-albums")
                .WithTitle(title: "Favorite Albums".Localize())
                .WithNavigation(previousId: "favorite-artists", nextId: "playlists")
                .WithItems(
                    items: data.FavoriteAlbums.Select(selector: item => Component.MusicCard(data: new MusicCardData(album: item)))
                )
        );

        items.Add(
            item: Component
                .Carousel()
                .WithId(id: "playlists")
                .WithTitle(title: "Playlists".Localize())
                .WithMoreLink(moreLink: "/music/playlists")
                .WithNavigation(previousId: "favorite-albums", nextId: "artists")
                .WithItems(
                    items: data.Playlists.Select(selector: item => Component.MusicCard(data: new MusicCardData(playlist: item)))
                )
        );

        items.Add(
            item: Component
                .Carousel()
                .WithId(id: "artists")
                .WithTitle(title: "Artists".Localize())
                .WithMoreLink(moreLink: "/music/artists/letter/_")
                .WithNavigation(previousId: "playlists", nextId: "albums")
                .WithItems(
                    items: data.LatestArtists.Select(selector: item => Component.MusicCard(data: new MusicCardData(artist: item)))
                )
        );

        items.Add(
            item: Component
                .Carousel()
                .WithId(id: "albums")
                .WithTitle(title: "Albums".Localize())
                .WithMoreLink(moreLink: "/music/albums/letter/_")
                .WithNavigation(previousId: "artists", nextId: "genres")
                .WithItems(
                    items: data.LatestAlbums.Select(selector: item => Component.MusicCard(data: new MusicCardData(album: item)))
                )
        );

        items.Add(
            item: Component
                .Carousel()
                .WithId(id: "genres")
                .WithTitle(title: "Genres".Localize())
                .WithMoreLink(moreLink: "/music/genres/letter/_")
                .WithNavigation(previousId: "albums")
                .WithItems(
                    items: data.LatestGenres.Select(selector: item => Component.MusicCard(data: new MusicCardData(genre: item)))
                )
        );

        return Ok(value: ComponentResponse.From(components: items));
    }

    [HttpPost]
    [Route(template: "start/favorites")]
    public async Task<IActionResult> Favorites()
    {
        Guid userId = User.UserId();

        TopMusicItemDto? topArtist = await _musicRepository.GetTopArtistAsync(userId: userId);
        TopMusicItemDto? topAlbum = await _musicRepository.GetTopAlbumAsync(userId: userId);
        TopMusicItemDto? topPlaylist = await _musicRepository.GetTopPlaylistAsync(userId: userId);

        List<ComponentEnvelope> favoriteItems = [];
        if (topArtist is not null)
            favoriteItems.Add(
                item: Component
                    .MusicHomeCard(data: new(topMusic: new TopMusicDto(item: topArtist)))
                    .WithTitle(title: "Most listened artist".Localize())
            );
        if (topAlbum is not null)
            favoriteItems.Add(
                item: Component
                    .MusicHomeCard(data: new(topMusic: new TopMusicDto(item: topAlbum)))
                    .WithTitle(title: "Most listened album".Localize())
            );
        if (topPlaylist is not null)
            favoriteItems.Add(
                item: Component
                    .MusicHomeCard(data: new(topMusic: new TopMusicDto(item: topPlaylist)))
                    .WithTitle(title: "Most listened playlist".Localize())
            );

        return Ok(
            value: ComponentResponse.From(
                component: Component
                    .Container()
                    .WithId(id: "favorites")
                    .WithNavigation(previousId: "favorites", nextId: "favorite-artists")
                    .WithUpdate(when: "pageLoad", link: "/music/start/favorites")
                    .WithItems(items: favoriteItems)
            )
        );
    }

    [HttpPost]
    [Route(template: "start/favorite-artists")]
    public async Task<IActionResult> FavoriteArtists([FromBody] CardRequestDto request)
    {
        Guid userId = User.UserId();

        List<ArtistCardDto> favoriteArtists = await _musicRepository.GetFavoriteArtistCardsAsync(
            userId: userId
        );

        return Ok(
            value: ComponentResponse.From(
                component: Component
                    .Carousel()
                    .WithId(id: "favorite-artists")
                    .WithNavigation(previousId: "favorite-albums", nextId: "favorite-albums")
                    .WithTitle(title: "Favorite Artists".Localize())
                    .WithUpdate(when: "pageLoad", link: "/music/start/favorite-artists")
                    .WithReplacing(replacingId: request.ReplaceId)
                    .WithItems(
                        items: favoriteArtists.Select(selector: item => Component.MusicCard(data: new MusicCardData(artist: item)))
                    )
            )
        );
    }

    [HttpPost]
    [Route(template: "start/favorite-albums")]
    public async Task<IActionResult> FavoriteAlbums([FromBody] CardRequestDto request)
    {
        Guid userId = User.UserId();

        List<AlbumCardDto> favoriteAlbums = await _musicRepository.GetFavoriteAlbumCardsAsync(
            userId: userId
        );

        return Ok(
            value: ComponentResponse.From(
                component: Component
                    .Carousel()
                    .WithId(id: "favorite-albums")
                    .WithNavigation(previousId: "favorite-artists", nextId: "playlists")
                    .WithTitle(title: "Favorite Albums".Localize())
                    .WithUpdate(when: "pageLoad", link: "/music/start/favorite-albums")
                    .WithReplacing(replacingId: request.ReplaceId)
                    .WithItems(
                        items: favoriteAlbums.Select(selector: item => Component.MusicCard(data: new MusicCardData(album: item)))
                    )
            )
        );
    }

    [HttpPost]
    [Route(template: "start/playlists")]
    public async Task<IActionResult> Playlists([FromBody] CardRequestDto request)
    {
        Guid userId = User.UserId();

        List<PlaylistCardDto> playlists = await _musicRepository.GetPlaylistCardsAsync(userId: userId);

        return Ok(
            value: ComponentResponse.From(
                component: Component
                    .Carousel()
                    .WithId(id: "playlists")
                    .WithNavigation(previousId: "favorite-albums", nextId: "artists")
                    .WithTitle(title: "Playlists".Localize())
                    .WithMoreLink(moreLink: new Uri(uriString: "/music/start/playlists", uriKind: UriKind.Relative))
                    .WithUpdate(when: "pageLoad", link: "/music/start/playlists")
                    .WithReplacing(replacingId: request.ReplaceId)
                    .WithItems(
                        items: playlists.Select(selector: item => Component.MusicCard(data: new MusicCardData(playlist: item)))
                    )
            )
        );
    }

    [NotMapped]
    public class SearchQueryRequest
    {
        [JsonProperty(propertyName: "query")]
        public string Query { get; set; } = string.Empty;

        [JsonProperty(propertyName: "type")]
        public string? Type { get; set; }
    }

    [HttpGet]
    [Route(template: "search")]
    public async Task<IActionResult> Search([FromQuery] SearchQueryRequest request)
    {
        Guid userId = User.UserId();
        string country = Country();
        string normalizedQuery = request.Query.NormalizeSearch();

        // Bound every category. Without this a broad query fans thousands of matches
        // through cross-reference and card projection into a multi-MB payload, while
        // the view only renders the top result, six tracks, and the carousels. Capping
        // the id lists keeps the rendered first-N identical and drops only the tail.
        const int resultCap = UiLimits.SearchResultsPerCategory;

        // Step 1: Get IDs using search methods
        List<Guid> artistIds = (await _musicRepository.SearchArtistIdsAsync(normalizedQuery: normalizedQuery))
            .Take(count: resultCap)
            .ToList();
        List<Guid> albumIds = (await _musicRepository.SearchAlbumIdsAsync(normalizedQuery: normalizedQuery))
            .Take(count: resultCap)
            .ToList();
        List<Guid> playlistIds = (await _musicRepository.SearchPlaylistIdsAsync(normalizedQuery: normalizedQuery))
            .Take(count: resultCap)
            .ToList();
        List<Guid> trackIds = (await _musicRepository.SearchTrackIdsAsync(normalizedQuery: normalizedQuery))
            .Take(count: resultCap)
            .ToList();

        // Step 2: Cross-reference to find additional artists/albums
        List<Guid> additionalArtistIds = [];
        if (albumIds.Count > 0)
            additionalArtistIds.AddRange(
                collection: await _musicRepository.GetArtistIdsFromAlbumsAsync(albumIds: albumIds)
            );
        if (playlistIds.Count > 0)
            additionalArtistIds.AddRange(
                collection: await _musicRepository.GetArtistIdsFromPlaylistTracksAsync(playlistIds: playlistIds)
            );
        if (trackIds.Count > 0)
            additionalArtistIds.AddRange(
                collection: await _musicRepository.GetArtistIdsFromTracksAsync(trackIds: trackIds)
            );

        List<Guid> allArtistIds = artistIds
            .Union(second: additionalArtistIds)
            .Distinct()
            .Take(count: resultCap)
            .ToList();

        List<Guid> additionalAlbumIds = [];
        if (trackIds.Count > 0)
            additionalAlbumIds.AddRange(
                collection: await _musicRepository.GetAlbumIdsFromTracksAsync(trackIds: trackIds)
            );

        List<Guid> allAlbumIds = albumIds
            .Union(second: additionalAlbumIds)
            .Distinct()
            .Take(count: resultCap)
            .ToList();

        // Step 3: Get projection data
        List<ArtistCardDto> artists =
            allArtistIds.Count > 0
                ? await _musicRepository.GetArtistCardsByIdsAsync(artistIds: allArtistIds)
                : [];
        List<AlbumCardDto> albums =
            allAlbumIds.Count > 0
                ? await _musicRepository.GetAlbumCardsByIdsAsync(albumIds: allAlbumIds)
                : [];
        List<PlaylistCardDto> playlistCards =
            playlistIds.Count > 0
                ? await _musicRepository.GetPlaylistCardsByIdsAsync(playlistIds: playlistIds)
                : [];
        List<SearchTrackCardDto> tracks =
            trackIds.Count > 0
                ? await _musicRepository.SearchTrackCardsAsync(trackIds: trackIds, userId: userId, country: country)
                : [];

        if (
            artists.Count == 0
            && albums.Count == 0
            && playlistCards.Count == 0
            && tracks.Count == 0
        )
            return NotFoundResponse(detail: "No results found");

        SearchTrackCardDto? topTrack = tracks.FirstOrDefault();
        ArtistCardDto? topArtist = artists.FirstOrDefault();
        AlbumCardDto? topAlbum = albums.FirstOrDefault();

        // Build TopResultCardData from the first match
        TopResultCardData? topResultData =
            topTrack != null ? new(track: topTrack)
            : topArtist != null ? new(artist: topArtist)
            : topAlbum != null ? new TopResultCardData(album: topAlbum)
            : null;

        List<TrackRowData> songResults = tracks
            .Take(count: 6)
            .Select(selector: track => new TrackRowData(track: track))
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
                                    .WithTitle(title: "Tracks".Localize())
                                    .WithItems(
                                        builders: songResults.Select(selector: track =>
                                            Component.TrackRow(data: track).WithDisplayList(displayList: songResults)
                                        )
                                    )
                            ]
                        )
                        .Build(),
                    Component
                        .Carousel()
                        .WithId(id: "artists")
                        .WithTitle(title: "Artist".Localize())
                        .WithItems(items: artists.Select(selector: item => Component.MusicCard(data: new MusicCardData(artist: item))))
                        .Build(),
                    Component
                        .Carousel()
                        .WithId(id: "albums")
                        .WithTitle(title: "Albums".Localize())
                        .WithItems(items: albums.Select(selector: item => Component.MusicCard(data: new MusicCardData(album: item))))
                        .Build(),
                    Component
                        .Carousel()
                        .WithId(id: "playlists")
                        .WithTitle(title: "Playlists".Localize())
                        .WithItems(
                            items: playlistCards.Select(selector: item => Component.MusicCard(data: new MusicCardData(playlist: item)))
                        )
                ]
            )
        );
    }

    [HttpPost]
    [Route(template: "search/{query}/{Type}")]
    public IActionResult TypeSearch(string query, string type)
    {
        return Ok(value: new PlaceholderResponse { Data = [] });
    }
}
