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

using NoMercy.Encoder.Codecs;

namespace NoMercy.Encoder.LiveTranscode;

/// <summary>
/// Per-codec device decode capability, replacing the flat global-boolean shape
/// (<see cref="SupportedVideoCodecs"/>/<see cref="Supports10Bit"/> below, kept only
/// for older client builds). A device that decodes HEVC Main10 but not AVC High10
/// now has a way to say so; the legacy fields could not express that distinction.
/// </summary>
public record ClientCapabilities(
    VideoCodecCapability[]? Video = null,
    AudioCodecCapability[]? Audio = null,
    string[]? SupportedContainers = null,
    bool SupportsHdr = false,
    int MaxBitrateKbps = 0,
    // Legacy fields — see PlaybackDecisionEngine's legacy-payload synthesis.
    // Nullable: absent on new payloads, populated when an older client build
    // still sends the flat shape.
    VideoCodecType[]? SupportedVideoCodecs = null,
    AudioCodecType[]? SupportedAudioCodecs = null,
    int? MaxWidth = null,
    int? MaxHeight = null,
    bool? Supports10Bit = null,
    // Orthogonal to per-codec decode capability: caps the ABR ladder's audio
    // channel count in DeviceAwareVariantSelector, not covered by the
    // per-codec redesign (variant-ladder selection, not decode gating).
    int MaxAudioChannels = 2
)
{
    public VideoCodecCapability[] Video { get; init; } = Video ?? [];
    public AudioCodecCapability[] Audio { get; init; } = Audio ?? [];
    public string[] SupportedContainers { get; init; } = SupportedContainers ?? [];
}

public record VideoCodecCapability(
    VideoCodecType Codec,
    string[] Profiles,
    int MaxBitDepth,
    int MaxWidth,
    int MaxHeight,
    int MaxFramerate,
    string[] HdrFormats,
    int MaxBitrateKbps
);

public record AudioCodecCapability(
    AudioCodecType Codec,
    int MaxChannels,
    bool Passthrough,
    bool Decode
);
