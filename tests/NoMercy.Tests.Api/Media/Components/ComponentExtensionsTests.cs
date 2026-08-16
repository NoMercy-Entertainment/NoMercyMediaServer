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

using NoMercy.Api.DTOs.Media.Components;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.TvShows;
using Xunit;

namespace NoMercy.Tests.Api.Media.Components;

[Trait("Category", "Unit")]
public class ComponentExtensionsTests
{
    private static Movie BuildMovie(int id = 1) =>
        new()
        {
            Id = id,
            Title = "Test Movie",
            TitleSort = "test movie",
            Overview = "A test movie.",
        };

    private static Tv BuildTv(int id = 1) =>
        new()
        {
            Id = id,
            Title = "Test Show",
            TitleSort = "test show",
            Overview = "A test show.",
        };

    private static Collection BuildCollection(int id = 1) =>
        new()
        {
            Id = id,
            Title = "Test Collection",
            TitleSort = "test collection",
        };

    private static Special BuildSpecial() => new() { Id = Ulid.NewUlid(), Title = "Test Special" };

    private static Genre BuildGenre() => new() { Id = 1, Name = "Action" };

    private static MusicGenre BuildMusicGenre() => new() { Id = Guid.NewGuid(), Name = "Rock" };

    private static Album BuildAlbum() =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = "Test Album",
            LibraryId = Ulid.NewUlid(),
        };

    private static Artist BuildArtist() =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = "Test Artist",
            LibraryId = Ulid.NewUlid(),
        };

    private static Track BuildTrack() =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = "Test Track",
            Folder = "/music/track",
            Filename = "track.flac",
            FolderId = Ulid.NewUlid(),
        };

    // =========================================================================
    // Regression: watch=true must produce a /watch link on CardData.Data.Link
    // for every ToCard extension. The bug: Component.Card(new(entity, country))
    // omitted watch from the CardData ctor call, so Link never got the /watch
    // suffix even though .WithWatch(watch) set the separate Props.Watch flag.
    // =========================================================================

    [Fact]
    public void Movie_ToCard_WatchTrue_LinkPointsAtWatchRoute()
    {
        Movie movie = BuildMovie(129);

        ComponentEnvelope envelope = movie.ToCard("US", watch: true);

        CardData data = ((LeafProps<CardData>)envelope.Props).Data!;
        data.Link.ToString().Should().Be("/movie/129/watch");
    }

    [Fact]
    public void Movie_ToCard_WatchFalse_LinkDoesNotPointAtWatchRoute()
    {
        Movie movie = BuildMovie(129);

        ComponentEnvelope envelope = movie.ToCard("US");

        CardData data = ((LeafProps<CardData>)envelope.Props).Data!;
        data.Link.ToString().Should().Be("/movie/129");
    }

    [Fact]
    public void Tv_ToCard_WatchTrue_LinkPointsAtWatchRoute()
    {
        Tv tv = BuildTv(1399);

        ComponentEnvelope envelope = tv.ToCard("US", watch: true);

        CardData data = ((LeafProps<CardData>)envelope.Props).Data!;
        data.Link.ToString().Should().Be("/tv/1399/watch");
    }

    [Fact]
    public void Collection_ToCard_WatchTrue_LinkPointsAtWatchRoute()
    {
        Collection collection = BuildCollection(42);

        ComponentEnvelope envelope = collection.ToCard("US", watch: true);

        CardData data = ((LeafProps<CardData>)envelope.Props).Data!;
        data.Link.ToString().Should().Be("/collection/42/watch");
    }

    [Fact]
    public void Special_ToCard_WatchTrue_LinkPointsAtWatchRoute()
    {
        Special special = BuildSpecial();

        ComponentEnvelope envelope = special.ToCard("US", watch: true);

        CardData data = ((LeafProps<CardData>)envelope.Props).Data!;
        data.Link.ToString().Should().Be($"/specials/{special.Id}/watch");
    }

    // =========================================================================
    // ToCard: the Props.Watch flag itself (separate from Link) is still set.
    // =========================================================================

    [Fact]
    public void Movie_ToCard_WatchTrue_AlsoSetsPropsWatchFlag()
    {
        Movie movie = BuildMovie();

        ComponentEnvelope envelope = movie.ToCard("US", watch: true);

        ((LeafProps<CardData>)envelope.Props).Watch.Should().BeTrue();
    }

    [Fact]
    public void Movie_ToCard_WatchFalse_PropsWatchFlagIsFalse()
    {
        Movie movie = BuildMovie();

        ComponentEnvelope envelope = movie.ToCard("US");

        ((LeafProps<CardData>)envelope.Props).Watch.Should().BeFalse();
    }

    // =========================================================================
    // ToCard: component type + data identity
    // =========================================================================

    [Fact]
    public void Movie_ToCard_UsesCardComponentType()
    {
        Movie movie = BuildMovie(7);

        ComponentEnvelope envelope = movie.ToCard("US");

        envelope.Component.Should().Be(ComponentTypes.Card);
        ((object)((LeafProps<CardData>)envelope.Props).Data!.Id!).Should().Be(7);
    }

    // =========================================================================
    // ToCards: collection extensions thread watch through to every item
    // =========================================================================

    [Fact]
    public void Movies_ToCards_WatchTrue_EveryCardLinksToWatchRoute()
    {
        List<Movie> movies = [BuildMovie(1), BuildMovie(2)];

        List<ComponentEnvelope> envelopes = [.. movies.ToCards("US", watch: true)];

        envelopes.Should().HaveCount(2);
        envelopes
            .Select(e => ((LeafProps<CardData>)e.Props).Data!.Link.ToString())
            .Should()
            .Equal("/movie/1/watch", "/movie/2/watch");
    }

    [Fact]
    public void Shows_ToCards_WatchTrue_EveryCardLinksToWatchRoute()
    {
        List<Tv> shows = [BuildTv(10), BuildTv(20)];

        List<ComponentEnvelope> envelopes = [.. shows.ToCards("US", watch: true)];

        envelopes
            .Select(e => ((LeafProps<CardData>)e.Props).Data!.Link.ToString())
            .Should()
            .Equal("/tv/10/watch", "/tv/20/watch");
    }

    [Fact]
    public void Collections_ToCards_WatchTrue_EveryCardLinksToWatchRoute()
    {
        List<Collection> collections = [BuildCollection(1), BuildCollection(2)];

        List<ComponentEnvelope> envelopes = [.. collections.ToCards("US", watch: true)];

        envelopes
            .Select(e => ((LeafProps<CardData>)e.Props).Data!.Link.ToString())
            .Should()
            .Equal("/collection/1/watch", "/collection/2/watch");
    }

    // =========================================================================
    // ToHomeCard
    // =========================================================================

    [Fact]
    public void Movie_ToHomeCard_UsesHomeCardComponentType()
    {
        Movie movie = BuildMovie(129);

        ComponentEnvelope envelope = movie.ToHomeCard("US");

        envelope.Component.Should().Be(ComponentTypes.HomeCard);
        ((object)((LeafProps<HomeCardData>)envelope.Props).Data!.Id!).Should().Be(129);
    }

    [Fact]
    public void Tv_ToHomeCard_UsesHomeCardComponentType()
    {
        Tv tv = BuildTv(1399);

        ComponentEnvelope envelope = tv.ToHomeCard("US");

        envelope.Component.Should().Be(ComponentTypes.HomeCard);
        ((object)((LeafProps<HomeCardData>)envelope.Props).Data!.Id!).Should().Be(1399);
    }

    // =========================================================================
    // ToGenreCard
    // =========================================================================

    [Fact]
    public void Genre_ToGenreCard_UsesGenreCardComponentType()
    {
        Genre genre = BuildGenre();

        ComponentEnvelope envelope = genre.ToGenreCard();

        envelope.Component.Should().Be(ComponentTypes.GenreCard);
    }

    [Fact]
    public void MusicGenre_ToGenreCard_UsesGenreCardComponentType()
    {
        MusicGenre musicGenre = BuildMusicGenre();

        ComponentEnvelope envelope = musicGenre.ToGenreCard();

        envelope.Component.Should().Be(ComponentTypes.GenreCard);
    }

    // =========================================================================
    // Music extensions
    // =========================================================================

    [Fact]
    public void Album_ToMusicCard_UsesMusicCardComponentType()
    {
        Album album = BuildAlbum();

        ComponentEnvelope envelope = album.ToMusicCard();

        envelope.Component.Should().Be(ComponentTypes.MusicCard);
        ((LeafProps<MusicCardData>)envelope.Props).Data!.Id.Should().Be(album.Id.ToString());
    }

    [Fact]
    public void Artist_ToMusicCard_UsesMusicCardComponentType()
    {
        Artist artist = BuildArtist();

        ComponentEnvelope envelope = artist.ToMusicCard();

        envelope.Component.Should().Be(ComponentTypes.MusicCard);
        ((LeafProps<MusicCardData>)envelope.Props).Data!.Id.Should().Be(artist.Id.ToString());
    }

    [Fact]
    public void Track_ToTrackRow_DefaultsToNotFavorite()
    {
        Track track = BuildTrack();

        ComponentEnvelope envelope = track.ToTrackRow();

        envelope.Component.Should().Be(ComponentTypes.TrackRow);
    }

    [Fact]
    public void Track_ToTrackRow_HonoursFavoriteFlag()
    {
        Track track = BuildTrack();

        ComponentEnvelope favoriteEnvelope = track.ToTrackRow(isFavorite: true);
        ComponentEnvelope notFavoriteEnvelope = track.ToTrackRow(isFavorite: false);

        favoriteEnvelope.Should().NotBeNull();
        notFavoriteEnvelope.Should().NotBeNull();
    }

    [Fact]
    public void Tracks_ToTrackRows_UsesPerTrackFavoritePredicate()
    {
        Track favoriteTrack = BuildTrack();
        Track otherTrack = BuildTrack();
        List<Track> tracks = [favoriteTrack, otherTrack];

        List<ComponentEnvelope> envelopes = [.. tracks.ToTrackRows(t => t == favoriteTrack)];

        envelopes.Should().HaveCount(2);
    }

    [Fact]
    public void Tracks_ToTrackRows_NullPredicate_DefaultsToNotFavorite()
    {
        List<Track> tracks = [BuildTrack(), BuildTrack()];

        List<ComponentEnvelope> envelopes = [.. tracks.ToTrackRows()];

        envelopes.Should().HaveCount(2);
    }

    // =========================================================================
    // Container builders: WrapInCarousel / WrapInGrid / WrapInList
    // =========================================================================

    [Fact]
    public void WrapInCarousel_SetsTitleMoreLinkItemsAndId()
    {
        List<ComponentEnvelope> items = [BuildMovie(1).ToCard("US"), BuildMovie(2).ToCard("US")];
        Ulid id = Ulid.NewUlid();

        ComponentEnvelope envelope = items.WrapInCarousel("Continue Watching", "/more", id);

        envelope.Component.Should().Be(ComponentTypes.Carousel);
        ContainerProps props = (ContainerProps)envelope.Props;
        props.Title.Should().Be("Continue Watching");
        props.MoreLink!.ToString().Should().Be("/more");
        props.Items.Should().HaveCount(2);
        ((Ulid)props.Id!).Should().Be(id);
    }

    [Fact]
    public void WrapInCarousel_NullId_LeavesDefaultId()
    {
        List<ComponentEnvelope> items = [BuildMovie(1).ToCard("US")];

        ComponentEnvelope envelope = items.WrapInCarousel();

        envelope.Component.Should().Be(ComponentTypes.Carousel);
    }

    [Fact]
    public void WrapInGrid_SetsTitleMoreLinkAndItems()
    {
        List<ComponentEnvelope> items = [BuildMovie(1).ToCard("US")];

        ComponentEnvelope envelope = items.WrapInGrid("All Movies", "/movies");

        envelope.Component.Should().Be(ComponentTypes.Grid);
        ContainerProps props = (ContainerProps)envelope.Props;
        props.Title.Should().Be("All Movies");
        props.MoreLink!.ToString().Should().Be("/movies");
        props.Items.Should().ContainSingle();
    }

    [Fact]
    public void WrapInList_SetsTitleMoreLinkAndItems()
    {
        List<ComponentEnvelope> items = [BuildTv(1).ToCard("US")];

        ComponentEnvelope envelope = items.WrapInList("Shows", "/shows");

        envelope.Component.Should().Be(ComponentTypes.List);
        ContainerProps props = (ContainerProps)envelope.Props;
        props.Title.Should().Be("Shows");
        props.MoreLink!.ToString().Should().Be("/shows");
        props.Items.Should().ContainSingle();
    }

    [Fact]
    public void WrapInList_NoTitleOrMoreLink_LeavesThemEmpty()
    {
        List<ComponentEnvelope> items = [BuildTv(1).ToCard("US")];

        ComponentEnvelope envelope = items.WrapInList();

        ContainerProps props = (ContainerProps)envelope.Props;
        props.Title.Should().BeEmpty();
        props.MoreLink.Should().BeNull();
    }
}
