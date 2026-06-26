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

public record EncodingProfile(
    Ulid Id,
    string Name,
    Container Container,
    VideoOutput? Video,
    AudioOutput[] Audio,
    SubtitleOutput[] Subtitles,
    ThumbnailOutput? Thumbnails = null,
    LadderConfig? Ladder = null,
    HlsConfig? Hls = null,
    HlsDerivatives? HlsDerivatives = null,
    DashConfig? Dash = null,
    DrmConfig? Drm = null,
    int SegmentDurationSeconds = 6,
    EncodeMode EncodeMode = EncodeMode.SinglePass,
    bool AutoDetectCrop = false,
    int SchemaVersion = 2
)
{
    public string? Description { get; init; }
    public bool IsBuiltin { get; init; }

    // Default to PreferHardware so any preset (builtin or backfilled V1) hits
    // NVENC/AMF/QSV/VideoToolbox when one is present. Presets that need CPU
    // quality (Compress HEVC MKV, archival) opt into PreferQuality explicitly.
    public HardwarePreference HardwarePreference { get; init; } = HardwarePreference.PreferHardware;
    public BitDepthPolicy BitDepthPolicy { get; init; } = BitDepthPolicy.WarnAndDowngrade;
    public HdrPolicy HdrPolicy { get; init; } = HdrPolicy.PassthroughWhenPossible;
    public HdrOptions? HdrOptions { get; init; }
    public ClientCompatibility ClientCompatibility { get; init; } = ClientCompatibility.Universal;
    public SubtitleAcquisitionConfig? SubtitleAcquisition { get; init; }
    public Dictionary<string, string>? CustomArguments { get; init; }

    // Near-lossless intermediate config for the distributed-encode derivation
    // path. Null (default) = no mezzanine. When the planner derives rungs across
    // a worker boundary it falls back to this default if a profile opts in
    // without specifying one. See MezzanineConfig.
    public MezzanineConfig? Mezzanine { get; init; }
}
