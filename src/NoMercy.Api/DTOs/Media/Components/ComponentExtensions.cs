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

using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.TvShows;

namespace NoMercy.Api.DTOs.Media.Components;

/// <summary>
/// Extension methods for converting domain models to component data.
/// </summary>
public static class ComponentExtensions
{
    #region Movie Extensions

    /// <summary>
    /// Converts a Movie to a Card component.
    /// </summary>
    public static ComponentEnvelope ToCard(this Movie movie, string country, bool watch = false)
    {
        return Component.Card(data: new(movie: movie, country: country, watch: watch)).WithWatch(watch: watch);
    }

    /// <summary>
    /// Converts a Movie to a HomeCard component.
    /// </summary>
    public static ComponentEnvelope ToHomeCard(this Movie movie, string country)
    {
        return Component.HomeCard(data: new(movie: movie, country: country));
    }

    /// <summary>
    /// Converts a collection of Movies to Card components.
    /// </summary>
    public static IEnumerable<ComponentEnvelope> ToCards(
        this IEnumerable<Movie> movies,
        string country,
        bool watch = false
    )
    {
        return movies.Select(selector: m => m.ToCard(country: country, watch: watch));
    }

    #endregion

    #region TV Extensions

    /// <summary>
    /// Converts a Tv to a Card component.
    /// </summary>
    public static ComponentEnvelope ToCard(this Tv tv, string country, bool watch = false)
    {
        return Component.Card(data: new(tv: tv, country: country, watch: watch)).WithWatch(watch: watch);
    }

    /// <summary>
    /// Converts a Tv to a HomeCard component.
    /// </summary>
    public static ComponentEnvelope ToHomeCard(this Tv tv, string country)
    {
        return Component.HomeCard(data: new(tv: tv, country: country));
    }

    /// <summary>
    /// Converts a collection of Tv shows to Card components.
    /// </summary>
    public static IEnumerable<ComponentEnvelope> ToCards(
        this IEnumerable<Tv> shows,
        string country,
        bool watch = false
    )
    {
        return shows.Select(selector: t => t.ToCard(country: country, watch: watch));
    }

    #endregion

    #region Collection Extensions

    /// <summary>
    /// Converts a Collection to a Card component.
    /// </summary>
    public static ComponentEnvelope ToCard(
        this Collection collection,
        string country,
        bool watch = false
    )
    {
        return Component.Card(data: new(collection: collection, country: country, watch: watch)).WithWatch(watch: watch);
    }

    /// <summary>
    /// Converts a collection of Collections to Card components.
    /// </summary>
    public static IEnumerable<ComponentEnvelope> ToCards(
        this IEnumerable<Collection> collections,
        string country,
        bool watch = false
    )
    {
        return collections.Select(selector: c => c.ToCard(country: country, watch: watch));
    }

    #endregion

    #region Special Extensions

    /// <summary>
    /// Converts a Special to a Card component.
    /// </summary>
    public static ComponentEnvelope ToCard(this Special special, string country, bool watch = false)
    {
        return Component.Card(data: new(special: special, country: country, watch: watch)).WithWatch(watch: watch);
    }

    #endregion

    #region Genre Extensions

    /// <summary>
    /// Converts a Genre to a GenreCard component.
    /// </summary>
    public static ComponentEnvelope ToGenreCard(this Genre genre)
    {
        return Component.GenreCard(data: new(genre: genre));
    }

    /// <summary>
    /// Converts a MusicGenre to a GenreCard component.
    /// </summary>
    public static ComponentEnvelope ToGenreCard(this MusicGenre musicGenre)
    {
        return Component.GenreCard(data: new(genre: musicGenre));
    }

    #endregion

    #region Music Extensions

    /// <summary>
    /// Converts an Album to a MusicCard component.
    /// </summary>
    public static ComponentEnvelope ToMusicCard(this Album album)
    {
        return Component.MusicCard(data: new MusicCardData(album: album));
    }

    /// <summary>
    /// Converts an Artist to a MusicCard component.
    /// </summary>
    public static ComponentEnvelope ToMusicCard(this Artist artist)
    {
        return Component.MusicCard(data: new MusicCardData(artist: artist));
    }

    /// <summary>
    /// Converts a Track to a TrackRow component.
    /// </summary>
    public static ComponentEnvelope ToTrackRow(this Track track, bool isFavorite = false)
    {
        return Component.TrackRow(data: new(track: track, isFavorite: isFavorite));
    }

    /// <summary>
    /// Converts a collection of Tracks to TrackRow components.
    /// </summary>
    public static IEnumerable<ComponentEnvelope> ToTrackRows(
        this IEnumerable<Track> tracks,
        Func<Track, bool>? isFavorite = null
    )
    {
        return tracks.Select(selector: t => t.ToTrackRow(isFavorite: isFavorite?.Invoke(arg: t) ?? false));
    }

    #endregion

    #region Container Builders

    /// <summary>
    /// Wraps components in a Carousel container.
    /// </summary>
    public static ComponentEnvelope WrapInCarousel(
        this IEnumerable<ComponentEnvelope> items,
        string? title = null,
        string? moreLink = null,
        dynamic? id = null
    )
    {
        ContainerComponentBuilder builder = Component
            .Carousel()
            .WithTitle(title: title)
            .WithMoreLink(moreLink: moreLink)
            .WithItems(items: items);

        if (id != null)
            builder.WithId(id: id);

        return builder;
    }

    /// <summary>
    /// Wraps components in a Grid container.
    /// </summary>
    public static ComponentEnvelope WrapInGrid(
        this IEnumerable<ComponentEnvelope> items,
        string? title = null,
        string? moreLink = null,
        dynamic? id = null
    )
    {
        ContainerComponentBuilder builder = Component
            .Grid()
            .WithTitle(title: title)
            .WithMoreLink(moreLink: moreLink)
            .WithItems(items: items);

        if (id != null)
            builder.WithId(id: id);

        return builder;
    }

    /// <summary>
    /// Wraps components in a List container.
    /// </summary>
    public static ComponentEnvelope WrapInList(
        this IEnumerable<ComponentEnvelope> items,
        string? title = null,
        string? moreLink = null,
        dynamic? id = null
    )
    {
        ContainerComponentBuilder builder = Component
            .List()
            .WithTitle(title: title)
            .WithMoreLink(moreLink: moreLink)
            .WithItems(items: items);

        if (id != null)
            builder.WithId(id: id);

        return builder;
    }

    #endregion
}
