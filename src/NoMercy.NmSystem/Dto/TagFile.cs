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

using Newtonsoft.Json;
using TagLib;
using FileTag = TagLib.File;

namespace NoMercy.NmSystem.Dto;

public class TagFile
{
    public static TagFile Create(string path)
    {
        using FileTag? fileTag = FileTag.Create(path: path);
        fileTag.Tag.Pictures = [];
        return new() { Tag = fileTag.Tag, Properties = fileTag.Properties };
    }

    [JsonIgnore]
    public Tag? Tag { get; set; }
    public Properties? Properties { get; set; }
}
