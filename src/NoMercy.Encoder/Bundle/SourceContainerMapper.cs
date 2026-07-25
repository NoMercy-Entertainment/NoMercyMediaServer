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

namespace NoMercy.Encoder.Bundle;

/// <summary>
/// Maps ffprobe's <c>format_name</c> (a comma-separated list of every muxer
/// that can read the container, e.g. <c>"matroska,webm"</c> or
/// <c>"mov,mp4,m4a,3gp,3g2,mj2"</c>) to the single container name a
/// reconstruction targets — see spec "encodes[]": <c>target_container</c> is
/// the SOURCE container a rebuild produces, never the HLS/fMP4 output.
/// </summary>
public static class SourceContainerMapper
{
    public static string Map(string formatName)
    {
        if (string.IsNullOrWhiteSpace(formatName))
            return "unknown";

        string first = formatName.Split(',')[0].Trim().ToLowerInvariant();

        return first switch
        {
            "matroska" or "webm" => "matroska",
            "mov" or "mp4" or "m4a" or "3gp" or "3g2" or "mj2" => "mp4",
            _ => first,
        };
    }
}
