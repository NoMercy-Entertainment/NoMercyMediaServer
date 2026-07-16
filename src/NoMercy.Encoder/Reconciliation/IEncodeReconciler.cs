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

using NoMercy.Storage;

namespace NoMercy.Encoder.Reconciliation;

/// <summary>
/// Decides, for a file about to be dispatched to the encoder, what work
/// actually needs to run — skip entirely, run only the missing pieces, or
/// re-encode from scratch. Runs BEFORE any ffmpeg command is built.
/// </summary>
public interface IEncodeReconciler
{
    /// <summary>
    /// Pure decision — no I/O. The primary unit-tested surface: hand it a
    /// profile and a snapshot of what already exists and it returns the
    /// verdict, deterministically.
    /// </summary>
    ReconciliationDecision Decide(ReconciliationInput input);

    /// <summary>
    /// Gathers an <see cref="ExistingOutputSnapshot"/> from disk for a
    /// previously-encoded file. Lists <paramref name="mediaRootPath"/>
    /// recursively — the real on-disk layout every VideoEncodeJob strategy
    /// writes to today (<c>video_*/</c>, <c>audio_*/</c>, <c>subtitles/</c>,
    /// a master playlist, <c>chapters.vtt</c>, <c>fonts.json</c> — all
    /// directly under the media folder, per <see cref="Output.TemplateResolver"/>'s
    /// naming tokens) — and additionally reads <c>manifest.json</c> for a
    /// stored profile fingerprint when one is present. Manifest reading is
    /// forward-compatible rather than load-bearing today: <c>EncodingContext.MediaItem</c>
    /// is not yet populated by any production caller, so
    /// <see cref="Pipeline.Stages.FinalizeStage"/> never actually writes a
    /// manifest in practice yet — every real output on disk today hits the
    /// "no fingerprint, fall back to the real listing" branch, which is
    /// exactly the legacy/backward-compat path reconciliation must handle
    /// correctly regardless.
    /// </summary>
    /// <param name="mediaRootPath">
    /// The media item's folder, scope-relative to <paramref name="destinationStorage"/>
    /// (e.g. <c>"Show Name/Show Name S01E01"</c>).
    /// </param>
    Task<ExistingOutputSnapshot> InspectAsync(
        string mediaRootPath,
        IStorage destinationStorage,
        CancellationToken ct
    );
}
