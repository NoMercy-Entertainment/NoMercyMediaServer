namespace NoMercy.Tests.Encoder.V3.Jobs;

using Newtonsoft.Json;
using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Jobs;
using NoMercy.Encoder.V3.Profiles;

public class JobCheckpointTests
{
    [Fact]
    public void JobCheckpoint_RoundTrips_ThroughJson()
    {
        JobCheckpoint checkpoint = new(
            JobId: "job-abc-123",
            InputPath: "/media/source/movie.mkv",
            OutputDirectory: "/output/movie",
            CompletedGroupIndices: [0, 1, 2],
            LastUpdated: new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc)
        );

        string json = JsonConvert.SerializeObject(checkpoint);
        JobCheckpoint? deserialized = JsonConvert.DeserializeObject<JobCheckpoint>(json);

        deserialized.Should().NotBeNull();
        deserialized!.JobId.Should().Be("job-abc-123");
        deserialized.InputPath.Should().Be("/media/source/movie.mkv");
        deserialized.OutputDirectory.Should().Be("/output/movie");
        deserialized.CompletedGroupIndices.Should().BeEquivalentTo(new[] { 0, 1, 2 });
        deserialized
            .LastUpdated.Should()
            .Be(new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void JobCheckpoint_WithNoCompletedGroups_IsValid()
    {
        JobCheckpoint checkpoint = new(
            JobId: "fresh-job",
            InputPath: "/media/source/show.mkv",
            OutputDirectory: "/output/show",
            CompletedGroupIndices: [],
            LastUpdated: DateTime.UtcNow
        );

        checkpoint.CompletedGroupIndices.Should().BeEmpty();
        checkpoint.JobId.Should().Be("fresh-job");
    }

    [Fact]
    public void JobCheckpoint_WithCompletedGroups_PreservesIndices()
    {
        int[] indices = [3, 7, 12, 15];

        JobCheckpoint checkpoint = new(
            JobId: "partial-job",
            InputPath: "/media/source/film.mkv",
            OutputDirectory: "/output/film",
            CompletedGroupIndices: indices,
            LastUpdated: DateTime.UtcNow
        );

        string json = JsonConvert.SerializeObject(checkpoint);
        JobCheckpoint? deserialized = JsonConvert.DeserializeObject<JobCheckpoint>(json);

        deserialized!.CompletedGroupIndices.Should().Equal(indices);
    }

    [Fact]
    public void EncodingJob_RoundTrips_ThroughJson()
    {
        EncodingProfile profile = new(
            Id: "hls-1080p",
            Name: "HLS 1080p",
            Format: OutputFormat.Hls,
            VideoOutputs: [],
            AudioOutputs: [],
            SubtitleOutputs: []
        );

        EncodingJob job = new(
            JobId: "encode-xyz-789",
            InputPath: "/media/source/movie.mkv",
            OutputDirectory: "/output/hls/movie",
            Profile: profile,
            Checkpoint: null
        );

        string json = JsonConvert.SerializeObject(job);
        EncodingJob? deserialized = JsonConvert.DeserializeObject<EncodingJob>(json);

        deserialized.Should().NotBeNull();
        deserialized!.JobId.Should().Be("encode-xyz-789");
        deserialized.InputPath.Should().Be("/media/source/movie.mkv");
        deserialized.OutputDirectory.Should().Be("/output/hls/movie");
        deserialized.Profile.Id.Should().Be("hls-1080p");
        deserialized.Checkpoint.Should().BeNull();
    }

    [Fact]
    public void EncodingJob_WithCheckpoint_RoundTrips_ThroughJson()
    {
        JobCheckpoint checkpoint = new(
            JobId: "encode-xyz-789",
            InputPath: "/media/source/movie.mkv",
            OutputDirectory: "/output/hls/movie",
            CompletedGroupIndices: [0],
            LastUpdated: new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc)
        );

        EncodingProfile profile = new(
            Id: "hls-1080p",
            Name: "HLS 1080p",
            Format: OutputFormat.Hls,
            VideoOutputs: [],
            AudioOutputs: [],
            SubtitleOutputs: []
        );

        EncodingJob job = new(
            JobId: "encode-xyz-789",
            InputPath: "/media/source/movie.mkv",
            OutputDirectory: "/output/hls/movie",
            Profile: profile,
            Checkpoint: checkpoint
        );

        string json = JsonConvert.SerializeObject(job);
        EncodingJob? deserialized = JsonConvert.DeserializeObject<EncodingJob>(json);

        deserialized!.Checkpoint.Should().NotBeNull();
        deserialized.Checkpoint!.CompletedGroupIndices.Should().Equal(0);
    }
}
