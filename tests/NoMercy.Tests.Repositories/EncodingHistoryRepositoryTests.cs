using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

[Trait("Category", "Characterization")]
public class EncodingHistoryRepositoryTests : IDisposable
{
    private readonly MediaContext _context;
    private readonly EncodingHistoryRepository _repository;

    public EncodingHistoryRepositoryTests()
    {
        _context = TestMediaContextFactory.CreateSeededContext();
        _repository = new(_context);
    }

    [Fact]
    public async Task AddAsync_PersistsEntry()
    {
        EncodingHistory entry = Build();

        await _repository.AddAsync(entry);

        EncodingHistory? loaded = await _repository.GetByIdAsync(entry.Id);
        Assert.NotNull(loaded);
        Assert.Equal(entry.InputPath, loaded!.InputPath);
        Assert.Equal(entry.ProfileName, loaded.ProfileName);
    }

    [Fact]
    public async Task GetRecentAsync_OrdersNewestFirst()
    {
        EncodingHistory older = Build(
            profileName: "older",
            createdAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        );
        EncodingHistory newer = Build(
            profileName: "newer",
            createdAt: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        );

        await _repository.AddAsync(older);
        await _repository.AddAsync(newer);

        List<EncodingHistory> page = await _repository.GetRecentAsync(pageSize: 10, pageIndex: 0);

        Assert.Equal("newer", page[0].ProfileName);
        Assert.Equal("older", page[1].ProfileName);
    }

    [Fact]
    public async Task GetRecentAsync_PaginatesCorrectly()
    {
        for (int i = 0; i < 15; i++)
            await _repository.AddAsync(
                Build(profileName: $"profile-{i:D2}", createdAt: DateTime.UtcNow.AddMinutes(i))
            );

        List<EncodingHistory> page1 = await _repository.GetRecentAsync(pageSize: 5, pageIndex: 0);
        List<EncodingHistory> page2 = await _repository.GetRecentAsync(pageSize: 5, pageIndex: 1);

        Assert.Equal(5, page1.Count);
        Assert.Equal(5, page2.Count);
        Assert.DoesNotContain(page1.Select(p => p.Id), id => page2.Select(p => p.Id).Contains(id));
    }

    [Fact]
    public async Task GetRecentAsync_NormalizesBadArguments()
    {
        await _repository.AddAsync(Build());

        List<EncodingHistory> result = await _repository.GetRecentAsync(
            pageSize: -1,
            pageIndex: -5
        );

        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GetTotalCountAsync_ReturnsRowCount()
    {
        await _repository.AddAsync(Build());
        await _repository.AddAsync(Build());
        await _repository.AddAsync(Build());

        int count = await _repository.GetTotalCountAsync();

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        EncodingHistory? loaded = await _repository.GetByIdAsync(Ulid.NewUlid());

        Assert.Null(loaded);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private static EncodingHistory Build(
        string profileName = "default",
        DateTime? createdAt = null
    ) =>
        new()
        {
            InputPath = "/media/source.mkv",
            OutputPath = "/media/encoded/movie.NoMercy.m3u8",
            ProfileId = Ulid.NewUlid(),
            ProfileName = profileName,
            EncoderUsed = "libx264",
            GpuUsed = null,
            DurationSeconds = 300,
            InputSizeBytes = 8_000_000_000,
            OutputSizeBytes = 4_000_000_000,
            CompressionRatio = 0.5,
            AverageSpeed = 2.0,
            AverageFps = 24.0,
            CreatedAt = createdAt ?? DateTime.UtcNow,
        };
}
