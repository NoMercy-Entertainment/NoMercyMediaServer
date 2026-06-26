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

namespace NoMercy.Events.Encoding;

public sealed class EncodingStageChangedEvent : EventBase
{
    public override string Source => "Encoder";

    public required int JobId { get; init; }
    public required string Status { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public string BaseFolder { get; init; } = string.Empty;
    public string ShareBasePath { get; init; } = string.Empty;
    public List<string> VideoStreams { get; init; } = [];
    public List<string> AudioStreams { get; init; } = [];
    public List<string> SubtitleStreams { get; init; } = [];
    public bool HasGpu { get; init; }
    public bool IsHdr { get; init; }
}
