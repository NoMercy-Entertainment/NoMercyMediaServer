using Newtonsoft.Json;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Jobs;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Jobs;

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
            LastUpdated: new(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc)
        );

        string json = JsonConvert.SerializeObject(checkpoint);
        JobCheckpoint? deserialized = JsonConvert.DeserializeObject<JobCheckpoint>(json);

        deserialized.Should().NotBeNull();
        deserialized!.JobId.Should().Be("job-abc-123");
        deserialized.InputPath.Should().Be("/media/source/movie.mkv");
        deserialized.OutputDirectory.Should().Be("/output/movie");
        deserialized.CompletedGroupIndices.Should().BeEquivalentTo(new[] { 0, 1, 2 });
        deserialized.LastUpdated.Should().Be(new(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc));
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
        Ulid profileId = Ulid.NewUlid();

        EncodingProfile profile = new(
            Id: profileId,
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
            Checkpoint: null,
            CreatedAtUtc: DateTime.UtcNow
        );

        string json = JsonConvert.SerializeObject(job);
        EncodingJob? deserialized = JsonConvert.DeserializeObject<EncodingJob>(json);

        deserialized.Should().NotBeNull();
        deserialized!.JobId.Should().Be("encode-xyz-789");
        deserialized.InputPath.Should().Be("/media/source/movie.mkv");
        deserialized.OutputDirectory.Should().Be("/output/hls/movie");
        deserialized.Profile.Id.Should().Be(profileId);
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
            LastUpdated: new(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc)
        );

        EncodingProfile profile = new(
            Id: Ulid.NewUlid(),
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
            Checkpoint: checkpoint,
            CreatedAtUtc: DateTime.UtcNow
        );

        string json = JsonConvert.SerializeObject(job);
        EncodingJob? deserialized = JsonConvert.DeserializeObject<EncodingJob>(json);

        deserialized!.Checkpoint.Should().NotBeNull();
        deserialized.Checkpoint!.CompletedGroupIndices.Should().Equal(0);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Phase 4.13 — resume / cancel fields. Pipeline integration (write on
    // ffmpeg crash, resume seek-to-keyframe, finalize cleanup, cancellation
    // SIGTERM/SIGKILL grace) lands as a follow-up; this set guarantees the
    // record itself preserves the new shape end-to-end.
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void JobCheckpoint_NewFields_DefaultToSafeValues()
    {
        JobCheckpoint cp = new(
            JobId: "j-1",
            InputPath: "/in/source.mkv",
            OutputDirectory: "/out/j-1",
            CompletedGroupIndices: [],
            LastUpdated: DateTime.UtcNow
        );

        cp.VariantId.Should().BeEmpty();
        cp.LastProgressMs.Should().Be(0);
        cp.LastFfmpegStderrTail.Should().BeEmpty();
        cp.FailedAt.Should().BeNull();
    }

    [Fact]
    public void JobCheckpoint_NewFields_RoundTripThroughJson()
    {
        DateTime failedAt = new(2026, 4, 25, 12, 34, 56, DateTimeKind.Utc);
        JobCheckpoint original = new(
            JobId: "j-1",
            InputPath: "/in/source.mkv",
            OutputDirectory: "/out/j-1",
            CompletedGroupIndices: [0, 1, 2],
            LastUpdated: DateTime.UtcNow,
            VariantId: "1080p",
            LastProgressMs: 12_345,
            LastFfmpegStderrTail: "frame=890 fps=42 time=00:00:30 bitrate=4500kbits/s",
            FailedAt: failedAt
        );

        string json = JsonConvert.SerializeObject(original);
        JobCheckpoint? roundTripped = JsonConvert.DeserializeObject<JobCheckpoint>(json);

        roundTripped.Should().NotBeNull();
        roundTripped!.VariantId.Should().Be("1080p");
        roundTripped.LastProgressMs.Should().Be(12_345);
        roundTripped.LastFfmpegStderrTail.Should().Contain("frame=890");
        roundTripped.FailedAt.Should().Be(failedAt);
    }

    [Fact]
    public void JobCheckpoint_LegacyJson_LoadsWithDefaultedNewFields()
    {
        // Simulates a checkpoint written by a pre-Phase-4.13 build. Older
        // self-hosted servers must keep resuming jobs after upgrade —
        // missing fields take the record's positional defaults.
        const string legacyJson = """
            {
                "JobId": "legacy-1",
                "InputPath": "/in/source.mkv",
                "OutputDirectory": "/out/legacy-1",
                "CompletedGroupIndices": [0, 1],
                "LastUpdated": "2026-04-20T12:00:00Z",
                "Pass1Completed": true,
                "LastCompletedSegment": 5,
                "EncodeMode": "two-pass"
            }
            """;

        JobCheckpoint? loaded = JsonConvert.DeserializeObject<JobCheckpoint>(legacyJson);

        loaded.Should().NotBeNull();
        loaded!.JobId.Should().Be("legacy-1");
        loaded.LastCompletedSegment.Should().Be(5);
        loaded.EncodeMode.Should().Be("two-pass");
        // Newtonsoft.Json populates string fields not present in the JSON
        // payload as null instead of running the record's positional
        // default. The store treats null + empty as equivalent for the
        // resume path; both signal "no data carried over from before."
        (loaded.VariantId ?? string.Empty)
            .Should()
            .BeEmpty();
        loaded.LastProgressMs.Should().Be(0);
        (loaded.LastFfmpegStderrTail ?? string.Empty).Should().BeEmpty();
        loaded.FailedAt.Should().BeNull();
    }
}
