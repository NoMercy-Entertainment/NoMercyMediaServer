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

using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models.Libraries;

[PrimaryKey(nameof(Id))]
[Index(nameof(Status))]
[Index(nameof(DetectedType))]
[Index(nameof(CreatedAt))]
public class InboxItem : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Ulid Id { get; set; }

    [JsonProperty("source_path")]
    public string SourcePath { get; set; } = string.Empty;

    [JsonProperty("driver_id")]
    public Ulid DriverId { get; set; }

    /// movie, tv, anime, music, unknown
    [JsonProperty("detected_type")]
    public string DetectedType { get; set; } = "unknown";

    /// high, medium, low
    [JsonProperty("confidence")]
    public string Confidence { get; set; } = "low";

    /// NeedsReview, Routing, Imported, Encoding, Done, Failed, Dismissed
    [JsonProperty("status")]
    public string Status { get; set; } = "NeedsReview";

    /// JSON array of CandidateMatch. Stored as text; exceeds the global 256 cap (see MediaContext config).
    [Column("Candidates")]
    [JsonIgnore]
    public string CandidatesJson { get; set; } = "[]";

    [NotMapped]
    [JsonProperty("candidates")]
    public CandidateMatch[] Candidates
    {
        get => JsonConvert.DeserializeObject<CandidateMatch[]>(CandidatesJson) ?? [];
        set => CandidatesJson = JsonConvert.SerializeObject(value);
    }

    [Column("SelectedMatch")]
    [JsonIgnore]
    public string? SelectedMatchJson { get; set; }

    [NotMapped]
    [JsonProperty("selected_match")]
    public CandidateMatch? SelectedMatch
    {
        get =>
            SelectedMatchJson is null
                ? null
                : JsonConvert.DeserializeObject<CandidateMatch>(SelectedMatchJson);
        set => SelectedMatchJson = value is null ? null : JsonConvert.SerializeObject(value);
    }

    [JsonProperty("target_library_id")]
    public Ulid? TargetLibraryId { get; set; }

    [JsonProperty("target_folder_id")]
    public Ulid? TargetFolderId { get; set; }

    [JsonProperty("target_profile_id")]
    public Ulid? TargetProfileId { get; set; }

    [JsonProperty("error")]
    public string? Error { get; set; }
}
