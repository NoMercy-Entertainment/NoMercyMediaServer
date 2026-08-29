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

[PrimaryKey(nameof(LibraryId), nameof(TvId))]
[Index(nameof(LibraryId))]
[Index(nameof(TvId))]
public class LibraryTv
{
    [JsonProperty("library_id")]
    public Ulid LibraryId { get; set; }
    public Library Library { get; set; } = null!;

    [JsonProperty("tv_id")]
    public int TvId { get; set; }
    public Tv Tv { get; set; } = null!;

    /// <summary>
    /// Why this show is in this library: <see cref="LibraryLinkOrigin.Manual" />
    /// when someone asked for it, <see cref="LibraryLinkOrigin.File" /> when a
    /// file on disk brought it in.
    /// <para>
    /// Nothing recorded the difference, so nothing downstream could tell a show
    /// the owner added from one a scan attached on a guess. The listing worked
    /// around that by filtering on files that exist, which is why a show added a
    /// moment ago was invisible until something downloaded for it - the one day
    /// seeing it matters most.
    /// </para>
    /// </summary>
    [JsonProperty("added_by")]
    public string AddedBy { get; set; } = LibraryLinkOrigin.File;

    [JsonProperty("added_at")]
    public DateTime? AddedAt { get; set; }

    public LibraryTv()
    {
        //
    }

    public LibraryTv(Ulid libraryId, int tvId, string? addedBy = null)
    {
        LibraryId = libraryId;
        TvId = tvId;
        AddedBy = addedBy ?? LibraryLinkOrigin.File;
        AddedAt = DateTime.UtcNow;
    }
}
