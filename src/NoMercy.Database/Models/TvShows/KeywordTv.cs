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

using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models.TvShows;

[PrimaryKey(nameof(KeywordId), nameof(TvId))]
[Index(nameof(KeywordId))]
[Index(nameof(TvId))]
public class KeywordTv
{
    [JsonProperty("keyword_id")]
    public int KeywordId { get; set; }
    public Keyword Keyword { get; set; } = null!;

    [JsonProperty("tv_id")]
    public int TvId { get; set; }
    public Tv Tv { get; set; } = null!;
}
