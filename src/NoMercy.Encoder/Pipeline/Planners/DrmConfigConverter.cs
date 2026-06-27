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

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Codecs.Definitions;
using NoMercy.Encoder.ContentAnalysis;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Hdr;
using NoMercy.Encoder.Naming;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline.Optimizer;
using NoMercy.Encoder.Profiles;
using NoMercy.Encoder.Subtitles;
using LegacyDrmConfig = NoMercy.Encoder.BuildingBlocks.Drm.DrmConfig;
using LegacyDrmMethod = NoMercy.Encoder.BuildingBlocks.Drm.DrmMethod;

namespace NoMercy.Encoder.Pipeline.Stages;

/// <summary>
/// Converts a V2 <see cref="DrmConfig"/> into the legacy DRM config shape
/// consumed by the output pipeline. Extracted from PlanStage.
/// </summary>
public static class DrmConfigConverter
{
    public static LegacyDrmConfig? Convert(DrmConfig? v2Drm)
    {
        if (v2Drm is null)
            return null;

        LegacyDrmMethod method = v2Drm.Scheme.ToLowerInvariant() switch
        {
            "aes128" or "aes-128" => LegacyDrmMethod.Aes128,
            "cenc" => LegacyDrmMethod.Cenc,
            _ => LegacyDrmMethod.None,
        };

        string keyUri = v2Drm.Parameters?.GetValueOrDefault("key_uri") ?? string.Empty;

        return new LegacyDrmConfig(Method: method, KeyUri: keyUri);
    }
}
