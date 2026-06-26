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

namespace NoMercy.Encoder.Hardware;

public interface IFfmpegCapabilities
{
    IReadOnlySet<string> AvailableEncoders { get; }
    IReadOnlySet<string> AvailableDecoders { get; }
    IReadOnlySet<string> AvailableDemuxers { get; }
    IReadOnlySet<string> AvailableFilters { get; }
    IReadOnlySet<string> AvailableProtocols { get; }
    bool HasEncoder(string name);
    bool HasDemuxer(string name);
    bool HasFilter(string name);
    bool HasProtocol(string name);
    Task ProbeAsync(CancellationToken ct = default);
}
