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

namespace NoMercy.Encoder.Profiles;

/// <summary>
/// Complete per-stream decision plan produced by <see cref="PreviewEngine.Analyze"/>.
/// The dashboard renders each list as a colour-coded stream table so the user can
/// see exactly what will happen to every track before submitting the encode job.
/// </summary>
public sealed record PerStreamPlan(
    IReadOnlyList<VideoStreamAction> VideoStreams,
    IReadOnlyList<AudioStreamAction> AudioStreams,
    IReadOnlyList<SubtitleStreamAction> SubtitleStreams
);

/// <summary>
/// Planned action for one source video stream.
/// <see cref="Action"/> is a stable spec string: <c>copy | transcode | skip</c>.
/// <see cref="FilterChainSummary"/> is a human-readable summary of the filter graph
/// (e.g. <c>scale=1920:1080,format=yuv420p</c>) — null when action is <c>copy</c>.
/// <see cref="BitDepthDowngrade"/> is true when the source is 10-bit and the target
/// profile requests 8-bit, warning the user about quality loss.
/// </summary>
public sealed record VideoStreamAction(
    int SourceIndex,
    string Action,
    string Reason,
    object? Target,
    string? FilterChainSummary,
    bool BitDepthDowngrade
);

/// <summary>
/// Planned action for one source audio stream.
/// <see cref="Action"/> is a stable spec string: <c>copy | transcode | skip</c>.
/// </summary>
public sealed record AudioStreamAction(
    int SourceIndex,
    string Action,
    string Reason,
    object? Target
);

/// <summary>
/// Planned action for one source subtitle stream.
/// <see cref="Action"/> is a stable spec string:
/// <c>copy | transcode | skip | extract | extract_ocr | burn_in</c>.
/// </summary>
public sealed record SubtitleStreamAction(
    int SourceIndex,
    string Action,
    string Reason,
    object? Target
);
