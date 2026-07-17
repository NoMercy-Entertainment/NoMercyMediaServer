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

using NoMercy.Encoder.Profiles;

namespace NoMercy.Encoder.Reconciliation;

/// <summary>
/// Everything <see cref="IEncodeReconciler.Decide"/> needs to make a
/// skip/partial/full call. Pure data — no I/O, no storage handle — so the
/// decision itself stays a plain function callers can unit test without a
/// filesystem or a database.
/// </summary>
/// <param name="Profile">The RESOLVED profile about to be applied.</param>
/// <param name="IsSingleFileOutput">
/// True for single-file containers (MKV/MP4/audio-only) whose strategies
/// never decompose past a single <c>Whole</c> task — reconciliation for
/// those can only ever be Skip or Full, never Partial.
/// </param>
/// <param name="BitmapSubtitleStreamCount">
/// Number of bitmap (PGS/VobSub/DVB) subtitle streams on the SOURCE file that
/// need an OCR-generated WebVTT sidecar. Zero when the source has none.
/// </param>
/// <param name="Existing">What is already on disk / in the manifest.</param>
/// <param name="Force">
/// Operator escape hatch — when true, reconciliation is skipped entirely and
/// the file is always fully re-encoded, regardless of fingerprint or what is
/// already present.
/// </param>
/// <param name="SourceChapterCount">
/// Number of chapters on the SOURCE file. FinalizeStage only writes chapters.vtt
/// when the source has at least one, so expecting the file from a source with
/// none asks for something no encode will ever produce — the media reads as
/// incomplete forever and re-encodes on every dispatch. Zero when the source has
/// no chapters, which for a short is the normal case.
/// </param>
public sealed record ReconciliationInput(
    EncodingProfile Profile,
    bool IsSingleFileOutput,
    int BitmapSubtitleStreamCount,
    ExistingOutputSnapshot Existing,
    bool Force = false,
    int SourceChapterCount = 0
);
