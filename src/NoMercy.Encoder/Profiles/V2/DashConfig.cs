namespace NoMercy.Encoder.Profiles.V2;

public record DashConfig(
    int MinBufferTimeSeconds = 4,
    bool SegmentTemplate = true, // SegmentTemplate vs SegmentList
    bool UseTimeline = true, // SegmentTimeline inside SegmentTemplate
    int? MaxSegmentDurationSeconds = null, // override vs profile.SegmentDurationSeconds
    string? Profile = "urn:mpeg:dash:profile:isoff-live:2011"
);
