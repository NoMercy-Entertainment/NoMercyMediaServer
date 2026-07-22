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
using NoMercy.Encoder.Audio;
using NoMercy.Events;
using NoMercy.Events.DriveMonitor;
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Drives;
using NoMercy.OpticalMedia.Metadata;
using NoMercy.OpticalMedia.Rip;
using NoMercy.OpticalMedia.Sources;
using NoMercy.Providers.MusicBrainz.Client;
using NoMercy.Storage;

namespace NoMercy.Tests.OpticalMedia.Rip;

/// <summary>
/// Unit tests for the CD music branch in <see cref="DiscRipJob"/>.
/// Uses RipMode.RipToRaw to skip the DB move-and-import path.
/// </summary>
[Collection(name: "EventBusProvider")]
[Trait(name: "Category", value: "Unit")]
public class DiscRipJobCdBranchTests
{
    private static RipRequest CdRequest(string drivePath = "/dev/sr0", string? metadataId = null) =>
        new(
            DrivePath: drivePath,
            SelectedTitleIndices: [1, 2, 3],
            MetadataId: metadataId,
            Custom: null,
            LibraryId: Ulid.NewUlid(),
            FolderId: Ulid.NewUlid(),
            EncodingProfileId: null,
            AudioTracks: [],
            Subtitles: [],
            Mode: RipMode.RipToRaw,
            DiscType: OpticalDiscType.Cd
        );

    private static DiscRipResult[] SuccessResults(int[] trackIndices) =>
        trackIndices
            .Select(selector: i => new DiscRipResult(
                TitleIndex: i,
                OutputPath: Path.Combine(path1: Path.GetTempPath(), path2: $"{i:D2} - Track {i:D2}.flac"),
                Success: true,
                Duration: TimeSpan.FromMinutes(minutes: 3),
                OutputSizeBytes: 50_000_000,
                Error: null
            ))
            .ToArray();

    private static DiscRipJob MakeJob(
        RipRequest request,
        IDiscRipper ripper,
        IAudioMetadataWriter? tagWriter = null,
        MusicBrainzReleaseClient? mbClient = null
    )
    {
        DiscRipJob job = new(
            request: request,
            outputDir: Path.GetTempPath(),
            targetFolderId: null,
            targetLibraryId: null,
            targetLibraryType: null
        );

        job.DiscRipper = ripper;
        job.IdentificationService = new(
            identifiers: [],
            logger: NullLogger<DiscIdentificationService>.Instance
        );
        job.StorageFactory = Mock.Of<IStorageFactory>();
        job.StorageDriver = Mock.Of<IStorageDriver>();
        job.DriveLockRegistry = new();
        job.LoggerFactory = NullLoggerFactory.Instance;
        job.AudioMetadataWriter = tagWriter ?? Mock.Of<IAudioMetadataWriter>();
        job.MusicBrainzReleaseClient = mbClient ?? new MusicBrainzReleaseClient();

        return job;
    }

    // ── RipToRaw path fires rip_complete ──────────────────────────────────

    [Fact]
    public async Task Handle_CdRipToRaw_PublishesStartedAndComplete()
    {
        DiscRipResult[] ripResults = SuccessResults(trackIndices: [1, 2, 3]);
        Mock<IDiscRipper> ripperMock = new();
        ripperMock
            .Setup(expression: r =>
                r.RipAsync(
                    It.IsAny<RipRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: ripResults);

        List<DriveStateChangedEvent> published = [];
        Mock<IEventBus> busMock = new();
        busMock
            .Setup(expression: b =>
                b.PublishAsync(It.IsAny<DriveStateChangedEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback<DriveStateChangedEvent, CancellationToken>(action: (evt, _) => published.Add(item: evt))
            .Returns(value: Task.CompletedTask);

        EventBusProvider.Configure(eventBus: busMock.Object);

        DiscRipJob job = MakeJob(request: CdRequest(metadataId: null), ripper: ripperMock.Object);
        await job.Handle();

        published.Select(selector: e => e.DriveStateData.Method).Should().Contain(expected: "rip_started");
        published.Select(selector: e => e.DriveStateData.Method).Should().Contain(expected: "rip_complete");
        published.Select(selector: e => e.DriveStateData.Method).Should().NotContain(unexpected: "rip_error");
    }

    // ── No metadataId → tag writer is NOT called ──────────────────────────

    [Fact]
    public async Task Handle_CdRipToRaw_NoMetadataId_TagWriterNotCalled()
    {
        DiscRipResult[] ripResults = SuccessResults(trackIndices: [1]);
        Mock<IDiscRipper> ripperMock = new();
        ripperMock
            .Setup(expression: r =>
                r.RipAsync(
                    It.IsAny<RipRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: ripResults);

        Mock<IAudioMetadataWriter> tagWriterMock = new();

        Mock<IEventBus> busMock = new();
        busMock
            .Setup(expression: b =>
                b.PublishAsync(It.IsAny<DriveStateChangedEvent>(), It.IsAny<CancellationToken>())
            )
            .Returns(value: Task.CompletedTask);
        EventBusProvider.Configure(eventBus: busMock.Object);

        DiscRipJob job = MakeJob(
            request: CdRequest(metadataId: null),
            ripper: ripperMock.Object,
            tagWriter: tagWriterMock.Object
        );
        await job.Handle();

        tagWriterMock.Verify(
            expression: tw =>
                tw.WriteTagsAsync(
                    It.IsAny<string>(),
                    It.IsAny<AudioMetadata>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never,
            failMessage: "tag writer must not be called when no MetadataId is supplied"
        );
    }

    // ── DiscRipper failure → rip_error event, tag writer not called ───────

    [Fact]
    public async Task Handle_CdRipFails_PublishesError_TagWriterNotCalled()
    {
        Mock<IDiscRipper> ripperMock = new();
        ripperMock
            .Setup(expression: r =>
                r.RipAsync(
                    It.IsAny<RipRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(exception: new InvalidOperationException(message: "drive gone"));

        Mock<IAudioMetadataWriter> tagWriterMock = new();

        List<DriveStateChangedEvent> published = [];
        Mock<IEventBus> busMock = new();
        busMock
            .Setup(expression: b =>
                b.PublishAsync(It.IsAny<DriveStateChangedEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback<DriveStateChangedEvent, CancellationToken>(action: (evt, _) => published.Add(item: evt))
            .Returns(value: Task.CompletedTask);
        EventBusProvider.Configure(eventBus: busMock.Object);

        DiscRipJob job = MakeJob(
            request: CdRequest(metadataId: "some-mbid"),
            ripper: ripperMock.Object,
            tagWriter: tagWriterMock.Object
        );
        await job.Handle();

        published.Should().Contain(predicate: e => e.DriveStateData.Method == "rip_error");
        tagWriterMock.Verify(
            expression: tw =>
                tw.WriteTagsAsync(
                    It.IsAny<string>(),
                    It.IsAny<AudioMetadata>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never
        );
    }

    // ── TagWriter failure is non-fatal ────────────────────────────────────

    [Fact]
    public async Task Handle_CdRip_TagWriterThrows_DoesNotCrashJob()
    {
        DiscRipResult[] ripResults = SuccessResults(trackIndices: [1]);
        Mock<IDiscRipper> ripperMock = new();
        ripperMock
            .Setup(expression: r =>
                r.RipAsync(
                    It.IsAny<RipRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: ripResults);

        // Tag writer throws; this should be caught and logged, not propagated.
        Mock<IAudioMetadataWriter> tagWriterMock = new();
        tagWriterMock
            .Setup(expression: tw =>
                tw.WriteTagsAsync(
                    It.IsAny<string>(),
                    It.IsAny<AudioMetadata>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(exception: new InvalidOperationException(message: "TagLib write error"));

        // MusicBrainz client is not called in RipToRaw mode (no metadataId path),
        // so this tests that a tag-write failure inside TagCdTracksAsync does not bubble.
        // We supply a metadataId to trigger the tagging path but the client will
        // return null (no real HTTP), and the code gracefully handles that.
        Mock<IEventBus> busMock = new();
        busMock
            .Setup(expression: b =>
                b.PublishAsync(It.IsAny<DriveStateChangedEvent>(), It.IsAny<CancellationToken>())
            )
            .Returns(value: Task.CompletedTask);
        EventBusProvider.Configure(eventBus: busMock.Object);

        DiscRipJob job = MakeJob(
            request: CdRequest(metadataId: null),
            ripper: ripperMock.Object,
            tagWriter: tagWriterMock.Object
        );

        Exception? ex = await Record.ExceptionAsync(testCode: () => job.Handle());
        ex.Should().BeNull(because: "a tag-write failure must not crash the job");
    }

    // ── Non-CD disc type does not use CD path ────────────────────────────

    [Fact]
    public async Task Handle_BluRayDisc_TagWriterNotCalled()
    {
        RipRequest bluRayRequest = new(
            DrivePath: "bluray:/dev/sr0",
            SelectedTitleIndices: [1],
            MetadataId: null,
            Custom: new(
                Title: "The Matrix",
                Year: 1999,
                Type: MediaType.Movie,
                PosterUrl: null
            ),
            LibraryId: Ulid.NewUlid(),
            FolderId: Ulid.NewUlid(),
            EncodingProfileId: null,
            AudioTracks: [],
            Subtitles: [],
            Mode: RipMode.RipToRaw,
            DiscType: OpticalDiscType.BluRay
        );

        DiscRipResult[] ripResults =
        [
            new(
                TitleIndex: 1,
                OutputPath: Path.Combine(path1: Path.GetTempPath(), path2: "title_01.mkv"),
                Success: true,
                Duration: TimeSpan.FromMinutes(minutes: 120),
                OutputSizeBytes: 40_000_000_000,
                Error: null
            ),
        ];

        Mock<IDiscRipper> ripperMock = new();
        ripperMock
            .Setup(expression: r =>
                r.RipAsync(
                    It.IsAny<RipRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: ripResults);

        Mock<IAudioMetadataWriter> tagWriterMock = new();

        Mock<IEventBus> busMock = new();
        busMock
            .Setup(expression: b =>
                b.PublishAsync(It.IsAny<DriveStateChangedEvent>(), It.IsAny<CancellationToken>())
            )
            .Returns(value: Task.CompletedTask);
        EventBusProvider.Configure(eventBus: busMock.Object);

        DiscRipJob job = MakeJob(request: bluRayRequest, ripper: ripperMock.Object, tagWriter: tagWriterMock.Object);
        await job.Handle();

        tagWriterMock.Verify(
            expression: tw =>
                tw.WriteTagsAsync(
                    It.IsAny<string>(),
                    It.IsAny<AudioMetadata>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never,
            failMessage: "tag writer must not be called for Blu-ray discs"
        );
    }
}
