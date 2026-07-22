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
using NoMercy.Database.Models.Libraries;

namespace NoMercy.Api.DTOs.Dashboard;

public sealed class InboxItemDto
{
    [JsonProperty(propertyName: "id")]
    public string Id { get; set; }

    [JsonProperty(propertyName: "source_path")]
    public string SourcePath { get; set; }

    [JsonProperty(propertyName: "detected_type")]
    public string DetectedType { get; set; }

    [JsonProperty(propertyName: "confidence")]
    public string Confidence { get; set; }

    [JsonProperty(propertyName: "status")]
    public string Status { get; set; }

    [JsonProperty(propertyName: "candidates")]
    public CandidateMatch[] Candidates { get; set; }

    [JsonProperty(propertyName: "selected_match")]
    public CandidateMatch? SelectedMatch { get; set; }

    [JsonProperty(propertyName: "target_library_id")]
    public string? TargetLibraryId { get; set; }

    [JsonProperty(propertyName: "target_folder_id")]
    public string? TargetFolderId { get; set; }

    [JsonProperty(propertyName: "target_profile_id")]
    public string? TargetProfileId { get; set; }

    [JsonProperty(propertyName: "error")]
    public string? Error { get; set; }

    [JsonProperty(propertyName: "created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty(propertyName: "updated_at")]
    public DateTime UpdatedAt { get; set; }

    public InboxItemDto(InboxItem item)
    {
        Id = item.Id.ToString();
        SourcePath = item.SourcePath;
        DetectedType = item.DetectedType;
        Confidence = item.Confidence;
        Status = item.Status;
        Candidates = item.Candidates;
        SelectedMatch = item.SelectedMatch;
        TargetLibraryId = item.TargetLibraryId?.ToString();
        TargetFolderId = item.TargetFolderId?.ToString();
        TargetProfileId = item.TargetProfileId?.ToString();
        Error = item.Error;
        CreatedAt = item.CreatedAt;
        UpdatedAt = item.UpdatedAt;
    }
}
