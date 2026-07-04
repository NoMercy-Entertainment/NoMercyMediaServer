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
using NoMercy.Api.Controllers.V1.Streaming.Dtos;

namespace NoMercy.Api.Services;

/// <summary>
/// Live-transcode orchestration lifted out of
/// <c>LiveTranscodeController</c>: session lifecycle, playlist/segment
/// production, quality changes and seeking. The controller keeps only auth
/// checks and HTTP result mapping.
/// </summary>
public interface ILiveTranscodeService
{
    IReadOnlyList<LiveSessionDto> ListSessions();

    Task<LiveResult> StartSessionAsync(
        Guid userId,
        StartLiveSessionRequest request,
        string? deviceId,
        CancellationToken ct
    );

    LiveResult GetPlaylist(string sessionId);

    LiveResult GetSegment(string sessionId, string epoch, int index);

    LiveResult ReportPosition(string sessionId, ReportPositionRequest request);

    Task<LiveResult> ChangeQualityAsync(
        string sessionId,
        ChangeQualityRequest request,
        CancellationToken ct
    );

    Task<LiveResult> SeekAsync(string sessionId, SeekRequest request, CancellationToken ct);

    Task EndSessionAsync(string sessionId, CancellationToken ct);
}
