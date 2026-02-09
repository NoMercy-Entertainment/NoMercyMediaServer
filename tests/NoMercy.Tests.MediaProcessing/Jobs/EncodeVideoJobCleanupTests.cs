using NoMercy.MediaProcessing.Jobs.MediaJobs;

namespace NoMercy.Tests.MediaProcessing.Jobs;

public class EncodeVideoJobCleanupTests : IDisposable
{
    private readonly string _testDir;

    public EncodeVideoJobCleanupTests()
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

    [Fact]
    public void CleanupPartialOutput_RemovesExistingDirectory()
    {
        Directory.CreateDirectory(_testDir);
        string testFile = Path.Combine(_testDir, "video_00001.ts");
        File.WriteAllText(testFile, "partial segment data");

        string subDir = Path.Combine(_testDir, "subtitles");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "eng.vtt"), "subtitle data");

        EncodeVideoJob.CleanupPartialOutput(_testDir);

        Assert.False(Directory.Exists(_testDir));
    }

    [Fact]
    public void CleanupPartialOutput_NonExistentDirectory_DoesNotThrow()
    {
        string nonExistent = Path.Combine(_testDir, "does_not_exist");

        Exception? exception = Record.Exception(() =>
            EncodeVideoJob.CleanupPartialOutput(nonExistent));

        Assert.Null(exception);
    }

    [Fact]
    public void CleanupPartialOutput_RemovesAllNestedContent()
    {
        Directory.CreateDirectory(_testDir);

        string[] segments = ["video_00001.ts", "video_00002.ts", "video_00003.ts", "playlist.m3u8"];
        foreach (string segment in segments)
        {
            File.WriteAllText(Path.Combine(_testDir, segment), "data");
        }

        string thumbsDir = Path.Combine(_testDir, "thumbs");
        Directory.CreateDirectory(thumbsDir);
        File.WriteAllText(Path.Combine(thumbsDir, "sprite.jpg"), "sprite data");

        EncodeVideoJob.CleanupPartialOutput(_testDir);

        Assert.False(Directory.Exists(_testDir));
    }

    [Fact]
    public void CleanupPartialOutput_EmptyDirectory_RemovesIt()
    {
        Directory.CreateDirectory(_testDir);

        EncodeVideoJob.CleanupPartialOutput(_testDir);

        Assert.False(Directory.Exists(_testDir));
    }
}
