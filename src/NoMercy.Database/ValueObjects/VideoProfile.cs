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

public class VideoProfile
{
    public string Codec { get; set; } = string.Empty;
    public int Bitrate { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int Framerate { get; set; }
    public string Preset { get; set; } = string.Empty;
    public string Profile { get; set; } = string.Empty;
    public string Tune { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string SegmentName { get; set; } = string.Empty;
    public string PlaylistName { get; set; } = string.Empty;
    public string ColorSpace { get; set; } = string.Empty;
    public int Crf { get; set; }
    public int KeyInt { get; set; }
    public string[] Opts { get; set; } = [];
    public (string key, string Val)[] CustomArguments { get; set; } = [];
    public bool ConvertHdrToSdr { get; set; }
}
