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

namespace NoMercy.Encoder.BuildingBlocks.Drm;

/// <summary>
/// Which DRM scheme to apply. <see cref="Aes128"/> is the baseline for HLS.
/// <see cref="Cenc"/> applies raw-key CENC encryption to DASH outputs via
/// shaka-packager and produces PSSH boxes for Widevine/PlayReady/FairPlay
/// without requiring a cloud key server.
/// </summary>
public enum DrmMethod
{
    None,
    Aes128,
    Cenc,
}

/// <summary>
/// Per-profile DRM configuration.
///
/// For <see cref="DrmMethod.Aes128"/>: <see cref="KeyUri"/> is the URL HLS clients
/// hit to fetch the decryption key. The server returns the raw 16-byte key to
/// authenticated players.
///
/// For <see cref="DrmMethod.Cenc"/>: <see cref="CencKeys"/> holds one entry per
/// stream label (e.g. "SD", "HD"). Each entry contains a 16-byte key and a
/// 16-byte key_id used by shaka-packager's raw-key mode. No cloud key server
/// is required for packaging; a license server endpoint is wired separately
/// (Phase 11.2b/c).
///
/// The encoder never owns key generation policy: it uses the keys it's given
/// and writes packaging manifests. Licensing / rotation is a server concern.
/// </summary>
public record DrmConfig(
    DrmMethod Method,
    string KeyUri,
    byte[]? Key = null,
    byte[]? Iv = null,
    IReadOnlyList<CencKeyEntry>? CencKeys = null
);

/// <summary>
/// A single raw-key entry for CENC packaging. <see cref="Label"/> matches the
/// shaka-packager <c>drm_label</c> on the stream (e.g. "SD", "HD", "AUDIO").
/// <see cref="KeyId"/> and <see cref="Key"/> are each 16 bytes.
/// </summary>
public record CencKeyEntry(string Label, byte[] KeyId, byte[] Key);
