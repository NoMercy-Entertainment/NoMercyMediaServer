using Newtonsoft.Json;

namespace NoMercy.EncoderV2.Jobs;

[JsonObject(ItemTypeNameHandling = TypeNameHandling.Auto)]
public record EncodingJobPayload
{
    [JsonProperty("job_id")]
    public string JobId { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("job_type")]
    public EncodingJobType JobType { get; set; } = EncodingJobType.Video;

    [JsonProperty("media_type")]
    public string MediaType { get; set; } = string.Empty; // "video" or "audio"

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Input specification
    [JsonProperty("input")]
    public EncodingJobInput Input { get; set; } = new();

    // Output specification
    [JsonProperty("output")]
    public EncodingJobOutput Output { get; set; } = new();

    // Profile snapshot (self-contained)
    [JsonProperty("profile")]
    public EncodingProfile? Profile { get; set; }

    // Progress and status
    [JsonProperty("status")]
    public EncodingJobStatus Status { get; set; } = new();

    // Convenience properties for compatibility with EncodeMediaJob
    [JsonIgnore]
    public string InputFilePath => Input.FilePath;

    [JsonIgnore]
    public string OutputPath => Output.DestinationFolder;

    [JsonIgnore]
    public string VideoCodec => Profile?.VideoProfile?.Codec ?? "h264";

    [JsonIgnore]
    public string AudioCodec => Profile?.AudioProfile?.Codec ?? "aac";

    [JsonIgnore]
    public string SubtitleCodec => Profile?.SubtitleProfile?.Codec ?? "webvtt";

    [JsonIgnore]
    public string Container => Profile?.Container ?? "mp4";

    [JsonIgnore]
    public long TotalFrames => (long)(Input.Duration.TotalSeconds * 30); // Rough estimate, should be from ffprobe

    [JsonIgnore]
    public double TotalDuration => Input.Duration.TotalSeconds;
}

public record EncodingJobInput
{
    [JsonProperty("file_path")]
    public string FilePath { get; set; } = string.Empty;

    [JsonProperty("network_path")]
    public string? NetworkPath { get; set; } // UNC path for thin clients (\\server\share\file.mkv)

    [JsonProperty("file_hash")]
    public string FileHash { get; set; } = string.Empty;

    [JsonProperty("file_size")]
    public long FileSize { get; set; }

    [JsonProperty("duration")]
    public TimeSpan Duration { get; set; }
}

public record EncodingJobOutput
{
    [JsonProperty("destination_folder")]
    public string DestinationFolder { get; set; } = string.Empty;

    [JsonProperty("file_name")]
    public string FileName { get; set; } = string.Empty;

    [JsonProperty("thumbnail_folder")]
    public string? ThumbnailFolder { get; set; }

    [JsonProperty("chapter_extract_folder")]
    public string? ChapterExtractFolder { get; set; }

    [JsonProperty("font_extract_folder")]
    public string? FontExtractFolder { get; set; }

    [JsonProperty("generated_files")]
    public List<string> GeneratedFiles { get; set; } = [];
}

public record EncodingJobStatus
{
    [JsonProperty("state")]
    public string State { get; set; } = "pending"; // pending, encoding, completed, failed

    [JsonProperty("error_message")]
    public string? ErrorMessage { get; set; }

    [JsonProperty("progress_percentage")]
    public double ProgressPercentage { get; set; }

    [JsonProperty("progress")]
    public double Progress => ProgressPercentage;

    [JsonProperty("current_time")]
    public TimeSpan? CurrentTime { get; set; }

    [JsonProperty("estimated_remaining")]
    public TimeSpan? EstimatedRemaining { get; set; }

    [JsonProperty("fps")]
    public double? Fps { get; set; }

    [JsonProperty("current_fps")]
    public double CurrentFps => Fps ?? 0;

    [JsonProperty("speed")]
    public double? Speed { get; set; }

    [JsonProperty("current_bitrate")]
    public string? CurrentBitrate { get; set; }

    [JsonProperty("started_at")]
    public DateTime? StartedAt { get; set; }

    [JsonProperty("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [JsonProperty("execution_command")]
    public string ExecutionCommand { get; set; } = string.Empty;

    [JsonProperty("encoded_frames")]
    public long EncodedFrames { get; set; }

    [JsonProperty("output_size")]
    public long OutputSize { get; set; }

    [JsonProperty("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();

    [JsonProperty("applied_rules")]
    public List<string> AppliedRules { get; set; } = [];
}
