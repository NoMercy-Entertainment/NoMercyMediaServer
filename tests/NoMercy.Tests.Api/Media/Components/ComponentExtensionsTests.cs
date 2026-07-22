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

[Trait(name: "Category", value: "Unit")]
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
        Movie movie = BuildMovie(id: 129);

        ComponentEnvelope envelope = movie.ToCard(country: "US", watch: true);

        CardData data = ((LeafProps<CardData>)envelope.Props).Data!;
        data.Link.ToString().Should().Be(expected: "/movie/129/watch");
    }

    [Fact]
    public void Movie_ToCard_WatchFalse_LinkDoesNotPointAtWatchRoute()
    {
        Movie movie = BuildMovie(id: 129);

        ComponentEnvelope envelope = movie.ToCard(country: "US");

        CardData data = ((LeafProps<CardData>)envelope.Props).Data!;
        data.Link.ToString().Should().Be(expected: "/movie/129");
    }

    [Fact]
    public void Tv_ToCard_WatchTrue_LinkPointsAtWatchRoute()
    {
        Tv tv = BuildTv(id: 1399);

        ComponentEnvelope envelope = tv.ToCard(country: "US", watch: true);

        CardData data = ((LeafProps<CardData>)envelope.Props).Data!;
        data.Link.ToString().Should().Be(expected: "/tv/1399/watch");
    }

    [Fact]
    public void Collection_ToCard_WatchTrue_LinkPointsAtWatchRoute()
    {
        Collection collection = BuildCollection(id: 42);

        ComponentEnvelope envelope = collection.ToCard(country: "US", watch: true);

        CardData data = ((LeafProps<CardData>)envelope.Props).Data!;
        data.Link.ToString().Should().Be(expected: "/collection/42/watch");
    }

    [Fact]
    public void Special_ToCard_WatchTrue_LinkPointsAtWatchRoute()
    {
        Special special = BuildSpecial();

        ComponentEnvelope envelope = special.ToCard(country: "US", watch: true);

        CardData data = ((LeafProps<CardData>)envelope.Props).Data!;
        data.Link.ToString().Should().Be(expected: $"/specials/{special.Id}/watch");
    }

    // =========================================================================
    // ToCard: the Props.Watch flag itself (separate from Link) is still set.
    // =========================================================================

    [Fact]
    public void Movie_ToCard_WatchTrue_AlsoSetsPropsWatchFlag()
    {
        Movie movie = BuildMovie();

        ComponentEnvelope envelope = movie.ToCard(country: "US", watch: true);

        ((LeafProps<CardData>)envelope.Props).Watch.Should().BeTrue();
    }

    [Fact]
    public void Movie_ToCard_WatchFalse_PropsWatchFlagIsFalse()
    {
        Movie movie = BuildMovie();

        ComponentEnvelope envelope = movie.ToCard(country: "US");

        ((LeafProps<CardData>)envelope.Props).Watch.Should().BeFalse();
    }

    // =========================================================================
    // ToCard: component type + data identity
    // =========================================================================

    [Fact]
    public void Movie_ToCard_UsesCardComponentType()
    {
        Movie movie = BuildMovie(id: 7);

        ComponentEnvelope envelope = movie.ToCard(country: "US");

        envelope.Component.Should().Be(expected: ComponentTypes.Card);
        ((object)((LeafProps<CardData>)envelope.Props).Data!.Id!).Should().Be(expected: 7);
    }

    // =========================================================================
    // ToCards: collection extensions thread watch through to every item
    // =========================================================================

    [Fact]
    public void Movies_ToCards_WatchTrue_EveryCardLinksToWatchRoute()
    {
        List<Movie> movies = [BuildMovie(id: 1), BuildMovie(id: 2)];

        List<ComponentEnvelope> envelopes = movies.ToCards(country: "US", watch: true).ToList();

        envelopes.Should().HaveCount(expected: 2);
        envelopes
            .Select(selector: e => ((LeafProps<CardData>)e.Props).Data!.Link.ToString())
            .Should()
            .Equal(expected: ["/movie/1/watch", "/movie/2/watch"]);
    }

    [Fact]
    public void Shows_ToCards_WatchTrue_EveryCardLinksToWatchRoute()
    {
        List<Tv> shows = [BuildTv(id: 10), BuildTv(id: 20)];

        List<ComponentEnvelope> envelopes = shows.ToCards(country: "US", watch: true).ToList();

        envelopes
            .Select(selector: e => ((LeafProps<CardData>)e.Props).Data!.Link.ToString())
            .Should()
            .Equal(expected: ["/tv/10/watch", "/tv/20/watch"]);
    }

    [Fact]
    public void Collections_ToCards_WatchTrue_EveryCardLinksToWatchRoute()
    {
        List<Collection> collections = [BuildCollection(id: 1), BuildCollection(id: 2)];

        List<ComponentEnvelope> envelopes = collections.ToCards(country: "US", watch: true).ToList();

        envelopes
            .Select(selector: e => ((LeafProps<CardData>)e.Props).Data!.Link.ToString())
            .Should()
            .Equal(expected: ["/collection/1/watch", "/collection/2/watch"]);
    }

    // =========================================================================
    // ToHomeCard
    // =========================================================================

    [Fact]
    public void Movie_ToHomeCard_UsesHomeCardComponentType()
    {
        Movie movie = BuildMovie(id: 129);

        ComponentEnvelope envelope = movie.ToHomeCard(country: "US");

        envelope.Component.Should().Be(expected: ComponentTypes.HomeCard);
        ((object)((LeafProps<HomeCardData>)envelope.Props).Data!.Id!).Should().Be(expected: 129);
    }

    [Fact]
    public void Tv_ToHomeCard_UsesHomeCardComponentType()
    {
        Tv tv = BuildTv(id: 1399);

        ComponentEnvelope envelope = tv.ToHomeCard(country: "US");

        envelope.Component.Should().Be(expected: ComponentTypes.HomeCard);
        ((object)((LeafProps<HomeCardData>)envelope.Props).Data!.Id!).Should().Be(expected: 1399);
    }

    // =========================================================================
    // ToGenreCard
    // =========================================================================

    [Fact]
    public void Genre_ToGenreCard_UsesGenreCardComponentType()
    {
        Genre genre = BuildGenre();

        ComponentEnvelope envelope = genre.ToGenreCard();

        envelope.Component.Should().Be(expected: ComponentTypes.GenreCard);
    }

    [Fact]
    public void MusicGenre_ToGenreCard_UsesGenreCardComponentType()
    {
        MusicGenre musicGenre = BuildMusicGenre();

        ComponentEnvelope envelope = musicGenre.ToGenreCard();

        envelope.Component.Should().Be(expected: ComponentTypes.GenreCard);
    }

    // =========================================================================
    // Music extensions
    // =========================================================================

    [Fact]
    public void Album_ToMusicCard_UsesMusicCardComponentType()
    {
        Album album = BuildAlbum();

        ComponentEnvelope envelope = album.ToMusicCard();

        envelope.Component.Should().Be(expected: ComponentTypes.MusicCard);
        ((LeafProps<MusicCardData>)envelope.Props).Data!.Id.Should().Be(expected: album.Id.ToString());
    }

    [Fact]
    public void Artist_ToMusicCard_UsesMusicCardComponentType()
    {
        Artist artist = BuildArtist();

        ComponentEnvelope envelope = artist.ToMusicCard();

        envelope.Component.Should().Be(expected: ComponentTypes.MusicCard);
        ((LeafProps<MusicCardData>)envelope.Props).Data!.Id.Should().Be(expected: artist.Id.ToString());
    }

    [Fact]
    public void Track_ToTrackRow_DefaultsToNotFavorite()
    {
        Track track = BuildTrack();

        ComponentEnvelope envelope = track.ToTrackRow();

        envelope.Component.Should().Be(expected: ComponentTypes.TrackRow);
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

        List<ComponentEnvelope> envelopes = tracks.ToTrackRows(isFavorite: t => t == favoriteTrack).ToList();

        envelopes.Should().HaveCount(expected: 2);
    }

    [Fact]
    public void Tracks_ToTrackRows_NullPredicate_DefaultsToNotFavorite()
    {
        List<Track> tracks = [BuildTrack(), BuildTrack()];

        List<ComponentEnvelope> envelopes = tracks.ToTrackRows().ToList();

        envelopes.Should().HaveCount(expected: 2);
    }

    // =========================================================================
    // Container builders: WrapInCarousel / WrapInGrid / WrapInList
    // =========================================================================

    [Fact]
    public void WrapInCarousel_SetsTitleMoreLinkItemsAndId()
    {
        List<ComponentEnvelope> items = [BuildMovie(id: 1).ToCard(country: "US"), BuildMovie(id: 2).ToCard(country: "US")];
        Ulid id = Ulid.NewUlid();

        ComponentEnvelope envelope = items.WrapInCarousel(title: "Continue Watching", moreLink: "/more", id: id);

        envelope.Component.Should().Be(expected: ComponentTypes.Carousel);
        ContainerProps props = (ContainerProps)envelope.Props;
        props.Title.Should().Be(expected: "Continue Watching");
        props.MoreLink!.ToString().Should().Be(expected: "/more");
        props.Items.Should().HaveCount(expected: 2);
        ((Ulid)props.Id!).Should().Be(expected: id);
    }

    [Fact]
    public void WrapInCarousel_NullId_LeavesDefaultId()
    {
        List<ComponentEnvelope> items = [BuildMovie(id: 1).ToCard(country: "US")];

        ComponentEnvelope envelope = items.WrapInCarousel();

        envelope.Component.Should().Be(expected: ComponentTypes.Carousel);
    }

    [Fact]
    public void WrapInGrid_SetsTitleMoreLinkAndItems()
    {
        List<ComponentEnvelope> items = [BuildMovie(id: 1).ToCard(country: "US")];

        ComponentEnvelope envelope = items.WrapInGrid(title: "All Movies", moreLink: "/movies");

        envelope.Component.Should().Be(expected: ComponentTypes.Grid);
        ContainerProps props = (ContainerProps)envelope.Props;
        props.Title.Should().Be(expected: "All Movies");
        props.MoreLink!.ToString().Should().Be(expected: "/movies");
        props.Items.Should().ContainSingle();
    }

    [Fact]
    public void WrapInList_SetsTitleMoreLinkAndItems()
    {
        List<ComponentEnvelope> items = [BuildTv(id: 1).ToCard(country: "US")];

        ComponentEnvelope envelope = items.WrapInList(title: "Shows", moreLink: "/shows");

        envelope.Component.Should().Be(expected: ComponentTypes.List);
        ContainerProps props = (ContainerProps)envelope.Props;
        props.Title.Should().Be(expected: "Shows");
        props.MoreLink!.ToString().Should().Be(expected: "/shows");
        props.Items.Should().ContainSingle();
    }

    [Fact]
    public void WrapInList_NoTitleOrMoreLink_LeavesThemEmpty()
    {
        List<ComponentEnvelope> items = [BuildTv(id: 1).ToCard(country: "US")];

        ComponentEnvelope envelope = items.WrapInList();

        ContainerProps props = (ContainerProps)envelope.Props;
        props.Title.Should().BeEmpty();
        props.MoreLink.Should().BeNull();
    }
}
