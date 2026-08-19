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

        AnimeEnrichmentService service = new(
            classifier.Object,
            aniList.Object,
            jikan.Object,
            showRepository.Object,
            Mock.Of<NoMercy.MediaProcessing.Movies.IMovieRepository>()
        );

        await service.EnrichTvAsync(42, "One Piece", 1999, ["JP"]);

        showRepository.Verify(r => r.ResolveAnimeThemeIdAsync("Pirates"), Times.Once);
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
                        links.Any(l => l.TvId == 42 && l.AnimeDemographicId == 27)
                    )
                ),
            Times.Once
        );
        showRepository.Verify(r => r.StoreAnimeSeason(42, 1999, "FALL"), Times.Once);
    }
}
