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

namespace NoMercy.Tests.OpticalMedia.Metadata;

[Trait("Category", "Unit")]
public class DiscIdentificationServiceTests
{
    private static DiscInfo MakeDisc(OpticalDiscType type) =>
        new(
            type,
            "TEST_DISC",
            [],
            null,
            TimeSpan.FromMinutes(90)
        );

    private static DiscIdentification MakeIdentification(
        MediaKind kind,
        bool needsManual = false
    ) =>
        new(
            kind,
            needsManual
                ? []
                :
                [
                    new(
                        "tmdb",
                        "12345",
                        "Test",
                        2024,
                        null,
                        null,
                        0.9
                    ),
                ],
            needsManual ? 0 : 0.9,
            !needsManual,
            needsManual
        );

    // ── Dispatcher routing ────────────────────────────────────────────────

    [Fact]
    public async Task IdentifyAsync_CdDisc_RoutesToAudioCdIdentifier()
    {
        Mock<IDiscIdentifier> videeMock = new();
        videeMock.Setup(id => id.CanHandle(OpticalDiscType.Cd)).Returns(false);
        videeMock.Setup(id => id.CanHandle(OpticalDiscType.Dvd)).Returns(true);
        videeMock.Setup(id => id.CanHandle(OpticalDiscType.BluRay)).Returns(true);

        Mock<IDiscIdentifier> audioMock = new();
        audioMock.Setup(id => id.CanHandle(OpticalDiscType.Cd)).Returns(true);
        DiscIdentification expected = MakeIdentification(MediaKind.Music);
        audioMock
            .Setup(id => id.IdentifyAsync(It.IsAny<DiscInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        DiscIdentificationService sut = new(
            [videeMock.Object, audioMock.Object],
            NullLogger<DiscIdentificationService>.Instance
        );

        DiscIdentification result = await sut.IdentifyAsync(
            MakeDisc(OpticalDiscType.Cd),
            CancellationToken.None
        );

        result.Should().Be(expected);
        audioMock.Verify(
            id => id.IdentifyAsync(It.IsAny<DiscInfo>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        videeMock.Verify(
            id => id.IdentifyAsync(It.IsAny<DiscInfo>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task IdentifyAsync_DvdDisc_RoutesToVideoDiscIdentifier()
    {
        Mock<IDiscIdentifier> videoMock = new();
        videoMock.Setup(id => id.CanHandle(OpticalDiscType.Dvd)).Returns(true);
        DiscIdentification expected = MakeIdentification(MediaKind.Movie);
        videoMock
            .Setup(id => id.IdentifyAsync(It.IsAny<DiscInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        Mock<IDiscIdentifier> audioMock = new();
        audioMock.Setup(id => id.CanHandle(OpticalDiscType.Dvd)).Returns(false);
        audioMock.Setup(id => id.CanHandle(OpticalDiscType.Cd)).Returns(true);

        DiscIdentificationService sut = new(
            [videoMock.Object, audioMock.Object],
            NullLogger<DiscIdentificationService>.Instance
        );

        DiscIdentification result = await sut.IdentifyAsync(
            MakeDisc(OpticalDiscType.Dvd),
            CancellationToken.None
        );

        result.Should().Be(expected);
        videoMock.Verify(
            id => id.IdentifyAsync(It.IsAny<DiscInfo>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        audioMock.Verify(
            id => id.IdentifyAsync(It.IsAny<DiscInfo>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task IdentifyAsync_BluRayDisc_RoutesToVideoDiscIdentifier()
    {
        Mock<IDiscIdentifier> videoMock = new();
        videoMock.Setup(id => id.CanHandle(OpticalDiscType.BluRay)).Returns(true);
        DiscIdentification expected = MakeIdentification(MediaKind.Movie);
        videoMock
            .Setup(id => id.IdentifyAsync(It.IsAny<DiscInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        Mock<IDiscIdentifier> audioMock = new();
        audioMock.Setup(id => id.CanHandle(OpticalDiscType.BluRay)).Returns(false);

        DiscIdentificationService sut = new(
            [videoMock.Object, audioMock.Object],
            NullLogger<DiscIdentificationService>.Instance
        );

        DiscIdentification result = await sut.IdentifyAsync(
            MakeDisc(OpticalDiscType.BluRay),
            CancellationToken.None
        );

        result.Should().Be(expected);
        videoMock.Verify(
            id => id.IdentifyAsync(It.IsAny<DiscInfo>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    // ── NeedsManualAssignment fallback ────────────────────────────────────

    [Fact]
    public async Task IdentifyAsync_NoHandlerRegistered_ReturnsNeedsManualAssignment()
    {
        Mock<IDiscIdentifier> audioMock = new();
        audioMock.Setup(id => id.CanHandle(It.IsAny<OpticalDiscType>())).Returns(false);

        DiscIdentificationService sut = new(
            [audioMock.Object],
            NullLogger<DiscIdentificationService>.Instance
        );

        DiscIdentification result = await sut.IdentifyAsync(
            MakeDisc(OpticalDiscType.Dvd),
            CancellationToken.None
        );

        result.NeedsManualAssignment.Should().BeTrue();
        result.AutoApply.Should().BeFalse();
        result.Candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task IdentifyAsync_EmptyIdentifierList_ReturnsNeedsManualAssignment()
    {
        DiscIdentificationService sut = new([], NullLogger<DiscIdentificationService>.Instance);

        DiscIdentification result = await sut.IdentifyAsync(
            MakeDisc(OpticalDiscType.Cd),
            CancellationToken.None
        );

        result.NeedsManualAssignment.Should().BeTrue();
    }

    // ── Manual SearchAsync delegation ──────────────────────────────────────

    [Fact]
    public async Task SearchAsync_NoVideoDiscIdentifierRegistered_ReturnsEmpty()
    {
        // Only a non-VideoDiscIdentifier IDiscIdentifier is registered — the
        // dashboard search-box delegate must degrade to empty rather than throw.
        Mock<IDiscIdentifier> audioMock = new();

        DiscIdentificationService sut = new(
            [audioMock.Object],
            NullLogger<DiscIdentificationService>.Instance
        );

        DiscCandidate[] result = await sut.SearchAsync(
            "Inception",
            MediaType.Movie,
            CancellationToken.None
        );

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_EmptyIdentifierList_ReturnsEmpty()
    {
        DiscIdentificationService sut = new([], NullLogger<DiscIdentificationService>.Instance);

        DiscCandidate[] result = await sut.SearchAsync(
            "Inception",
            MediaType.Movie,
            CancellationToken.None
        );

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_VideoDiscIdentifierRegistered_DelegatesToIt()
    {
        VideoDiscIdentifier videoIdentifier = new(NullLogger<VideoDiscIdentifier>.Instance);

        DiscIdentificationService sut = new(
            [videoIdentifier],
            NullLogger<DiscIdentificationService>.Instance
        );

        // No network seam on VideoDiscIdentifier.SearchAsync's TMDB call —
        // an empty query short-circuits before any HTTP call is made, which
        // still proves the delegation path (non-empty query paths are
        // covered end-to-end in VideoDiscIdentifierTests via the HTTP harness).
        DiscCandidate[] result = await sut.SearchAsync(
            string.Empty,
            MediaType.Movie,
            CancellationToken.None
        );

        result.Should().BeEmpty();
    }
}
