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
            Type: type,
            DiscLabel: "TEST_DISC",
            Titles: [],
            AudioTracks: null,
            TotalDuration: TimeSpan.FromMinutes(90)
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
                    new DiscCandidate(
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
}
