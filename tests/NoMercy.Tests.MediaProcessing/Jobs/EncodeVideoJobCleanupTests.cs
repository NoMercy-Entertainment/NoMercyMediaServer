using NoMercy.Encoder.Format.Audio;
using NoMercy.Encoder.Format.Container;
using NoMercy.MediaProcessing.Jobs.MediaJobs;

namespace NoMercy.Tests.MediaProcessing.Jobs;

public class VideoEncodeJobCleanupTests : IDisposable
{
    private readonly string _testDir;

    public VideoEncodeJobCleanupTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "NoMercy_Test_" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a container whose GetOutputSubdirectories() yields the given
    /// subdirectory names.  We drive this through BaseAudio.HlsSegmentFilename
    /// because that is the path GetOutputSubdirectories reads for audio streams.
    /// </summary>
    private static BaseContainer ContainerWithSubdirs(params string[] subdirNames)
    {
        BaseContainer container = new();

        foreach (string name in subdirNames)
        {
            // "name/segment%03d.ts" — Split('/')[0] == name
            BaseAudio audio = new() { HlsSegmentFilename = $"{name}/segment%03d.ts" };
            container.AudioStreams.Add(audio);
        }

        return container;
    }

    /// <summary>
    /// Returns a container with no streams and a FileName set, representing a
    /// non-HLS single-file output.
    /// </summary>
    private static BaseContainer ContainerWithFileName(string fileName)
    {
        return new BaseContainer { FileName = fileName };
    }

    /// <summary>
    /// Returns a container with no streams and no FileName — the no-op case.
    /// </summary>
    private static BaseContainer EmptyContainer()
    {
        return new BaseContainer();
    }

    // ── subdirectory-scoped cleanup ──────────────────────────────────────────

    [Fact]
    public void CleanupPartialOutput_RemovesOnlyReportedSubdirectory()
    {
        // Arrange — two subdirs inside basePath; container owns only "video"
        string videoDir = Path.Combine(_testDir, "video");
        string audioDir = Path.Combine(_testDir, "audio");

        Directory.CreateDirectory(videoDir);
        Directory.CreateDirectory(audioDir);

        File.WriteAllText(Path.Combine(videoDir, "segment001.ts"), "ts data");
        File.WriteAllText(Path.Combine(audioDir, "eng.ts"), "audio data");

        BaseContainer container = ContainerWithSubdirs("video");

        // Act
        VideoEncodeJob.CleanupPartialOutput(_testDir, container);

        // Assert — only the container-owned subdir is gone; basePath and audio survive
        Assert.False(Directory.Exists(videoDir));
        Assert.True(Directory.Exists(audioDir));
        Assert.True(Directory.Exists(_testDir));
    }

    [Fact]
    public void CleanupPartialOutput_RemovesAllReportedSubdirectories()
    {
        // Arrange — container owns both "video" and "audio"
        string videoDir = Path.Combine(_testDir, "video");
        string audioDir = Path.Combine(_testDir, "audio");
        string otherDir = Path.Combine(_testDir, "thumbs");

        Directory.CreateDirectory(videoDir);
        Directory.CreateDirectory(audioDir);
        Directory.CreateDirectory(otherDir);

        BaseContainer container = ContainerWithSubdirs("video", "audio");

        // Act
        VideoEncodeJob.CleanupPartialOutput(_testDir, container);

        // Assert — both owned dirs removed; unrelated "thumbs" survives
        Assert.False(Directory.Exists(videoDir));
        Assert.False(Directory.Exists(audioDir));
        Assert.True(Directory.Exists(otherDir));
        Assert.True(Directory.Exists(_testDir));
    }

    [Fact]
    public void CleanupPartialOutput_ReportedSubdirDoesNotExist_DoesNotThrow()
    {
        // Arrange — basePath exists but the subdir the container reports does not
        Directory.CreateDirectory(_testDir);
        BaseContainer container = ContainerWithSubdirs("ghost_dir");

        // Act + Assert
        Exception? exception = Record.Exception(() =>
            VideoEncodeJob.CleanupPartialOutput(_testDir, container)
        );

        Assert.Null(exception);
        Assert.True(Directory.Exists(_testDir));
    }

    [Fact]
    public void CleanupPartialOutput_NestedContentInSubdir_RemovedRecursively()
    {
        // Arrange — subdir contains nested files and a sub-subdir
        string videoDir = Path.Combine(_testDir, "video");
        string nestedDir = Path.Combine(videoDir, "quality_1080p");

        Directory.CreateDirectory(nestedDir);
        File.WriteAllText(Path.Combine(videoDir, "playlist.m3u8"), "m3u8");
        File.WriteAllText(Path.Combine(nestedDir, "segment001.ts"), "ts data");
        File.WriteAllText(Path.Combine(nestedDir, "segment002.ts"), "ts data");

        BaseContainer container = ContainerWithSubdirs("video");

        // Act
        VideoEncodeJob.CleanupPartialOutput(_testDir, container);

        // Assert — entire video subdir tree is gone
        Assert.False(Directory.Exists(videoDir));
        Assert.True(Directory.Exists(_testDir));
    }

    // ── single-file cleanup ──────────────────────────────────────────────────

    [Fact]
    public void CleanupPartialOutput_SingleFileContainer_RemovesOutputFile()
    {
        // Arrange — container has no streams but FileName is set
        Directory.CreateDirectory(_testDir);
        string outputFileName = "output.mp4";
        string outputFilePath = Path.Combine(_testDir, outputFileName);
        File.WriteAllText(outputFilePath, "mp4 data");

        // A second file that the container does NOT own — must survive
        string otherFile = Path.Combine(_testDir, "chapters.json");
        File.WriteAllText(otherFile, "{}");

        BaseContainer container = ContainerWithFileName(outputFileName);

        // Act
        VideoEncodeJob.CleanupPartialOutput(_testDir, container);

        // Assert
        Assert.False(File.Exists(outputFilePath));
        Assert.True(File.Exists(otherFile));
        Assert.True(Directory.Exists(_testDir));
    }

    [Fact]
    public void CleanupPartialOutput_SingleFileContainer_FileMissing_DoesNotThrow()
    {
        // Arrange — container claims a FileName but the file was never written
        Directory.CreateDirectory(_testDir);
        BaseContainer container = ContainerWithFileName("ghost.mp4");

        // Act + Assert
        Exception? exception = Record.Exception(() =>
            VideoEncodeJob.CleanupPartialOutput(_testDir, container)
        );

        Assert.Null(exception);
    }

    // ── no-op cases ──────────────────────────────────────────────────────────

    [Fact]
    public void CleanupPartialOutput_EmptyContainer_LeavesDirectoryUntouched()
    {
        // Arrange — container has no streams and no FileName
        Directory.CreateDirectory(_testDir);
        string existingFile = Path.Combine(_testDir, "important.mkv");
        File.WriteAllText(existingFile, "data");

        BaseContainer container = EmptyContainer();

        // Act
        VideoEncodeJob.CleanupPartialOutput(_testDir, container);

        // Assert — nothing deleted
        Assert.True(File.Exists(existingFile));
        Assert.True(Directory.Exists(_testDir));
    }

    [Fact]
    public void CleanupPartialOutput_NonExistentBasePath_DoesNotThrow()
    {
        // Arrange — basePath itself does not exist
        string nonExistent = Path.Combine(_testDir, "does_not_exist");
        BaseContainer container = ContainerWithSubdirs("video");

        // Act + Assert
        Exception? exception = Record.Exception(() =>
            VideoEncodeJob.CleanupPartialOutput(nonExistent, container)
        );

        Assert.Null(exception);
    }
}
