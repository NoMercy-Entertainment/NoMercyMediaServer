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

using NoMercy.Api.DTOs.Media;
using NoMercy.Database.Models.Media;
using NoMercy.Providers.TMDB.Models.Shared;
using Xunit;

namespace NoMercy.Tests.Api.Media;

/// <summary>
/// Regression coverage for the HashToId nondeterminism bug: the surrogate id
/// minted for a TMDB image with no primary key used to derive from
/// string.GetHashCode(), which .NET randomizes per process (DoS hardening) —
/// the same file path minted a different "id" after every server restart.
/// A same-process "call it twice" assertion can never catch that, because
/// the randomized seed is fixed for the lifetime of one process. These
/// golden-value tests pin the id to a specific pre-computed FNV-1a result;
/// they can only pass against a pure function of the input bytes; against
/// randomized string.GetHashCode() they fail intermittently across runs.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class ImageDtoTests
{
    private static TmdbImage MakeTmdbImage(string filePath, int width = 1920, int height = 1080) =>
        new()
        {
            FilePath = filePath,
            Width = width,
            Height = height,
            Iso6391 = "en",
            VoteAverage = 7.5f,
            VoteCount = 10,
        };

    [Fact]
    public void Ctor_TmdbImage_SameFilePath_MintsSameId_WithinProcess()
    {
        TmdbImage first = MakeTmdbImage(filePath: "/abc123.jpg");
        TmdbImage second = MakeTmdbImage(filePath: "/abc123.jpg");

        ImageDto firstDto = new(media: first);
        ImageDto secondDto = new(media: second);

        firstDto.Id.Should().Be(expected: secondDto.Id);
    }

    [Fact]
    public void Ctor_TmdbImage_DifferentFilePaths_MintDifferentIds()
    {
        ImageDto dtoA = new(media: MakeTmdbImage(filePath: "/path-a.jpg"));
        ImageDto dtoB = new(media: MakeTmdbImage(filePath: "/path-b.jpg"));

        dtoA.Id.Should().NotBe(unexpected: dtoB.Id);
    }

    [Fact]
    public void Ctor_TmdbImage_NeverProducesNegativeId()
    {
        // Sweep a range of inputs since the sign-flip transform is the part
        // most likely to regress if the hash algorithm changes again.
        for (int i = 0; i < 200; i++)
        {
            ImageDto dto = new(media: MakeTmdbImage(filePath: $"/sweep-{i}-{new string(c: 'x', count: i % 7)}.jpg"));
            dto.Id.Should().BeGreaterThanOrEqualTo(expected: 0);
        }
    }

    [Theory]
    [InlineData(data: ["/abc123.jpg", 922_006_951L])]
    [InlineData(data: ["/path-a.jpg", 1_781_138_878L])]
    [InlineData(data: ["/path-b.jpg", 2_746_522_937L])]
    [InlineData(data: ["", 3_128_831_035L])]
    [InlineData(data: ["/determinism-check-9f3ac1.jpg", 2_410_689_069L])]
    public void Ctor_TmdbImage_MintsThePreComputedFnv1AId_NotARandomizedHash(
        string filePath,
        long expectedId
    )
    {
        // These expected values are the FNV-1a hash of each input string,
        // computed independently of this codebase. string.GetHashCode() is
        // randomized per process and would only match one of these values by
        // 1-in-2^32 chance, so this pins the algorithm rather than merely
        // re-deriving whatever the implementation currently does.
        ImageDto dto = new(media: MakeTmdbImage(filePath: filePath));

        dto.Id.Should().Be(expected: expectedId);
    }

    [Fact]
    public void Ctor_TmdbImage_NullFilePath_DoesNotThrow_AndIsDeterministic()
    {
        TmdbImage image = new()
        {
            FilePath = null!,
            Width = 100,
            Height = 200,
        };

        ImageDto first = new(media: image);
        ImageDto second = new(media: image);

        first.Id.Should().Be(expected: second.Id);
    }

    [Fact]
    public void Ctor_TmdbImage_WidthGreaterThanOrEqualHeight_TypeIsBackdrop()
    {
        ImageDto dto = new(media: MakeTmdbImage(filePath: "/landscape.jpg", width: 1920, height: 1080));

        dto.Type.Should().Be(expected: "backdrop");
    }

    [Fact]
    public void Ctor_TmdbImage_HeightGreaterThanWidth_TypeIsPoster()
    {
        ImageDto dto = new(media: MakeTmdbImage(filePath: "/portrait.jpg", width: 500, height: 750));

        dto.Type.Should().Be(expected: "poster");
    }

    [Fact]
    public void Ctor_TmdbImage_SetsSrcAndMetadataFromSource()
    {
        TmdbImage source = MakeTmdbImage(filePath: "/src-path.jpg");

        ImageDto dto = new(media: source);

        dto.Src.Should().Be(expected: "/src-path.jpg");
        dto.Width.Should().Be(expected: 1920);
        dto.Height.Should().Be(expected: 1080);
        dto.Iso6391.Should().Be(expected: "en");
        dto.VoteAverage.Should().Be(expected: 7.5f);
        dto.VoteCount.Should().Be(expected: 10);
        dto.ColorPalette.Should().NotBeNull();
    }

    [Fact]
    public void Ctor_TmdbProfile_SameFilePath_MintsSameId()
    {
        TmdbProfile first = new()
        {
            FilePath = "/profile-abc.jpg",
            Width = 300,
            Height = 450,
        };
        TmdbProfile second = new()
        {
            FilePath = "/profile-abc.jpg",
            Width = 300,
            Height = 450,
        };

        ImageDto firstDto = new(image: first);
        ImageDto secondDto = new(image: second);

        firstDto.Id.Should().Be(expected: secondDto.Id);
        firstDto.Type.Should().Be(expected: "poster");
    }

    [Fact]
    public void Ctor_TmdbProfile_NullFilePath_DoesNotThrow()
    {
        TmdbProfile profile = new()
        {
            FilePath = null,
            Width = 100,
            Height = 200,
        };

        ImageDto dto = new(image: profile);

        dto.Id.Should().BeGreaterThanOrEqualTo(expected: 0);
    }

    [Fact]
    public void Ctor_Image_TmdbSite_SrcIsRelativeToSiteRoot()
    {
        Image media = new()
        {
            Id = 42,
            FilePath = "/tmdb-poster.jpg",
            Site = "https://image.tmdb.org/t/p/",
            Type = "poster",
            Width = 500,
            Height = 750,
            Iso6391 = "en",
            VoteAverage = 8.1,
            VoteCount = 4,
        };

        ImageDto dto = new(media: media);

        dto.Id.Should().Be(expected: 42);
        dto.Src.Should().Be(expected: "/tmdb-poster.jpg");
        dto.Type.Should().Be(expected: "poster");
    }

    [Fact]
    public void Ctor_Image_NonTmdbSite_SrcIsRelativeToMusicImagesRoot()
    {
        Image media = new()
        {
            Id = 7,
            FilePath = "/artist-cover.jpg",
            Site = "https://coverartarchive.org/",
            Type = "cover",
            Width = 500,
            Height = 500,
        };

        ImageDto dto = new(media: media);

        dto.Src.Should().Be(expected: "/images/music/artist-cover.jpg");
    }
}
