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

using NoMercy.Encoder.Errors;

namespace NoMercy.Encoder.LiveTranscode.Protocol;

public record SessionCreatedMessage(
    string SessionId,
    double DurationSeconds,
    LiveQuality[] AvailableQualities,
    LiveQuality SelectedQuality,
    string FirstSegmentUrl
);

public record SegmentReadyMessage(
    int Index,
    double StartTimeSeconds,
    double DurationSeconds,
    string RelativeUrl,
    long SizeBytes
);

public record SeekCompletedMessage(
    double NewPositionSeconds,
    int FirstSegmentIndex,
    string SeekEpoch = ""
);

public record QualityChangedMessage(
    LiveQuality NewQuality,
    QualityChangeReason Reason,
    string SeekEpoch = ""
);

public enum QualityChangeReason
{
    UserRequested,
    AutoAdaptive,
    HardwareLimited,
    GpuFallbackToCpu,
}

public record TranscodeStateMessage(
    double Speed,
    double BufferAheadSeconds,
    LiveSessionState State
);

public record TranscodeErrorMessage(EncodingErrorKind Kind, string Message, bool Recoverable);

public record SessionEndedMessage(SessionEndReason Reason);

public enum SessionEndReason
{
    ClientDisconnected,
    Completed,
    Error,
    ServerShutdown,
}
