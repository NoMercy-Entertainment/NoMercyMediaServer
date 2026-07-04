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

public sealed class EncodingProgressUpdatedEvent : EventBase
{
    public override string Source => "Encoder";

    public required int JobId { get; init; }
    public required double Percentage { get; init; }
    public TimeSpan Elapsed { get; init; }
    public TimeSpan? Estimated { get; init; }
    public double? Fps { get; init; }
    public double? Speed { get; init; }
    public int? BitrateKbps { get; init; }
}
