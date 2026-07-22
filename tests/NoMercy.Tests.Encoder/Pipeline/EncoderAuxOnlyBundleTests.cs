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

using NoMercy.Encoder.Decomposition;

namespace NoMercy.Tests.Encoder.Pipeline;

/// <summary>
/// A bundled Whole task that covers no video and no audio writes no variant
/// playlists. Finalizing it as a full encode measures the whole plan's variants
/// against a staging directory that never received them and fails the task on
/// "Master playlist would list zero variants" — which costs the job its
/// post-encode phase, and with it the subtitle OCR that runs there.
/// </summary>
public class EncoderAuxOnlyBundleTests
{
    private static DecomposedTask Task(
        EncodeTaskKind kind,
        int[]? video = null,
        int[]? audio = null,
        bool? thumbs = null,
        bool partial = false
    ) =>
        new(
            TaskId: "task-0",
            ParentJobId: 1,
            GroupTag: "group",
            Kind: kind,
            OutputIndex: 0,
            Resources: null,
            VideoSliceIndexes: video,
            AudioSliceIndexes: audio,
            IncludeThumbnails: thumbs,
            IsPartialTopUp: partial
        );

    private static bool Is(DecomposedTask? task) => task?.IsAuxOnlyBundle == true;

    private static bool LeavesMaster(DecomposedTask? task) =>
        task?.LeavesMasterPlaylistAlone == true;

    [Fact]
    public void ThumbnailsOnlyBundle_IsAuxOnly()
    {
        // What the coordinator dispatches when only the thumbnail strip is
        // missing: a Whole bundle covering no streams at all.
        Is(task: Task(kind: EncodeTaskKind.Whole, video: [], audio: [], thumbs: true)).Should().BeTrue();
    }

    [Fact]
    public void FullBundle_IsNotAuxOnly()
    {
        // Null slice indexes mean "every output" — the ordinary whole encode,
        // which must still write its master playlist.
        Is(task: Task(kind: EncodeTaskKind.Whole)).Should().BeFalse();
    }

    [Fact]
    public void BundleCarryingVideo_IsNotAuxOnly()
    {
        Is(task: Task(kind: EncodeTaskKind.Whole, video: [0], audio: [])).Should().BeFalse();
    }

    [Fact]
    public void BundleCarryingAudio_IsNotAuxOnly()
    {
        // Audio alone still produces a variant playlist the master must list.
        Is(task: Task(kind: EncodeTaskKind.Whole, video: [], audio: [0])).Should().BeFalse();
    }

    // ── Leaving the master alone: aux-only OR a partial top-up ───────────────
    // The master lists every rendition. A bundle covering less than the whole set
    // must not regenerate it from its slice, or it drops the renditions it did not
    // touch — which is exactly how an audio-only re-encode wiped the video entry.

    [Fact]
    public void AudioOnlyPartialTopUp_LeavesTheMasterAlone()
    {
        // The audio-only rebuild: video already on disk, only the audio rendition
        // regenerated. Its sliced plan has no video, so it must not rewrite the
        // master that still correctly lists both.
        LeavesMaster(task: Task(kind: EncodeTaskKind.Whole, video: [], audio: [0], partial: true))
            .Should()
            .BeTrue();
    }

    [Fact]
    public void VideoOnlyPartialTopUp_LeavesTheMasterAlone()
    {
        LeavesMaster(task: Task(kind: EncodeTaskKind.Whole, video: [0], audio: [], partial: true))
            .Should()
            .BeTrue();
    }

    [Fact]
    public void AuxOnlyBundle_LeavesTheMasterAlone_EvenWhenNotFlaggedPartial()
    {
        LeavesMaster(task: Task(kind: EncodeTaskKind.Whole, video: [], audio: [], thumbs: true))
            .Should()
            .BeTrue();
    }

    [Fact]
    public void FullBundle_WritesTheMaster()
    {
        // Not a top-up and not aux-only: the ordinary full encode owns the master.
        LeavesMaster(task: Task(kind: EncodeTaskKind.Whole, video: [0], audio: [0])).Should().BeFalse();
    }

    [Fact]
    public void PerStreamThumbnailTask_IsNotAuxOnly_ItDefersFinalizeAnyway()
    {
        // Only Whole-kind bundles reach the finalize branch this guards; a
        // per-stream task already defers to the coordinator's FinalizeOnly pass,
        // and must not be mistaken for a bundle.
        Is(task: Task(kind: EncodeTaskKind.Thumbnails, video: [], audio: [])).Should().BeFalse();
    }

    [Fact]
    public void NoTaskFilter_IsNotAuxOnly()
    {
        // A plain undecomposed encode has no filter at all.
        Is(task: null).Should().BeFalse();
    }
}
