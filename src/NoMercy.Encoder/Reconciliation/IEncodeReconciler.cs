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
    /// naming tokens) — and additionally reads the media item's
    /// <c>.nomercy.json</c> blueprint for <paramref name="presetId"/>'s stored
    /// profile fingerprint, when present. Output encoded before the
    /// fingerprint existed (or before the blueprint shipped) carries none and
    /// hits the "no fingerprint, fall back to the real listing" branch, which
    /// stays the common case for a long while yet and is exactly the legacy
    /// path reconciliation must keep handling correctly.
    /// </summary>
    /// <param name="mediaRootPath">
    /// The media item's folder, scope-relative to <paramref name="destinationStorage"/>
    /// (e.g. <c>"Show Name/Show Name S01E01"</c>).
    /// </param>
    /// <param name="presetId">
    /// The resolved profile's id — selects this preset's entry out of the
    /// blueprint's <c>encodes[]</c> (one media item can carry several presets).
    /// </param>
    Task<ExistingOutputSnapshot> InspectAsync(
        string mediaRootPath,
        string presetId,
        IStorage destinationStorage,
        CancellationToken ct
    );
}
