// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

[Trait(name: "Category", value: "Characterization")]
public class EncodingPresetRepositoryTests : IDisposable
{
    private readonly MediaContext _context;
    private readonly EncodingPresetRepository _repository;

    public EncodingPresetRepositoryTests()
    {
        _context = TestMediaContextFactory.CreateSeededContext();
        _repository = new(context: _context);
    }

    [Fact]
    public async Task CreateAsync_PersistsPresetAndAssignsTimestamps()
    {
        EncodingPreset preset = Build(name: "General 1080p");

        EncodingPreset saved = await _repository.CreateAsync(preset: preset);

        Assert.NotEqual(expected: default, actual: saved.CreatedAt);
        Assert.Equal(expected: saved.CreatedAt, actual: saved.UpdatedAt);

        EncodingPreset? loaded = await _repository.GetByIdAsync(id: saved.Id);
        Assert.NotNull(@object: loaded);
        Assert.Equal(expected: "General 1080p", actual: loaded!.Name);
    }

    [Fact]
    public async Task GetByNameAsync_ReturnsExactMatch()
    {
        await _repository.CreateAsync(preset: Build(name: "Anime"));
        await _repository.CreateAsync(preset: Build(name: "Archive"));

        EncodingPreset? anime = await _repository.GetByNameAsync(name: "Anime");

        Assert.NotNull(@object: anime);
        Assert.Equal(expected: "Anime", actual: anime!.Name);
    }

    [Fact]
    public async Task ListAsync_PutsBuiltInsFirst()
    {
        await _repository.CreateAsync(preset: Build(name: "zzz-user", isBuiltIn: false));
        await _repository.CreateAsync(preset: Build(name: "aaa-builtin", isBuiltIn: true));

        // Seeded baseline data (e.g. the shared "Default HLS" preset) may add
        // other non-builtin rows to the page, so assert relative order between
        // the two rows this test controls rather than absolute page positions.
        List<EncodingPreset> page = await _repository.ListAsync();

        int builtinIndex = page.FindIndex(match: p => p.Name == "aaa-builtin");
        int userIndex = page.FindIndex(match: p => p.Name == "zzz-user");

        Assert.True(condition: builtinIndex >= 0, userMessage: "aaa-builtin should be present in the page");
        Assert.True(condition: userIndex >= 0, userMessage: "zzz-user should be present in the page");
        Assert.True(condition: builtinIndex < userIndex, userMessage: "built-in presets must sort before user presets");
    }

    [Fact]
    public async Task UpdateAsync_AppliesChanges_AndRefreshesTimestamp()
    {
        EncodingPreset original = await _repository.CreateAsync(
            preset: Build(name: "V1", description: "before")
        );

        await Task.Delay(millisecondsDelay: 5); // Ensure UtcNow moves forward.
        EncodingPreset? updated = await _repository.UpdateAsync(
            id: original.Id,
            apply: p => p.Description = "after"
        );

        Assert.NotNull(@object: updated);
        Assert.Equal(expected: "after", actual: updated!.Description);
        Assert.True(condition: updated.UpdatedAt >= original.UpdatedAt);
    }

    [Fact]
    public async Task UpdateAsync_BuiltInPreset_Throws()
    {
        EncodingPreset builtin = await _repository.CreateAsync(preset: Build(isBuiltIn: true));

        await Assert.ThrowsAsync<InvalidOperationException>(testCode: () =>
            _repository.UpdateAsync(id: builtin.Id, apply: p => p.Description = "nope")
        );
    }

    [Fact]
    public async Task DeleteAsync_BuiltInPreset_Throws()
    {
        EncodingPreset builtin = await _repository.CreateAsync(preset: Build(isBuiltIn: true));

        await Assert.ThrowsAsync<InvalidOperationException>(testCode: () =>
            _repository.DeleteAsync(id: builtin.Id)
        );
    }

    [Fact]
    public async Task DeleteAsync_UserPreset_Removes()
    {
        EncodingPreset user = await _repository.CreateAsync(preset: Build(isBuiltIn: false));

        bool removed = await _repository.DeleteAsync(id: user.Id);

        Assert.True(condition: removed);
        Assert.Null(@object: await _repository.GetByIdAsync(id: user.Id));
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_ReturnsFalse()
    {
        bool removed = await _repository.DeleteAsync(id: Ulid.NewUlid());
        Assert.False(condition: removed);
    }

    [Fact]
    public async Task UpdateAsync_UnknownId_ReturnsNull()
    {
        EncodingPreset? updated = await _repository.UpdateAsync(id: Ulid.NewUlid(), apply: _ => { });
        Assert.Null(@object: updated);
    }

    [Fact]
    public async Task GetTotalCountAsync_CountsEveryRow()
    {
        int before = await _repository.GetTotalCountAsync();

        await _repository.CreateAsync(preset: Build(name: "a"));
        await _repository.CreateAsync(preset: Build(name: "b"));
        await _repository.CreateAsync(preset: Build(name: "c"));

        int count = await _repository.GetTotalCountAsync();

        Assert.Equal(expected: before + 3, actual: count);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(obj: this);
    }

    [Fact]
    public async Task ListAsync_TagFilter_ReturnsOnlyMatches()
    {
        await _repository.CreateAsync(preset: Build(name: "Anime 1080p", tags: "anime,1080p,h264"));
        await _repository.CreateAsync(preset: Build(name: "Archive", tags: "archival,h265"));
        await _repository.CreateAsync(preset: Build(name: "Web", tags: "web,720p"));

        List<EncodingPreset> animeMatches = await _repository.ListAsync(tagFilter: "anime");
        List<EncodingPreset> hevcMatches = await _repository.ListAsync(tagFilter: "h265");

        Assert.Single(collection: animeMatches);
        Assert.Equal(expected: "Anime 1080p", actual: animeMatches[index: 0].Name);
        Assert.Single(collection: hevcMatches);
        Assert.Equal(expected: "Archive", actual: hevcMatches[index: 0].Name);
    }

    [Fact]
    public async Task ListAsync_TagFilter_IsCaseInsensitive()
    {
        await _repository.CreateAsync(preset: Build(name: "a", tags: "Anime,Drama"));

        List<EncodingPreset> lower = await _repository.ListAsync(tagFilter: "anime");
        List<EncodingPreset> upper = await _repository.ListAsync(tagFilter: "ANIME");

        Assert.Single(collection: lower);
        Assert.Single(collection: upper);
    }

    [Fact]
    public async Task ListAsync_TagFilter_UnknownTagReturnsEmpty()
    {
        await _repository.CreateAsync(preset: Build(tags: "anime"));

        List<EncodingPreset> matches = await _repository.ListAsync(tagFilter: "nonexistent");

        Assert.Empty(collection: matches);
    }

    [Fact]
    public async Task GetAllTagsAsync_ReturnsDistinctSortedTags()
    {
        await _repository.CreateAsync(preset: Build(name: "a", tags: "anime,1080p"));
        await _repository.CreateAsync(preset: Build(name: "b", tags: "anime,archival"));
        await _repository.CreateAsync(preset: Build(name: "c", tags: null));

        IReadOnlyList<string> tags = await _repository.GetAllTagsAsync();

        Assert.Equal(expected: ["1080p", "anime", "archival"], actual: tags);
    }

    private static EncodingPreset Build(
        string name = "Sample",
        string? description = null,
        string? tags = null,
        bool isBuiltIn = false
    ) =>
        new()
        {
            Name = name,
            Description = description,
            Tags = tags,
            ProfileJson =
                "{\"Name\":\"sample\",\"Format\":\"Hls\",\"VideoOutputs\":[],\"AudioOutputs\":[],\"SubtitleOutputs\":[]}",
            IsBuiltIn = isBuiltIn,
        };
}
