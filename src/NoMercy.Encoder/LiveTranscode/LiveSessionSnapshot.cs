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

namespace NoMercy.Encoder.LiveTranscode;

/// <summary>
/// Immutable point-in-time snapshot of a live session's observable state.
/// Exposed via GET /api/v1/streaming/live/sessions for admin UI polling.
/// Does NOT include cancellation tokens, scratch paths, or internal mutable
/// fields — only information safe to hand to the HTTP layer.
/// </summary>
public sealed record LiveSessionSnapshot(
    string SessionId,
    LiveSessionState State,
    string QualityId,
    string QualityLabel,
    int Width,
    int Height,
    int BitrateKbps,
    double PositionSeconds,
    double BufferAheadSeconds,
    int SegmentCount,
    bool IsComplete,
    DateTime LastAccess
);
