using Newtonsoft.Json;
using NoMercy.Database.Models.Libraries;

namespace NoMercy.Api.DTOs.Dashboard;

public sealed class InboxItemDto
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("source_path")]
    public string SourcePath { get; set; }

    [JsonProperty("detected_type")]
    public string DetectedType { get; set; }

    [JsonProperty("confidence")]
    public string Confidence { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; }

    [JsonProperty("candidates")]
    public CandidateMatch[] Candidates { get; set; }

    [JsonProperty("selected_match")]
    public CandidateMatch? SelectedMatch { get; set; }

    [JsonProperty("target_library_id")]
    public string? TargetLibraryId { get; set; }

    [JsonProperty("target_folder_id")]
    public string? TargetFolderId { get; set; }

    [JsonProperty("target_profile_id")]
    public string? TargetProfileId { get; set; }

    [JsonProperty("error")]
    public string? Error { get; set; }

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty("updated_at")]
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
