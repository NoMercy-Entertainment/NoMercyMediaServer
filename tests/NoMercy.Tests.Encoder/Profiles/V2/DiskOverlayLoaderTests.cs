using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Profiles.V2;

public class DiskOverlayLoaderTests : IDisposable
{
    private readonly string _tempDir;

    public DiskOverlayLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"nomercy-overlay-{Ulid.NewUlid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch { }
    }

    [Fact]
    public void Loads_wrapper_form()
    {
        File.WriteAllText(
            Path.Combine(_tempDir, "wrap.json"),
            """
            {
                "name": "Wrapped",
                "profile": { "id": "01HQ6298ZS00000000000000AA", "name": "Wrapped", "container": 3, "audio": [], "subtitles": [] }
            }
            """
        );

        DiskOverlayLoader.LoadResult result = DiskOverlayLoader.Load(_tempDir);
        result.Loaded.Should().HaveCount(1);
        result.Loaded[0].Profile.Name.Should().Be("Wrapped");
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Loads_raw_form()
    {
        File.WriteAllText(
            Path.Combine(_tempDir, "raw.json"),
            """
            { "id": "01HQ6298ZS00000000000000BB", "name": "Raw", "container": 3, "audio": [], "subtitles": [] }
            """
        );

        DiskOverlayLoader.LoadResult result = DiskOverlayLoader.Load(_tempDir);
        result.Loaded.Should().HaveCount(1);
        result.Loaded[0].Profile.Name.Should().Be("Raw");
    }

    [Fact]
    public void Bad_json_logs_error_skips_file_continues()
    {
        File.WriteAllText(
            Path.Combine(_tempDir, "good.json"),
            """
            { "name": "Good", "profile": { "id": "01HQ6298ZS00000000000000CC", "name": "Good", "container": 3, "audio": [], "subtitles": [] } }
            """
        );
        File.WriteAllText(Path.Combine(_tempDir, "bad.json"), "{not json");

        DiskOverlayLoader.LoadResult result = DiskOverlayLoader.Load(_tempDir);
        result.Loaded.Should().HaveCount(1);
        result.Loaded[0].Profile.Name.Should().Be("Good");
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Should().Contain("bad.json");
    }

    [Fact]
    public void Forward_compat_extra_keys_tolerated()
    {
        File.WriteAllText(
            Path.Combine(_tempDir, "extra.json"),
            """
            {
                "name": "Extra",
                "futureField": "ignored",
                "profile": {
                    "id": "01HQ6298ZS00000000000000DD",
                    "name": "Extra",
                    "container": 3,
                    "audio": [],
                    "subtitles": [],
                    "anotherFutureField": 42
                }
            }
            """
        );
        DiskOverlayLoader.LoadResult result = DiskOverlayLoader.Load(_tempDir);
        result.Loaded.Should().HaveCount(1);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Empty_file_logs_error_continues()
    {
        File.WriteAllText(Path.Combine(_tempDir, "empty.json"), "");
        File.WriteAllText(
            Path.Combine(_tempDir, "ok.json"),
            """
            { "id": "01HQ6298ZS00000000000000EE", "name": "Ok", "container": 3, "audio": [], "subtitles": [] }
            """
        );
        DiskOverlayLoader.LoadResult result = DiskOverlayLoader.Load(_tempDir);
        result.Loaded.Should().HaveCount(1);
        result.Errors.Should().HaveCount(1);
    }

    [Fact]
    public void Mixed_wrapper_and_raw_both_load()
    {
        File.WriteAllText(
            Path.Combine(_tempDir, "w.json"),
            """
            { "name": "W", "profile": { "id": "01HQ6298ZS00000000000000FF", "name": "W", "container": 3, "audio": [], "subtitles": [] } }
            """
        );
        File.WriteAllText(
            Path.Combine(_tempDir, "r.json"),
            """
            { "id": "01HQ6298ZS0000000000000100", "name": "R", "container": 3, "audio": [], "subtitles": [] }
            """
        );
        DiskOverlayLoader.LoadResult result = DiskOverlayLoader.Load(_tempDir);
        result.Loaded.Should().HaveCount(2);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Subdirectories_not_scanned_recursively()
    {
        string sub = Path.Combine(_tempDir, "sub");
        Directory.CreateDirectory(sub);
        File.WriteAllText(
            Path.Combine(sub, "deep.json"),
            """
            { "id": "01HQ6298ZS0000000000000111", "name": "Deep", "container": 3, "audio": [], "subtitles": [] }
            """
        );
        DiskOverlayLoader.LoadResult result = DiskOverlayLoader.Load(_tempDir);
        result.Loaded.Should().BeEmpty();
    }

    [Fact]
    public void Missing_directory_returns_empty()
    {
        string missing = Path.Combine(_tempDir, "nope");
        DiskOverlayLoader.LoadResult result = DiskOverlayLoader.Load(missing);
        result.Loaded.Should().BeEmpty();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Duplicate_ulid_across_files_warns_but_loads_both()
    {
        File.WriteAllText(
            Path.Combine(_tempDir, "first.json"),
            """
            { "id": "01HQ6298ZS0000000000000222", "name": "First", "container": 3, "audio": [], "subtitles": [] }
            """
        );
        File.WriteAllText(
            Path.Combine(_tempDir, "second.json"),
            """
            { "id": "01HQ6298ZS0000000000000222", "name": "Second", "container": 3, "audio": [], "subtitles": [] }
            """
        );
        DiskOverlayLoader.LoadResult result = DiskOverlayLoader.Load(_tempDir);
        result.Loaded.Should().HaveCount(2);
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Should().Contain("duplicate");
    }
}
