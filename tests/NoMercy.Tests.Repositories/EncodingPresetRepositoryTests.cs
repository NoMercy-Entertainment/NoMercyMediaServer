using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

[Trait("Category", "Characterization")]
public class EncodingPresetRepositoryTests : IDisposable
{
    private readonly MediaContext _context;
    private readonly EncodingPresetRepository _repository;

    public EncodingPresetRepositoryTests()
    {
        _context = TestMediaContextFactory.CreateSeededContext();
        _repository = new(_context);
    }

    [Fact]
    public async Task CreateAsync_PersistsPresetAndAssignsTimestamps()
    {
        EncodingPreset preset = Build(name: "General 1080p");

        EncodingPreset saved = await _repository.CreateAsync(preset);

        Assert.NotEqual(default, saved.CreatedAt);
        Assert.Equal(saved.CreatedAt, saved.UpdatedAt);

        EncodingPreset? loaded = await _repository.GetByIdAsync(saved.Id);
        Assert.NotNull(loaded);
        Assert.Equal("General 1080p", loaded!.Name);
    }

    [Fact]
    public async Task GetByNameAsync_ReturnsExactMatch()
    {
        await _repository.CreateAsync(Build(name: "Anime"));
        await _repository.CreateAsync(Build(name: "Archive"));

        EncodingPreset? anime = await _repository.GetByNameAsync("Anime");

        Assert.NotNull(anime);
        Assert.Equal("Anime", anime!.Name);
    }

    [Fact]
    public async Task ListAsync_PutsBuiltInsFirst()
    {
        await _repository.CreateAsync(Build(name: "zzz-user", isBuiltIn: false));
        await _repository.CreateAsync(Build(name: "aaa-builtin", isBuiltIn: true));

        List<EncodingPreset> page = await _repository.ListAsync();

        Assert.Equal("aaa-builtin", page[0].Name);
        Assert.Equal("zzz-user", page[1].Name);
    }

    [Fact]
    public async Task UpdateAsync_AppliesChanges_AndRefreshesTimestamp()
    {
        EncodingPreset original = await _repository.CreateAsync(
            Build(name: "V1", description: "before")
        );

        await Task.Delay(5); // Ensure UtcNow moves forward.
        EncodingPreset? updated = await _repository.UpdateAsync(
            original.Id,
            p => p.Description = "after"
        );

        Assert.NotNull(updated);
        Assert.Equal("after", updated!.Description);
        Assert.True(updated.UpdatedAt >= original.UpdatedAt);
    }

    [Fact]
    public async Task UpdateAsync_BuiltInPreset_Throws()
    {
        EncodingPreset builtin = await _repository.CreateAsync(Build(isBuiltIn: true));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repository.UpdateAsync(builtin.Id, p => p.Description = "nope")
        );
    }

    [Fact]
    public async Task DeleteAsync_BuiltInPreset_Throws()
    {
        EncodingPreset builtin = await _repository.CreateAsync(Build(isBuiltIn: true));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repository.DeleteAsync(builtin.Id)
        );
    }

    [Fact]
    public async Task DeleteAsync_UserPreset_Removes()
    {
        EncodingPreset user = await _repository.CreateAsync(Build(isBuiltIn: false));

        bool removed = await _repository.DeleteAsync(user.Id);

        Assert.True(removed);
        Assert.Null(await _repository.GetByIdAsync(user.Id));
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_ReturnsFalse()
    {
        bool removed = await _repository.DeleteAsync(Ulid.NewUlid());
        Assert.False(removed);
    }

    [Fact]
    public async Task UpdateAsync_UnknownId_ReturnsNull()
    {
        EncodingPreset? updated = await _repository.UpdateAsync(Ulid.NewUlid(), _ => { });
        Assert.Null(updated);
    }

    [Fact]
    public async Task GetTotalCountAsync_CountsEveryRow()
    {
        await _repository.CreateAsync(Build(name: "a"));
        await _repository.CreateAsync(Build(name: "b"));
        await _repository.CreateAsync(Build(name: "c"));

        int count = await _repository.GetTotalCountAsync();

        Assert.Equal(3, count);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private static EncodingPreset Build(
        string name = "Sample",
        string? description = null,
        bool isBuiltIn = false
    ) =>
        new()
        {
            Name = name,
            Description = description,
            ProfileJson =
                "{\"Name\":\"sample\",\"Format\":\"Hls\",\"VideoOutputs\":[],\"AudioOutputs\":[],\"SubtitleOutputs\":[]}",
            IsBuiltIn = isBuiltIn,
        };
}
