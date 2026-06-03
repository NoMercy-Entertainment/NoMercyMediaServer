using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.MediaProcessing.Images.Palettes;
using NoMercy.MediaProcessing.Jobs.PaletteJobs;

namespace NoMercy.Tests.MediaProcessing.Palettes;

[Trait("Category", "Unit")]
public class ColorPaletteJobTests : IDisposable
{
    private readonly MediaContext _db;

    public ColorPaletteJobTests()
    {
        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new(options);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    // ── Fake source helpers ─────────────────────────────────────────────────

    private sealed class StubSource : IPaletteSource
    {
        private readonly string _current;
        private readonly Func<Task<PaletteResult>> _generateFactory;

        public string EntityType => "stub";
        public bool PersistCalled { get; private set; }
        public string? PersistedJson { get; private set; }

        public StubSource(string current, Func<Task<PaletteResult>> generateFactory)
        {
            _current = current;
            _generateFactory = generateFactory;
        }

        public Task<string?> CurrentPaletteAsync(
            MediaContext db,
            string id,
            CancellationToken ct
        ) => Task.FromResult<string?>(_current);

        public Task<PaletteResult> GenerateAsync(
            MediaContext db,
            string id,
            CancellationToken ct
        ) => _generateFactory();

        public Task PersistAsync(MediaContext db, string id, string json, CancellationToken ct)
        {
            PersistCalled = true;
            PersistedJson = json;
            return Task.CompletedTask;
        }
    }

    private static (ColorPaletteJob, StubSource) Build(
        string current,
        Func<Task<PaletteResult>> generate
    )
    {
        StubSource source = new(current, generate);
        PaletteSourceRegistry registry = new([source]);
        ColorPaletteJob job = new("stub", "1");
        return (job, source);
    }

    // ── Tests ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Already_filled_palette_skips_persist()
    {
        (ColorPaletteJob job, StubSource source) = Build(
            current: "{\"poster\":{}}",
            generate: () => Task.FromResult(PaletteResult.Success("{\"poster\":{}}"))
        );
        PaletteSourceRegistry registry = new([source]);

        await job.HandleCore(_db, registry);

        source.PersistCalled.Should().BeFalse("palette is already filled");
    }

    [Fact]
    public async Task Empty_palette_and_successful_generate_persists_real_json()
    {
        string expectedJson = "{\"poster\":{\"dominant\":\"#abc\"}}";
        (ColorPaletteJob job, StubSource source) = Build(
            current: "",
            generate: () => Task.FromResult(PaletteResult.Success(expectedJson))
        );
        PaletteSourceRegistry registry = new([source]);

        await job.HandleCore(_db, registry);

        source.PersistCalled.Should().BeTrue();
        source.PersistedJson.Should().Be(expectedJson);
    }

    [Fact]
    public async Task Transient_failure_propagates_and_does_not_persist_empty_braces()
    {
        (ColorPaletteJob job, StubSource source) = Build(
            current: "",
            generate: () => throw new HttpRequestException("simulated network failure")
        );
        PaletteSourceRegistry registry = new([source]);

        Func<Task> act = () => job.HandleCore(_db, registry);

        await act.Should().ThrowAsync<HttpRequestException>();
        source.PersistCalled.Should().BeFalse("transient failures must not write \"{}\"");
    }

    [Fact]
    public async Task NoImage_result_persists_terminal_empty_braces()
    {
        (ColorPaletteJob job, StubSource source) = Build(
            current: "",
            generate: () => Task.FromResult(PaletteResult.NoImage())
        );
        PaletteSourceRegistry registry = new([source]);

        await job.HandleCore(_db, registry);

        source.PersistCalled.Should().BeTrue();
        source.PersistedJson.Should().Be("{}");
    }

    [Fact]
    public async Task Unknown_entity_type_returns_without_error()
    {
        ColorPaletteJob job = new("unknown_type", "42");
        PaletteSourceRegistry registry = new([]);

        Func<Task> act = () => job.HandleCore(_db, registry);

        await act.Should().NotThrowAsync();
    }
}
