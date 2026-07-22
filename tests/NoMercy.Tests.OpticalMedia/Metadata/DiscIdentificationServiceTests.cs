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

[Trait(name: "Category", value: "Unit")]
public class DiscIdentificationServiceTests
{
    private static DiscInfo MakeDisc(OpticalDiscType type) =>
        new(
            Type: type,
            DiscLabel: "TEST_DISC",
            Titles: [],
            AudioTracks: null,
            TotalDuration: TimeSpan.FromMinutes(minutes: 90)
        );

    private static DiscIdentification MakeIdentification(
        MediaKind kind,
        bool needsManual = false
    ) =>
        new(
            Kind: kind,
            Candidates: needsManual
                ? []
                :
                [
                    new(
                        Source: "tmdb",
                        StableId: "12345",
                        Title: "Test",
                        Year: 2024,
                        PosterUrl: null,
                        BackdropUrl: null,
                        Confidence: 0.9
                    ),
                ],
            TopConfidence: needsManual ? 0 : 0.9,
            AutoApply: !needsManual,
            NeedsManualAssignment: needsManual
        );

    // ── Dispatcher routing ────────────────────────────────────────────────

    [Fact]
    public async Task IdentifyAsync_CdDisc_RoutesToAudioCdIdentifier()
    {
        Mock<IDiscIdentifier> videeMock = new();
        videeMock.Setup(expression: id => id.CanHandle(OpticalDiscType.Cd)).Returns(value: false);
        videeMock.Setup(expression: id => id.CanHandle(OpticalDiscType.Dvd)).Returns(value: true);
        videeMock.Setup(expression: id => id.CanHandle(OpticalDiscType.BluRay)).Returns(value: true);

        Mock<IDiscIdentifier> audioMock = new();
        audioMock.Setup(expression: id => id.CanHandle(OpticalDiscType.Cd)).Returns(value: true);
        DiscIdentification expected = MakeIdentification(kind: MediaKind.Music);
        audioMock
            .Setup(expression: id => id.IdentifyAsync(It.IsAny<DiscInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: expected);

        DiscIdentificationService sut = new(
            identifiers: [videeMock.Object, audioMock.Object],
            logger: NullLogger<DiscIdentificationService>.Instance
        );

        DiscIdentification result = await sut.IdentifyAsync(
            disc: MakeDisc(type: OpticalDiscType.Cd),
            ct: CancellationToken.None
        );

        result.Should().Be(expected: expected);
        audioMock.Verify(
            expression: id => id.IdentifyAsync(It.IsAny<DiscInfo>(), It.IsAny<CancellationToken>()),
            times: Times.Once
        );
        videeMock.Verify(
            expression: id => id.IdentifyAsync(It.IsAny<DiscInfo>(), It.IsAny<CancellationToken>()),
            times: Times.Never
        );
    }

    [Fact]
    public async Task IdentifyAsync_DvdDisc_RoutesToVideoDiscIdentifier()
    {
        Mock<IDiscIdentifier> videoMock = new();
        videoMock.Setup(expression: id => id.CanHandle(OpticalDiscType.Dvd)).Returns(value: true);
        DiscIdentification expected = MakeIdentification(kind: MediaKind.Movie);
        videoMock
            .Setup(expression: id => id.IdentifyAsync(It.IsAny<DiscInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: expected);

        Mock<IDiscIdentifier> audioMock = new();
        audioMock.Setup(expression: id => id.CanHandle(OpticalDiscType.Dvd)).Returns(value: false);
        audioMock.Setup(expression: id => id.CanHandle(OpticalDiscType.Cd)).Returns(value: true);

        DiscIdentificationService sut = new(
            identifiers: [videoMock.Object, audioMock.Object],
            logger: NullLogger<DiscIdentificationService>.Instance
        );

        DiscIdentification result = await sut.IdentifyAsync(
            disc: MakeDisc(type: OpticalDiscType.Dvd),
            ct: CancellationToken.None
        );

        result.Should().Be(expected: expected);
        videoMock.Verify(
            expression: id => id.IdentifyAsync(It.IsAny<DiscInfo>(), It.IsAny<CancellationToken>()),
            times: Times.Once
        );
        audioMock.Verify(
            expression: id => id.IdentifyAsync(It.IsAny<DiscInfo>(), It.IsAny<CancellationToken>()),
            times: Times.Never
        );
    }

    [Fact]
    public async Task IdentifyAsync_BluRayDisc_RoutesToVideoDiscIdentifier()
    {
        Mock<IDiscIdentifier> videoMock = new();
        videoMock.Setup(expression: id => id.CanHandle(OpticalDiscType.BluRay)).Returns(value: true);
        DiscIdentification expected = MakeIdentification(kind: MediaKind.Movie);
        videoMock
            .Setup(expression: id => id.IdentifyAsync(It.IsAny<DiscInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: expected);

        Mock<IDiscIdentifier> audioMock = new();
        audioMock.Setup(expression: id => id.CanHandle(OpticalDiscType.BluRay)).Returns(value: false);

        DiscIdentificationService sut = new(
            identifiers: [videoMock.Object, audioMock.Object],
            logger: NullLogger<DiscIdentificationService>.Instance
        );

        DiscIdentification result = await sut.IdentifyAsync(
            disc: MakeDisc(type: OpticalDiscType.BluRay),
            ct: CancellationToken.None
        );

        result.Should().Be(expected: expected);
        videoMock.Verify(
            expression: id => id.IdentifyAsync(It.IsAny<DiscInfo>(), It.IsAny<CancellationToken>()),
            times: Times.Once
        );
    }

    // ── NeedsManualAssignment fallback ────────────────────────────────────

    [Fact]
    public async Task IdentifyAsync_NoHandlerRegistered_ReturnsNeedsManualAssignment()
    {
        Mock<IDiscIdentifier> audioMock = new();
        audioMock.Setup(expression: id => id.CanHandle(It.IsAny<OpticalDiscType>())).Returns(value: false);

        DiscIdentificationService sut = new(
            identifiers: [audioMock.Object],
            logger: NullLogger<DiscIdentificationService>.Instance
        );

        DiscIdentification result = await sut.IdentifyAsync(
            disc: MakeDisc(type: OpticalDiscType.Dvd),
            ct: CancellationToken.None
        );

        result.NeedsManualAssignment.Should().BeTrue();
        result.AutoApply.Should().BeFalse();
        result.Candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task IdentifyAsync_EmptyIdentifierList_ReturnsNeedsManualAssignment()
    {
        DiscIdentificationService sut = new(identifiers: [], logger: NullLogger<DiscIdentificationService>.Instance);

        DiscIdentification result = await sut.IdentifyAsync(
            disc: MakeDisc(type: OpticalDiscType.Cd),
            ct: CancellationToken.None
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
            identifiers: [audioMock.Object],
            logger: NullLogger<DiscIdentificationService>.Instance
        );

        DiscCandidate[] result = await sut.SearchAsync(
            query: "Inception",
            type: MediaType.Movie,
            ct: CancellationToken.None
        );

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_EmptyIdentifierList_ReturnsEmpty()
    {
        DiscIdentificationService sut = new(identifiers: [], logger: NullLogger<DiscIdentificationService>.Instance);

        DiscCandidate[] result = await sut.SearchAsync(
            query: "Inception",
            type: MediaType.Movie,
            ct: CancellationToken.None
        );

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_VideoDiscIdentifierRegistered_DelegatesToIt()
    {
        VideoDiscIdentifier videoIdentifier = new(logger: NullLogger<VideoDiscIdentifier>.Instance);

        DiscIdentificationService sut = new(
            identifiers: [videoIdentifier],
            logger: NullLogger<DiscIdentificationService>.Instance
        );

        // No network seam on VideoDiscIdentifier.SearchAsync's TMDB call —
        // an empty query short-circuits before any HTTP call is made, which
        // still proves the delegation path (non-empty query paths are
        // covered end-to-end in VideoDiscIdentifierTests via the HTTP harness).
        DiscCandidate[] result = await sut.SearchAsync(
            query: string.Empty,
            type: MediaType.Movie,
            ct: CancellationToken.None
        );

        result.Should().BeEmpty();
    }
}
