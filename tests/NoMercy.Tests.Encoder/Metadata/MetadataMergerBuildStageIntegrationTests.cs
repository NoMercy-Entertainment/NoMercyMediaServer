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
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Metadata;
using NoMercy.Encoder.Naming;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Optimizer;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.PostProcess;
using NoMercy.Tests.Encoder.Pipeline.Stages;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Metadata;

/// <summary>
/// Integration test: when a copy-mode preset is active, BuildStage passes
/// merged tracks (source language wins, DB title wins) to MetadataInjector.
/// </summary>
public class MetadataMergerBuildStageIntegrationTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static BuildStage CreateStage(IMetadataInjector injector, IMetadataMerger merger) =>
        new(
            new() { FfmpegPathOverride = "ffmpeg", FfprobePathOverride = "ffprobe" },
            fontExtractor: new FontExtractor(TestStorageFactory.CreateLocal()),
            subtitleExtractor: new SubtitleExtractor(),
            outputStrategyFactory: OutputStrategyFactoryTestHelper.Create(),
            drmProcessors: [],
            logger: NullLogger<BuildStage>.Instance,
            storage: TestStorageFactory.CreateLocal(),
            metadataInjector: injector,
            metadataMerger: merger
        );

    /// <summary>
    /// Copy-mode MKV plan: video EncoderName = "copy", audio Action = Copy.
    /// </summary>
    private static ExecutionPlan BuildCopyMkvPlan() =>
        new(
            [
                new(
                    "g0",
                    [
                        new("decode_0", OperationType.Decode, [], new()),
                        new("encode_0", OperationType.Encode, ["decode_0"], new()),
                    ],
                    null,
                    0,
                    2,
                    false,
                    1
                ),
            ],
            TimeSpan.FromMinutes(90),
            new(
                OutputFormat.Mkv,
                [
                    new(
                        1920,
                        1080,
                        "copy",
                        0,
                        0,
                        null,
                        null,
                        null,
                        false,
                        "yuv420p",
                        "0:v:0",
                        new()
                    ),
                ],
                [
                    new(
                        "copy",
                        0,
                        0,
                        0,
                        StreamAction.Copy,
                        "eng",
                        "0:a:0"
                    ),
                ],
                [],
                null
            )
        );

    private static MediaInfo BuildMediaInfo() =>
        new(
            "/movies/fight_club.mkv",
            "matroska",
            TimeSpan.FromMinutes(139),
            20000,
            20_000_000_000,
            [
                new(
                    0,
                    "hevc",
                    1920,
                    1080,
                    23.976,
                    8,
                    "yuv420p",
                    null,
                    null,
                    null,
                    true,
                    18000
                ),
            ],
            [
                // Source says "jpn" — DB will supply title override but NOT language
                new(
                    0,
                    "truehd",
                    8,
                    48000,
                    5000,
                    "jpn",
                    true,
                    false
                ),
            ],
            [],
            []
        );

    // -----------------------------------------------------------------------
    // Integration test
    // -----------------------------------------------------------------------

    [Fact]
    public async Task BuildStage_CopyMode_MergedTracksReflectSourceLanguageAndDbTitle()
    {
        // Source stream at OutputIndex 0: language=jpn, no title
        // DB says: language=eng (ignored), title="Japanese TrueHD 7.1"
        // Expected merged: language=jpn (source wins), title="Japanese TrueHD 7.1" (DB wins)

        CapturingMetadataInjector injector = new();
        MetadataMerger merger = new();
        BuildStage stage = CreateStage(injector, merger);

        // DB track: language=eng (should be overridden by source), explicit title
        TrackMetadata dbTrack = new(
            0,
            "audio",
            "eng",
            "Japanese TrueHD 7.1",
            true,
            false
        );

        // Source tracks injected via EncodingContext
        SourceTrackMetadata srcTrack = new(
            0,
            "audio",
            "jpn",
            null,
            null,
            true,
            false
        );

        EncodingContext context = EncodingContext.Create() with
        {
            MediaItem = new MovieMediaRef(
                MediaType.Movie,
                550,
                "Fight Club",
                1999
            ),
            MediaInfo = BuildMediaInfo(),
            SourceTracks = [srcTrack],
            DbTracks = [dbTrack],
            EnableMetadataInjection = true,
        };

        ExecutionPlan plan = BuildCopyMkvPlan();
        BuildInput input = new(
            plan,
            "/movies/fight_club.mkv",
            "/tmp/nmtest-output/fc",
            "Fight Club.NoMercy"
        );

        StageResult result = await stage.ExecuteAsync(input, context, default);

        result.Should().BeOfType<StageSuccess<FfmpegCommand[]>>();

        // The capturing injector recorded the context it received
        injector.CapturedContext.Should().NotBeNull();
        IReadOnlyList<TrackMetadata> tracks = injector.CapturedContext!.Tracks;

        tracks.Should().ContainSingle();
        TrackMetadata merged = tracks[0];
        merged.Language.Should().Be("jpn", "source language must win over DB language");
        merged.Title.Should().Be("Japanese TrueHD 7.1", "DB title must win when non-empty");
        merged.IsDefault.Should().BeTrue("DB IsDefault wins");
    }

    // -----------------------------------------------------------------------
    // Helper: injector that captures context for assertion
    // -----------------------------------------------------------------------

    private sealed class CapturingMetadataInjector : IMetadataInjector
    {
        public MetadataInjectionContext? CapturedContext { get; private set; }

        public IReadOnlyList<string> BuildArgs(MetadataInjectionContext ctx)
        {
            CapturedContext = ctx;
            return [];
        }
    }
}
