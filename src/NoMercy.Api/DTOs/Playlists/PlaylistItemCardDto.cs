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

using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Playlists;
using NoMercy.Database.Models.TvShows;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.DTOs.Playlists;

/// <summary>
/// One entry of a VIDEO-ONLY playlist, rendered as a single unified card shape
/// regardless of <see cref="Kind"/>. Deliberately NOT a reuse of NmCardDto:
/// movie/tv/episode/special share NmCardDto's shape closely, but episode has
/// no NmCardDto constructor at all. A unified DTO lets a client render one
/// list (map over items switching on `kind`) and route every item to the
/// video player from a single field: <see cref="PlayLink"/> (or null → not
/// playable yet) always resolves to a "/{type}/{id}/watch" route, matching
/// the convention NmCardDto and CardData already use. There is no music
/// branch — this feature never contains a track.
/// </summary>
public record PlaylistItemCardDto
{
    /// <summary>The PlaylistItem's own id — pass this to DELETE /items/{itemId} and PUT /items/order.</summary>
    [JsonProperty(propertyName: "id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>movie | tv | episode | special.</summary>
    [JsonProperty(propertyName: "kind")]
    public string Kind { get; set; } = string.Empty;

    /// <summary>The underlying Movie/Tv/Episode/Special id, stringified (types vary by kind).</summary>
    [JsonProperty(propertyName: "media_id")]
    public string MediaId { get; set; } = string.Empty;

    [JsonProperty(propertyName: "order")]
    public int Order { get; set; }

    [JsonProperty(propertyName: "title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty(propertyName: "overview")]
    public string? Overview { get; set; }

    [JsonProperty(propertyName: "poster")]
    public string? Poster { get; set; }

    [JsonProperty(propertyName: "backdrop")]
    public string? Backdrop { get; set; }

    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "year")]
    public int? Year { get; set; }

    /// <summary>Runtime in seconds, when known.</summary>
    [JsonProperty(propertyName: "duration")]
    public int? Duration { get; set; }

    /// <summary>The item's detail-page link (e.g. /movie/123, /tv/1399).</summary>
    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; } = null!;

    /// <summary>The watch/play link — present only when the item currently has playable media.</summary>
    [JsonProperty(propertyName: "play_link")]
    public Uri? PlayLink { get; set; }

    public static PlaylistItemCardDto From(PlaylistItem item) =>
        item.Kind switch
        {
            PlaylistItemKind.Movie => FromMovie(item: item),
            PlaylistItemKind.Tv => FromTv(item: item),
            PlaylistItemKind.Episode => FromEpisode(item: item),
            PlaylistItemKind.Special => FromSpecial(item: item),
            _ => throw new ArgumentOutOfRangeException(
                paramName: nameof(item),
                actualValue: item.Kind,
                message: "Unknown PlaylistItemKind"
            ),
        };

    private static PlaylistItemCardDto FromMovie(PlaylistItem item)
    {
        Movie movie =
            item.Movie ?? throw new InvalidOperationException(message: "PlaylistItem.Movie not loaded");
        string? title = movie.Translations.FirstOrDefault()?.Title;
        string? overview = movie.Translations.FirstOrDefault()?.Overview;
        bool playable = movie.VideoFiles.Any(predicate: v => v.Folder != null);

        return new()
        {
            Id = item.Id.ToString(),
            Kind = PlaylistItemKind.Movie.ToWireString(),
            MediaId = movie.Id.ToString(),
            Order = item.Order,
            Title = !string.IsNullOrEmpty(value: title) ? title : movie.Title,
            Overview = !string.IsNullOrEmpty(value: overview) ? overview : movie.Overview,
            Poster = movie.Poster,
            Backdrop = movie.Backdrop,
            ColorPalette = movie.ColorPalette,
            Year = movie.ReleaseDate.ParseYear(),
            Duration = movie
                .VideoFiles.FirstOrDefault(predicate: v => v.Folder != null)
                ?.Duration?.ToSeconds(),
            Link = new(uriString: $"/movie/{movie.Id}", uriKind: UriKind.Relative),
            PlayLink = playable ? new(uriString: $"/movie/{movie.Id}/watch", uriKind: UriKind.Relative) : null,
        };
    }

    private static PlaylistItemCardDto FromTv(PlaylistItem item)
    {
        Tv tv = item.Tv ?? throw new InvalidOperationException(message: "PlaylistItem.Tv not loaded");
        string? title = tv.Translations.FirstOrDefault()?.Title;
        string? overview = tv.Translations.FirstOrDefault()?.Overview;

        return new()
        {
            Id = item.Id.ToString(),
            Kind = PlaylistItemKind.Tv.ToWireString(),
            MediaId = tv.Id.ToString(),
            Order = item.Order,
            Title = !string.IsNullOrEmpty(value: title) ? title : tv.Title,
            Overview = !string.IsNullOrEmpty(value: overview) ? overview : tv.Overview,
            Poster = tv.Poster,
            Backdrop = tv.Backdrop,
            ColorPalette = tv.ColorPalette,
            Year = tv.FirstAirDate.ParseYear(),
            Link = new(uriString: $"/tv/{tv.Id}", uriKind: UriKind.Relative),
            // Whether the show currently has a playable episode isn't loaded on this
            // read path (GetPlaylistItemsAsync doesn't Include Tv.Episodes), so this
            // mirrors NmCardDto(UserData)'s continue-watching Tv branch: always link
            // to the show's watch route and let it resolve the next episode.
            PlayLink = new(uriString: $"/tv/{tv.Id}/watch", uriKind: UriKind.Relative),
        };
    }

    private static PlaylistItemCardDto FromEpisode(PlaylistItem item)
    {
        Episode episode =
            item.Episode ?? throw new InvalidOperationException(message: "PlaylistItem.Episode not loaded");
        string? title = episode.Translations.FirstOrDefault()?.Title;
        string? overview = episode.Translations.FirstOrDefault()?.Overview;
        bool playable = episode.VideoFiles.Any(predicate: v => v.Folder != null);

        return new()
        {
            Id = item.Id.ToString(),
            Kind = PlaylistItemKind.Episode.ToWireString(),
            MediaId = episode.Id.ToString(),
            Order = item.Order,
            Title = !string.IsNullOrEmpty(value: title) ? title : episode.Title.OrEmpty(),
            Overview = !string.IsNullOrEmpty(value: overview) ? overview : episode.Overview,
            Backdrop = episode.Still,
            ColorPalette = episode.ColorPalette,
            Year = episode.AirDate.ParseYear(),
            Duration = episode
                .VideoFiles.FirstOrDefault(predicate: v => v.Folder != null)
                ?.Duration?.ToSeconds(),
            Link = new(uriString: $"/tv/{episode.TvId}", uriKind: UriKind.Relative),
            PlayLink = playable
                ? new(
                    uriString: $"/tv/{episode.TvId}/watch?season={episode.SeasonNumber}&episode={episode.EpisodeNumber}",
                    uriKind: UriKind.Relative
                )
                : null,
        };
    }

    private static PlaylistItemCardDto FromSpecial(PlaylistItem item)
    {
        Special special =
            item.Special ?? throw new InvalidOperationException(message: "PlaylistItem.Special not loaded");

        return new()
        {
            Id = item.Id.ToString(),
            Kind = PlaylistItemKind.Special.ToWireString(),
            MediaId = special.Id.ToString(),
            Order = item.Order,
            Title = special.Title.OrEmpty(),
            Overview = special.Overview,
            Poster = special.Poster,
            Backdrop = special.Backdrop,
            ColorPalette = special.ColorPalette,
            Link = new(uriString: $"/specials/{special.Id}", uriKind: UriKind.Relative),
            PlayLink = new(uriString: $"/specials/{special.Id}/watch", uriKind: UriKind.Relative),
        };
    }
}
