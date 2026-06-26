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

namespace NoMercy.Encoder.Naming;

/// <summary>
/// Resolved output paths for one (media, preset) combination. All paths are
/// relative to the media item's library folder unless flagged absolute.
/// </summary>
public record BundleLayout(
    string MediaKey,
    string PresetSlug,
    bool IsSingleFile,
    string BundleDirectory, // "encodes/{presetSlug}" or "" for single-file
    string MasterPlaylistName, // "{mediaKey}_master.m3u8" or "" for single-file
    string ManifestPath, // "{bundleDir}/manifest.json" or "{singleFile}.manifest.json"
    string ReconstructionPath, // "{bundleDir}/reconstruction.json" or "{singleFile}.reconstruction.json"
    string SingleFileName, // "{Title}.NoMercy.{ext}" or "" for HLS
    // Preset metadata forwarded from EncodingProfile so FinalizeStage can
    // write a complete manifest.json without re-resolving the profile.
    string PresetId = "",
    string PresetName = "",
    string ContainerString = "" // e.g. "hls-fmp4", "mkv"
);
