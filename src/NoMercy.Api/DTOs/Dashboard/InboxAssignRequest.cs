using Newtonsoft.Json;
using NoMercy.Database.Models.Libraries;

namespace NoMercy.Api.DTOs.Dashboard;

public sealed class InboxAssignRequest
{
    [JsonProperty("type")]
    public required string Type { get; set; }

    [JsonProperty("match")]
    public required CandidateMatch Match { get; set; }

    [JsonProperty("target_library_id")]
    public required Ulid TargetLibraryId { get; set; }

    [JsonProperty("target_folder_id")]
    public required Ulid TargetFolderId { get; set; }

    [JsonProperty("target_profile_id")]
    public required Ulid TargetProfileId { get; set; }
}
