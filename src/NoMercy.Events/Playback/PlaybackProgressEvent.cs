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

namespace NoMercy.Events.Playback;

public sealed class PlaybackProgressEvent : EventBase
{
    public override string Source => "Playback";

    public required Guid UserId { get; init; }
    public required int MediaId { get; init; }
    public string? MediaIdentifier { get; init; }
    public required TimeSpan Position { get; init; }
    public required TimeSpan Duration { get; init; }
}
