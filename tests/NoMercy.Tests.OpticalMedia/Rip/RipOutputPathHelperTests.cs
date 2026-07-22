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

using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Metadata;
using NoMercy.OpticalMedia.Rip;
using NoMercy.OpticalMedia.Sources;

namespace NoMercy.Tests.OpticalMedia.Rip;

/// <summary>
/// REQUIREMENT: <see cref="RipOutputPathHelper.Build"/> must derive the
/// folder-relative rip destination the folder-watcher import pipeline can
/// match against TMDB — movies get <c>{Title} ({Year})/...</c>, TV/anime get
/// a per-season folder with <c>S{SS}E{EE}</c> naming (episode number offset
/// by <c>batchIndex</c>), unknown library types and missing metadata always
/// fall back to <c>disc-rips/title_NN.mkv</c>, and titles are sanitized for
/// filesystem-invalid characters.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class RipOutputPathHelperTests
{
    private static RipRequest MakeRequest(CustomMetadata? custom) =>
        new(
            DrivePath: "D:\\",
            SelectedTitleIndices: [1],
            MetadataId: null,
            Custom: custom,
            LibraryId: Ulid.NewUlid(),
            FolderId: Ulid.NewUlid(),
            EncodingProfileId: null,
            AudioTracks: [],
            Subtitles: []
        );

    [Fact]
    public void Build_NoCustomMetadata_FallsBackToDiscRipsTitleIndex()
    {
        string path = RipOutputPathHelper.Build(request: MakeRequest(custom: null), libraryType: "movie", titleIndex: 3, batchIndex: 0);

        path.Should().Be(expected: "disc-rips/title_03.mkv");
    }

    [Fact]
    public void Build_WhitespaceOnlyTitle_FallsBackToDiscRipsTitleIndex()
    {
        CustomMetadata custom = new(Title: "   ", Year: 2024, Type: MediaType.Movie, PosterUrl: null);

        string path = RipOutputPathHelper.Build(request: MakeRequest(custom: custom), libraryType: "movie", titleIndex: 5, batchIndex: 0);

        path.Should().Be(expected: "disc-rips/title_05.mkv");
    }

    [Fact]
    public void Build_UnknownLibraryType_FallsBackToDiscRipsTitleIndex()
    {
        CustomMetadata custom = new(Title: "Some Movie", Year: 2024, Type: MediaType.Movie, PosterUrl: null);

        string path = RipOutputPathHelper.Build(request: MakeRequest(custom: custom), libraryType: "music", titleIndex: 7, batchIndex: 0);

        path.Should().Be(expected: "disc-rips/title_07.mkv");
    }

    [Theory]
    [InlineData(data: ["movie", 0, "Inception (2010)/Inception (2010).mkv"])]
    [InlineData(data: ["movie", 1, "Inception (2010)/Inception (2010) - Disc 2.mkv"])]
    [InlineData(data: ["movie", 2, "Inception (2010)/Inception (2010) - Disc 3.mkv"])]
    public void Build_MovieLibraryType_BuildsShowRootAndDiscSuffix(
        string libraryType,
        int batchIndex,
        string expected
    )
    {
        CustomMetadata custom = new(Title: "Inception", Year: 2010, Type: MediaType.Movie, PosterUrl: null);

        string path = RipOutputPathHelper.Build(
            request: MakeRequest(custom: custom),
            libraryType: libraryType,
            titleIndex: 1,
            batchIndex: batchIndex
        );

        path.Should().Be(expected: expected);
    }

    [Fact]
    public void Build_MovieWithoutYear_OmitsYearSuffix()
    {
        CustomMetadata custom = new(Title: "Untitled", Year: null, Type: MediaType.Movie, PosterUrl: null);

        string path = RipOutputPathHelper.Build(request: MakeRequest(custom: custom), libraryType: "movie", titleIndex: 1, batchIndex: 0);

        path.Should().Be(expected: "Untitled/Untitled.mkv");
    }

    [Theory]
    [InlineData(data: "tv")]
    [InlineData(data: "anime")]
    public void Build_TvOrAnimeLibraryType_BuildsSeasonFolderAndSxxExx(string libraryType)
    {
        CustomMetadata custom = new(
            Title: "Breaking Bad",
            Year: 2008,
            Type: MediaType.TvShow,
            PosterUrl: null,
            SeasonNumber: 2,
            EpisodeStartNumber: 5
        );

        string path = RipOutputPathHelper.Build(
            request: MakeRequest(custom: custom),
            libraryType: libraryType,
            titleIndex: 1,
            batchIndex: 0
        );

        path.Should().Be(expected: "Breaking Bad (2008)/Season 02/Breaking Bad S02E05.mkv");
    }

    [Fact]
    public void Build_TvShow_BatchIndexOffsetsEpisodeNumber()
    {
        CustomMetadata custom = new(
            Title: "Show",
            Year: 2020,
            Type: MediaType.TvShow,
            PosterUrl: null,
            SeasonNumber: 1,
            EpisodeStartNumber: 1
        );

        string path = RipOutputPathHelper.Build(
            request: MakeRequest(custom: custom),
            libraryType: "tv",
            titleIndex: 1,
            batchIndex: 3
        );

        path.Should().Be(expected: "Show (2020)/Season 01/Show S01E04.mkv");
    }

    [Fact]
    public void Build_TvShow_NoSeasonNumber_DefaultsToSeasonOne()
    {
        CustomMetadata custom = new(Title: "Show", Year: 2020, Type: MediaType.TvShow, PosterUrl: null, SeasonNumber: null);

        string path = RipOutputPathHelper.Build(request: MakeRequest(custom: custom), libraryType: "tv", titleIndex: 1, batchIndex: 0);

        path.Should().Contain(expected: "Season 01");
    }

    [Fact]
    public void Build_TvShow_NoEpisodeStartNumber_DefaultsToEpisodeOne()
    {
        CustomMetadata custom = new(
            Title: "Show",
            Year: 2020,
            Type: MediaType.TvShow,
            PosterUrl: null,
            SeasonNumber: 1,
            EpisodeStartNumber: null
        );

        string path = RipOutputPathHelper.Build(request: MakeRequest(custom: custom), libraryType: "tv", titleIndex: 1, batchIndex: 0);

        path.Should().Contain(expected: "S01E01");
    }

    [Theory]
    [InlineData(data: ["Rocky: A Story?", "Rocky A Story"])]
    [InlineData(data: ["Colon:Test", "Colon Test"])]
    [InlineData(data: ["Slash/Back\\Slash", "Slash Back Slash"])]
    [InlineData(data: ["Pipe|Question?Star*", "Pipe Question Star"])]
    [InlineData(data: ["Quote\"Angle<>Bracket", "Quote Angle Bracket"])]
    public void Build_SanitizesFilesystemInvalidCharsFromTitle(string rawTitle, string sanitized)
    {
        CustomMetadata custom = new(Title: rawTitle, Year: 2020, Type: MediaType.Movie, PosterUrl: null);

        string path = RipOutputPathHelper.Build(request: MakeRequest(custom: custom), libraryType: "movie", titleIndex: 1, batchIndex: 0);

        path.Should().Be(expected: $"{sanitized} (2020)/{sanitized} (2020).mkv");
    }

    [Fact]
    public void Build_CollapsesMultipleWhitespaceRunsToSingleSpace()
    {
        CustomMetadata custom = new(Title: "Too    Many     Spaces", Year: 2020, Type: MediaType.Movie, PosterUrl: null);

        string path = RipOutputPathHelper.Build(request: MakeRequest(custom: custom), libraryType: "movie", titleIndex: 1, batchIndex: 0);

        path.Should().Be(expected: "Too Many Spaces (2020)/Too Many Spaces (2020).mkv");
    }

    [Fact]
    public void Build_TrimsSanitizedTitle()
    {
        CustomMetadata custom = new(Title: "  Padded Title  ", Year: 2020, Type: MediaType.Movie, PosterUrl: null);

        string path = RipOutputPathHelper.Build(request: MakeRequest(custom: custom), libraryType: "movie", titleIndex: 1, batchIndex: 0);

        path.Should().Be(expected: "Padded Title (2020)/Padded Title (2020).mkv");
    }
}
