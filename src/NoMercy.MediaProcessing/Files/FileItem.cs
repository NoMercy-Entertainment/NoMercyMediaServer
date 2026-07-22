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

using MovieFileLibrary;
using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.MediaProcessing.Files;

public class FileItem
{
    [JsonProperty(propertyName: "name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(propertyName: "mode")]
    public int Mode { get; set; }

    [JsonProperty(propertyName: "parent")]
    public string? Parent { get; set; }

    [JsonProperty(propertyName: "size")]
    public long Size { get; set; }

    [JsonProperty(propertyName: "parsed")]
    public MovieFile? Parsed { get; set; }

    [JsonProperty(propertyName: "match")]
    public MovieOrEpisode Match { get; set; } = new();

    [JsonProperty(propertyName: "streams")]
    public Streams Streams { get; set; } = new();

    [JsonProperty(propertyName: "path")]
    public string Path { get; set; } = string.Empty;

    [JsonProperty(propertyName: "tracks")]
    public int Tracks { get; set; }
}
