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

namespace NoMercy.Encoder.Devices;

public record VariantSelection(
    int? VariantIndex, // null = no existing variant fits; transcode required
    DeviceCapabilities? AppliedCapabilities, // null = no constraints applied (no caps declared)
    AudioConstraint? AudioConstraint, // non-null when transcode must downmix
    VideoConstraint? VideoConstraint, // non-null when transcode must downscale or transcode codec
    string? Reason // human-readable why-this-was-selected, for logs / dashboard
);

public record AudioConstraint(int Channels, string Codec);

public record VideoConstraint(int? MaxHeight, string? Codec);

public record VariantDescriptor(
    int Index,
    int Height,
    int Width,
    string VideoCodec,
    int VideoBitrateKbps,
    int AudioChannels,
    string AudioCodec
);
