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

namespace NoMercy.Database.Models.Media;

/// <summary>
/// Append-only record of every successful encode. The dashboard reads from
/// this table to show users what was encoded, when, how long it took, and
/// how much space it reclaimed (or spent). Writes happen from the encoding
/// orchestrator on EncodingResult.Success == true.
/// </summary>
[PrimaryKey(propertyName: nameof(Id))]
[Index(propertyName: nameof(CreatedAt))]
[Index(propertyName: nameof(ProfileId))]
public class EncodingHistory
{
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.None)]
    [JsonProperty(propertyName: "id")]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    [JsonProperty(propertyName: "input_path")]
    [MaxLength(length: 4096)]
    public required string InputPath { get; set; }

    [JsonProperty(propertyName: "output_path")]
    [MaxLength(length: 4096)]
    public required string OutputPath { get; set; }

    /// <summary>Profile that produced this encode — denormalized so history
    /// survives profile deletion.</summary>
    [JsonProperty(propertyName: "profile_id")]
    public Ulid? ProfileId { get; set; }

    [JsonProperty(propertyName: "profile_name")]
    [MaxLength(length: 256)]
    public required string ProfileName { get; set; }

    /// <summary>FFmpeg encoder used (libx264, h264_nvenc, …).</summary>
    [JsonProperty(propertyName: "encoder_used")]
    [MaxLength(length: 64)]
    public required string EncoderUsed { get; set; }

    [JsonProperty(propertyName: "gpu_used")]
    [MaxLength(length: 64)]
    public string? GpuUsed { get; set; }

    /// <summary>Total wall-clock duration of the encode.</summary>
    [JsonProperty(propertyName: "duration_seconds")]
    public double DurationSeconds { get; set; }

    [JsonProperty(propertyName: "input_size_bytes")]
    public long InputSizeBytes { get; set; }

    [JsonProperty(propertyName: "output_size_bytes")]
    public long OutputSizeBytes { get; set; }

    /// <summary>output/input ratio; &lt; 1 means the encode reclaimed space.
    /// 0 when input size is unknown.</summary>
    [JsonProperty(propertyName: "compression_ratio")]
    public double CompressionRatio { get; set; }

    [JsonProperty(propertyName: "average_speed")]
    public double AverageSpeed { get; set; }

    [JsonProperty(propertyName: "average_fps")]
    public double AverageFps { get; set; }

    [JsonProperty(propertyName: "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
