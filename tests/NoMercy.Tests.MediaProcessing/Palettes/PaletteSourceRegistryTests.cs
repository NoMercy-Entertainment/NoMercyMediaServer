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

using NoMercy.Database;
using NoMercy.MediaProcessing.Images.Palettes;

namespace NoMercy.Tests.MediaProcessing.Palettes;

[Trait(name: "Category", value: "Unit")]
public class PaletteSourceRegistryTests
{
    private sealed class FakeSource(string type) : IPaletteSource
    {
        public string EntityType => type;

        public Task<string?> CurrentPaletteAsync(
            MediaContext db,
            string id,
            CancellationToken ct
        ) => Task.FromResult<string?>(result: "");

        public Task<PaletteResult> GenerateAsync(
            MediaContext db,
            string id,
            CancellationToken ct
        ) => Task.FromResult(result: PaletteResult.NoImage());

        public Task PersistAsync(MediaContext db, string id, string json, CancellationToken ct) =>
            Task.CompletedTask;
    }

    [Fact]
    public void Resolves_registered_source_and_null_for_unknown()
    {
        PaletteSourceRegistry registry = new(sources: [new FakeSource(type: "movie"), new FakeSource(type: "tv")]);

        registry.Resolve(entityType: "movie").Should().NotBeNull();
        registry.Resolve(entityType: "nope").Should().BeNull();
        registry.EntityTypes.Should().BeEquivalentTo(expectation: ["movie", "tv"]);
    }
}
