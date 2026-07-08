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

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Metadata;
using NoMercy.OpticalMedia.Sources;
using NoMercy.Providers.MusicBrainz.Client;

namespace NoMercy.Tests.OpticalMedia.Metadata;

[Trait("Category", "Unit")]
public class AudioCdIdentifierTests
{
    private static DiscInfo MakeCdDisc(string label = "AUDIO_CD") =>
        new(
            Type: OpticalDiscType.Cd,
            DiscLabel: label,
            Titles: [],
            AudioTracks:
            [
                new(1, null, null, TimeSpan.FromSeconds(180), 44100, 2),
                new(2, null, null, TimeSpan.FromSeconds(210), 44100, 2),
            ],
            TotalDuration: TimeSpan.FromSeconds(390)
        );

    private static AudioCdIdentifier MakeSut(
        ITocReader? tocReader = null,
        MusicBrainzDiscClient? discClient = null
    )
    {
        tocReader ??= new NullTocReader();
        discClient ??= new();
        return new(tocReader, discClient, NullLogger<AudioCdIdentifier>.Instance);
    }

    // ── CanHandle ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(OpticalDiscType.Cd, true)]
    [InlineData(OpticalDiscType.Dvd, false)]
    [InlineData(OpticalDiscType.BluRay, false)]
    [InlineData(OpticalDiscType.None, false)]
    public void CanHandle_ReturnsCorrectValueForDiscType(OpticalDiscType type, bool expectedResult)
    {
        AudioCdIdentifier sut = MakeSut();
        sut.CanHandle(type).Should().Be(expectedResult);
    }

    // ── TOC unavailable → NeedsManualAssignment ────────────────────────────

    [Fact]
    public async Task IdentifyAsync_WhenTocIsNull_ReturnsNeedsManualAssignment()
    {
        Mock<ITocReader> tocMock = new();
        tocMock
            .Setup(r => r.ReadTocAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DiscToc?)null);

        AudioCdIdentifier sut = MakeSut(tocReader: tocMock.Object);
        DiscIdentification result = await sut.IdentifyAsync(MakeCdDisc(), CancellationToken.None);

        result.NeedsManualAssignment.Should().BeTrue();
        result.AutoApply.Should().BeFalse();
        result.Candidates.Should().BeEmpty();
        result.Kind.Should().Be(MediaKind.Music);
    }

    [Fact]
    public async Task IdentifyAsync_WithNullTocReaderDefault_ReturnsNeedsManualAssignment()
    {
        AudioCdIdentifier sut = MakeSut(tocReader: new NullTocReader());
        DiscIdentification result = await sut.IdentifyAsync(MakeCdDisc(), CancellationToken.None);

        result.NeedsManualAssignment.Should().BeTrue();
    }

    // ── VideoDiscIdentifier CanHandle ──────────────────────────────────────

    [Theory]
    [InlineData(OpticalDiscType.Dvd, true)]
    [InlineData(OpticalDiscType.BluRay, true)]
    [InlineData(OpticalDiscType.Cd, false)]
    [InlineData(OpticalDiscType.None, false)]
    public void VideoDiscIdentifier_CanHandle_CorrectDiscTypes(
        OpticalDiscType type,
        bool expectedResult
    )
    {
        VideoDiscIdentifier sut = new(NullLogger<VideoDiscIdentifier>.Instance);
        sut.CanHandle(type).Should().Be(expectedResult);
    }
}
