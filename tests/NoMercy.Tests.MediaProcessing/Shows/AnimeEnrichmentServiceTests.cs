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

using FluentAssertions;
using Moq;
using NoMercy.MediaProcessing.Shows;
using NoMercy.Providers.AniList;
using NoMercy.Providers.AniList.Models;
using NoMercy.Providers.Jikan;
using NoMercy.Providers.Jikan.Models;
using Xunit;

namespace NoMercy.Tests.MediaProcessing.Shows;

public class AnimeEnrichmentServiceTests
{
    [Fact]
    public async Task EnrichTvAsync_NotClassifiedAnime_DoesNotCallProviders()
    {
        Mock<IMediaTypeClassifier> classifier = new();
        classifier
            .Setup(c => c.ClassifyAsync("Breaking Bad", 2008, It.IsAny<string[]?>()))
            .ReturnsAsync("tv");
        Mock<IAniListMetadataProvider> aniList = new();
        Mock<IJikanMetadataProvider> jikan = new();
        Mock<IShowRepository> showRepository = new();

        AnimeEnrichmentService service = new(
            classifier.Object,
            aniList.Object,
            jikan.Object,
            showRepository.Object,
            Mock.Of<NoMercy.MediaProcessing.Movies.IMovieRepository>()
        );

        await service.EnrichTvAsync(1, "Breaking Bad", 2008, ["US"]);

        showRepository.Verify(
            r =>
                r.StoreAnimeThemes(
                    It.IsAny<IEnumerable<NoMercy.Database.Models.TvShows.AnimeThemeTv>>()
                ),
            Times.Never
        );
        aniList.Verify(
            p => p.SearchAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<bool?>()),
            Times.Never
        );
        jikan.Verify(
            p => p.SearchAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<bool?>()),
            Times.Never
        );
    }

    [Fact]
    public async Task EnrichTvAsync_ClassifiedAnime_PersistsThemesFromAniListAndBackfillsFromJikan()
    {
        Mock<IMediaTypeClassifier> classifier = new();
        classifier
            .Setup(c => c.ClassifyAsync("One Piece", 1999, It.IsAny<string[]?>()))
            .ReturnsAsync("anime");

        Mock<IAniListMetadataProvider> aniList = new();
        aniList
            .Setup(p => p.SearchAsync("One Piece", 1999, It.IsAny<bool?>()))
            .ReturnsAsync(
                new AniListMedia
                {
                    Title = new() { Romaji = "One Piece" },
                    SeasonYear = 1999,
                    Season = "FALL",
                    Tags = [new() { Name = "Pirates", Category = "Setting-Universe" }],
                }
            );

        Mock<IJikanMetadataProvider> jikan = new();
        jikan
            .Setup(p => p.SearchAsync("One Piece", 1999, It.IsAny<bool?>()))
            .ReturnsAsync(
                new JikanAnime
                {
                    Titles = [new() { Type = "Default", Title = "One Piece" }],
                    Demographics = [new() { MalId = 27, Name = "Shounen" }],
                }
            );

        Mock<IShowRepository> showRepository = new();
        // "Pirates" has never been seen before, so the repository resolves
        // (creates) a real AnimeTheme row and hands back its database id —
        // this is the real resolve-or-create id, not a GetHashCode() stub.
        showRepository.Setup(r => r.ResolveAnimeThemeIdAsync("Pirates")).ReturnsAsync(501);
        // "Shounen" has never been seen before either, so the repository
        // resolves (creates) a real AnimeDemographic row and hands back its
        // database id — not the raw Jikan MalId (27), which is only stable
        // within MyAnimeList's own numbering.
        showRepository
            .Setup(r => r.ResolveAnimeDemographicIdAsync("Shounen"))
            .ReturnsAsync(701);

        AnimeEnrichmentService service = new(
            classifier.Object,
            aniList.Object,
            jikan.Object,
            showRepository.Object,
            Mock.Of<NoMercy.MediaProcessing.Movies.IMovieRepository>()
        );

        await service.EnrichTvAsync(42, "One Piece", 1999, ["JP"]);

        showRepository.Verify(r => r.ResolveAnimeThemeIdAsync("Pirates"), Times.Once);
        showRepository.Verify(r => r.ResolveAnimeDemographicIdAsync("Shounen"), Times.Once);
        showRepository.Verify(
            r =>
                r.StoreAnimeThemes(
                    It.Is<IEnumerable<NoMercy.Database.Models.TvShows.AnimeThemeTv>>(links =>
                        links.Any(l => l.TvId == 42 && l.AnimeThemeId == 501)
                    )
                ),
            Times.Once
        );
        showRepository.Verify(
            r =>
                r.StoreAnimeDemographics(
                    It.Is<IEnumerable<NoMercy.Database.Models.TvShows.AnimeDemographicTv>>(links =>
                        links.Any(l => l.TvId == 42 && l.AnimeDemographicId == 701)
                    )
                ),
            Times.Once
        );
        showRepository.Verify(r => r.StoreAnimeSeason(42, 1999, "FALL"), Times.Once);
    }

    // S4: AniList tags include non-theme categories (Cast-*, Technical, ...)
    // and adult-flagged tags; only Theme-*/Setting-* categories should reach
    // AnimeTheme, and IsAdult must always be excluded regardless of category.
    [Fact]
    public async Task EnrichTvAsync_FiltersOutNonThemeCategoriesAndAdultTags()
    {
        Mock<IMediaTypeClassifier> classifier = new();
        classifier
            .Setup(c => c.ClassifyAsync("Chainsaw Man", 2022, It.IsAny<string[]?>()))
            .ReturnsAsync("anime");

        Mock<IAniListMetadataProvider> aniList = new();
        aniList
            .Setup(p => p.SearchAsync("Chainsaw Man", 2022, It.IsAny<bool?>()))
            .ReturnsAsync(
                new AniListMedia
                {
                    Title = new() { Romaji = "Chainsaw Man" },
                    Tags =
                    [
                        new() { Name = "Gore", Category = "Setting-Universe" },
                        new() { Name = "Denji", Category = "Cast-Main Cast" },
                        new() { Name = "CGI", Category = "Technical" },
                        new()
                        {
                            Name = "Sexual Content",
                            Category = "Sexual Content",
                            IsAdult = true,
                        },
                        new()
                        {
                            Name = "Not Actually Adult But Flagged",
                            Category = "Theme-Action",
                            IsAdult = true,
                        },
                    ],
                }
            );

        Mock<IJikanMetadataProvider> jikan = new();
        jikan
            .Setup(p => p.SearchAsync("Chainsaw Man", 2022, It.IsAny<bool?>()))
            .ReturnsAsync(
                new JikanAnime { Titles = [new() { Type = "Default", Title = "Chainsaw Man" }] }
            );

        Mock<IShowRepository> showRepository = new();
        showRepository.Setup(r => r.ResolveAnimeThemeIdAsync("Gore")).ReturnsAsync(801);

        AnimeEnrichmentService service = new(
            classifier.Object,
            aniList.Object,
            jikan.Object,
            showRepository.Object,
            Mock.Of<NoMercy.MediaProcessing.Movies.IMovieRepository>()
        );

        await service.EnrichTvAsync(99, "Chainsaw Man", 2022, ["JP"]);

        showRepository.Verify(r => r.ResolveAnimeThemeIdAsync("Gore"), Times.Once);
        showRepository.Verify(r => r.ResolveAnimeThemeIdAsync("Denji"), Times.Never);
        showRepository.Verify(r => r.ResolveAnimeThemeIdAsync("CGI"), Times.Never);
        showRepository.Verify(r => r.ResolveAnimeThemeIdAsync("Sexual Content"), Times.Never);
        showRepository.Verify(
            r => r.ResolveAnimeThemeIdAsync("Not Actually Adult But Flagged"),
            Times.Never
        );
        showRepository.Verify(
            r =>
                r.StoreAnimeThemes(
                    It.Is<IEnumerable<NoMercy.Database.Models.TvShows.AnimeThemeTv>>(links =>
                        links.Count() == 1 && links.Any(l => l.TvId == 99 && l.AnimeThemeId == 801)
                    )
                ),
            Times.Once
        );
    }

    // S5: when AniList has no match at all, Jikan's Themes field should be
    // used as a fallback theme source, and a season row should still be
    // written from Jikan's Year with the "UNKNOWN" quarter sentinel since
    // Jikan carries no quarter concept and AnimeSeason.Quarter is non-nullable.
    [Fact]
    public async Task EnrichTvAsync_NoAniListMatch_FallsBackToJikanThemesAndYearOnlySeason()
    {
        Mock<IMediaTypeClassifier> classifier = new();
        classifier
            .Setup(c => c.ClassifyAsync("Obscure Anime", 2015, It.IsAny<string[]?>()))
            .ReturnsAsync("anime");

        Mock<IAniListMetadataProvider> aniList = new();
        aniList
            .Setup(p => p.SearchAsync("Obscure Anime", 2015, It.IsAny<bool?>()))
            .ReturnsAsync((AniListMedia?)null);

        Mock<IJikanMetadataProvider> jikan = new();
        jikan
            .Setup(p => p.SearchAsync("Obscure Anime", 2015, It.IsAny<bool?>()))
            .ReturnsAsync(
                new JikanAnime
                {
                    Titles = [new() { Type = "Default", Title = "Obscure Anime" }],
                    Themes = [new() { MalId = 12, Name = "Mecha" }],
                    Year = 2015,
                }
            );

        Mock<IShowRepository> showRepository = new();
        showRepository.Setup(r => r.ResolveAnimeThemeIdAsync("Mecha")).ReturnsAsync(901);

        AnimeEnrichmentService service = new(
            classifier.Object,
            aniList.Object,
            jikan.Object,
            showRepository.Object,
            Mock.Of<NoMercy.MediaProcessing.Movies.IMovieRepository>()
        );

        await service.EnrichTvAsync(77, "Obscure Anime", 2015, ["JP"]);

        showRepository.Verify(r => r.ResolveAnimeThemeIdAsync("Mecha"), Times.Once);
        showRepository.Verify(
            r =>
                r.StoreAnimeThemes(
                    It.Is<IEnumerable<NoMercy.Database.Models.TvShows.AnimeThemeTv>>(links =>
                        links.Any(l => l.TvId == 77 && l.AnimeThemeId == 901)
                    )
                ),
            Times.Once
        );
        showRepository.Verify(r => r.StoreAnimeSeason(77, 2015, "UNKNOWN"), Times.Once);
    }

    // Themes and demographics already stored: nothing is missing, so neither
    // provider should be called at all - re-running enrichment on an
    // already-complete title must not spend AniList/Jikan quota for no reason.
    [Fact]
    public async Task EnrichTvAsync_ThemesAndDemographicsAlreadyStored_CallsNoProviders()
    {
        Mock<IMediaTypeClassifier> classifier = new();
        classifier
            .Setup(c => c.ClassifyAsync("One Piece", 1999, It.IsAny<string[]?>()))
            .ReturnsAsync("anime");

        Mock<IShowRepository> showRepository = new();
        showRepository.Setup(r => r.HasAnimeThemesAsync(42)).ReturnsAsync(true);
        showRepository.Setup(r => r.HasAnimeDemographicsAsync(42)).ReturnsAsync(true);

        Mock<IAniListMetadataProvider> aniList = new();
        Mock<IJikanMetadataProvider> jikan = new();

        AnimeEnrichmentService service = new(
            classifier.Object,
            aniList.Object,
            jikan.Object,
            showRepository.Object,
            Mock.Of<NoMercy.MediaProcessing.Movies.IMovieRepository>()
        );

        await service.EnrichTvAsync(42, "One Piece", 1999, ["JP"]);

        aniList.Verify(
            p => p.SearchAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<bool?>()),
            Times.Never
        );
        jikan.Verify(
            p => p.SearchAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<bool?>()),
            Times.Never
        );
    }

    // Themes already stored, demographics still missing: AniList already gave
    // everything it has, so only Jikan should be queried, and only to fill in
    // the missing demographic - not to re-derive themes/season it already has.
    [Fact]
    public async Task EnrichTvAsync_ThemesStoredDemographicsMissing_QueriesJikanOnlyForDemographics()
    {
        Mock<IMediaTypeClassifier> classifier = new();
        classifier
            .Setup(c => c.ClassifyAsync("One Piece", 1999, It.IsAny<string[]?>()))
            .ReturnsAsync("anime");

        Mock<IShowRepository> showRepository = new();
        showRepository.Setup(r => r.HasAnimeThemesAsync(42)).ReturnsAsync(true);
        showRepository.Setup(r => r.HasAnimeDemographicsAsync(42)).ReturnsAsync(false);
        showRepository.Setup(r => r.ResolveAnimeDemographicIdAsync("Shounen")).ReturnsAsync(701);

        Mock<IAniListMetadataProvider> aniList = new();

        Mock<IJikanMetadataProvider> jikan = new();
        jikan
            .Setup(p => p.SearchAsync("One Piece", 1999, It.IsAny<bool?>()))
            .ReturnsAsync(
                new JikanAnime
                {
                    Titles = [new() { Type = "Default", Title = "One Piece" }],
                    Demographics = [new() { MalId = 27, Name = "Shounen" }],
                }
            );

        AnimeEnrichmentService service = new(
            classifier.Object,
            aniList.Object,
            jikan.Object,
            showRepository.Object,
            Mock.Of<NoMercy.MediaProcessing.Movies.IMovieRepository>()
        );

        await service.EnrichTvAsync(42, "One Piece", 1999, ["JP"]);

        aniList.Verify(
            p => p.SearchAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<bool?>()),
            Times.Never
        );
        jikan.Verify(p => p.SearchAsync("One Piece", 1999, It.IsAny<bool?>()), Times.Once);
        showRepository.Verify(
            r =>
                r.StoreAnimeThemes(
                    It.IsAny<IEnumerable<NoMercy.Database.Models.TvShows.AnimeThemeTv>>()
                ),
            Times.Never
        );
        showRepository.Verify(
            r =>
                r.StoreAnimeDemographics(
                    It.Is<IEnumerable<NoMercy.Database.Models.TvShows.AnimeDemographicTv>>(links =>
                        links.Any(l => l.TvId == 42 && l.AnimeDemographicId == 701)
                    )
                ),
            Times.Once
        );
    }
}
