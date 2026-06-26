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

namespace NoMercy.Database;

public class ISubtitleProfile
{
    public string Codec { get; set; } = string.Empty;
    public string SegmentName { get; set; } = string.Empty;
    public string PlaylistName { get; set; } = string.Empty;
    public string[] AllowedLanguages { get; set; } = [];
    public string[] Opts { get; set; } = [];
    public (string key, string Val)[] CustomArguments { get; set; } = [];
}
