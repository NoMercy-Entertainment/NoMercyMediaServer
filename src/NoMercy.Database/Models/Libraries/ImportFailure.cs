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
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models.Libraries;

/// Dead-letter record for media imports that failed after all retries.
/// Surfaced to users via GET /api/v1/libraries/{libraryId}/import-failures.
[PrimaryKey(nameof(Id))]
[Index(nameof(JobType))]
[Index(nameof(LibraryId))]
[Index(nameof(Resolved))]
[Index(nameof(CreatedAt))]
public class ImportFailure : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    /// The thing that failed to import: a file path, TMDB id, MusicBrainz release id, etc.
    [MaxLength(1024)]
    [JsonProperty("file_path")]
    public string FilePath { get; set; } = string.Empty;

    /// "MovieImportJob", "ShowImportJob", "AudioImportJob".
    [JsonProperty("job_type")]
    public string JobType { get; set; } = string.Empty;

    [MaxLength(4096)]
    [JsonProperty("error_message")]
    public string ErrorMessage { get; set; } = string.Empty;

    [JsonProperty("retry_count")]
    public int RetryCount { get; set; }

    [JsonProperty("last_attempt_at")]
    public DateTimeOffset LastAttemptAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonProperty("resolved")]
    public bool Resolved { get; set; }

    [JsonProperty("library_id")]
    public Ulid? LibraryId { get; set; }
}
