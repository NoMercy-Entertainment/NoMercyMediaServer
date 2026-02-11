using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;

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
        return Component.Card(new(movie, country))
            .WithWatch(watch);
    }

    /// <summary>
    /// Converts a Movie to a HomeCard component.
    /// </summary>
    public static ComponentEnvelope ToHomeCard(this Movie movie, string country)
    {
        return Component.HomeCard(new(movie, country));
    }

    /// <summary>
    /// Converts a collection of Movies to Card components.
    /// </summary>
    public static IEnumerable<ComponentEnvelope> ToCards(this IEnumerable<Movie> movies, string country, bool watch = false)
    {
        return movies.Select(m => m.ToCard(country, watch));
    }

    #endregion

    #region TV Extensions

    /// <summary>
    /// Converts a Tv to a Card component.
    /// </summary>
    public static ComponentEnvelope ToCard(this Tv tv, string country, bool watch = false)
    {
        return Component.Card(new(tv, country))
            .WithWatch(watch);
    }

    /// <summary>
    /// Converts a Tv to a HomeCard component.
    /// </summary>
    public static ComponentEnvelope ToHomeCard(this Tv tv, string country)
    {
        return Component.HomeCard(new(tv, country));
    }

    /// <summary>
    /// Converts a collection of Tv shows to Card components.
    /// </summary>
    public static IEnumerable<ComponentEnvelope> ToCards(this IEnumerable<Tv> shows, string country, bool watch = false)
    {
        return shows.Select(t => t.ToCard(country, watch));
    }

    #endregion

    #region Collection Extensions

    /// <summary>
    /// Converts a Collection to a Card component.
    /// </summary>
    public static ComponentEnvelope ToCard(this Collection collection, string country, bool watch = false)
    {
        return Component.Card(new(collection, country))
            .WithWatch(watch);
    }

    /// <summary>
    /// Converts a collection of Collections to Card components.
    /// </summary>
    public static IEnumerable<ComponentEnvelope> ToCards(this IEnumerable<Collection> collections, string country, bool watch = false)
    {
        return collections.Select(c => c.ToCard(country, watch));
    }

    #endregion

    #region Special Extensions

    /// <summary>
    /// Converts a Special to a Card component.
    /// </summary>
    public static ComponentEnvelope ToCard(this Special special, string country, bool watch = false)
    {
        return Component.Card(new(special, country))
            .WithWatch(watch);
    }

    #endregion

    #region Genre Extensions

    /// <summary>
    /// Converts a Genre to a GenreCard component.
    /// </summary>
    public static ComponentEnvelope ToGenreCard(this Genre genre)
    {
        return Component.GenreCard(new(genre));
    }

    /// <summary>
    /// Converts a MusicGenre to a GenreCard component.
    /// </summary>
    public static ComponentEnvelope ToGenreCard(this MusicGenre musicGenre)
    {
        return Component.GenreCard(new(musicGenre));
    }

    #endregion

    #region Music Extensions

    /// <summary>
    /// Converts an Album to a MusicCard component.
    /// </summary>
    public static ComponentEnvelope ToMusicCard(this Album album)
    {
        return Component.MusicCard(new MusicCardData(album));
    }

    /// <summary>
    /// Converts an Artist to a MusicCard component.
    /// </summary>
    public static ComponentEnvelope ToMusicCard(this Artist artist)
    {
        return Component.MusicCard(new MusicCardData(artist));
    }

    /// <summary>
    /// Converts a Track to a TrackRow component.
    /// </summary>
    public static ComponentEnvelope ToTrackRow(this Track track, bool isFavorite = false)
    {
        return Component.TrackRow(new(track, isFavorite));
    }

    /// <summary>
    /// Converts a collection of Tracks to TrackRow components.
    /// </summary>
    public static IEnumerable<ComponentEnvelope> ToTrackRows(this IEnumerable<Track> tracks, Func<Track, bool>? isFavorite = null)
    {
        return tracks.Select(t => t.ToTrackRow(isFavorite?.Invoke(t) ?? false));
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
        dynamic? id = null)
    {
        ContainerComponentBuilder builder = Component.Carousel()
            .WithTitle(title)
            .WithMoreLink(moreLink)
            .WithItems(items);

        if (id != null)
            builder.WithId(id);

        return builder;
    }

    /// <summary>
    /// Wraps components in a Grid container.
    /// </summary>
    public static ComponentEnvelope WrapInGrid(
        this IEnumerable<ComponentEnvelope> items,
        string? title = null,
        string? moreLink = null,
        dynamic? id = null)
    {
        ContainerComponentBuilder builder = Component.Grid()
            .WithTitle(title)
            .WithMoreLink(moreLink)
            .WithItems(items);

        if (id != null)
            builder.WithId(id);

        return builder;
    }

    /// <summary>
    /// Wraps components in a List container.
    /// </summary>
    public static ComponentEnvelope WrapInList(
        this IEnumerable<ComponentEnvelope> items,
        string? title = null,
        string? moreLink = null,
        dynamic? id = null)
    {
        ContainerComponentBuilder builder = Component.List()
            .WithTitle(title)
            .WithMoreLink(moreLink)
            .WithItems(items);

        if (id != null)
            builder.WithId(id);

        return builder;
    }

    #endregion
}
