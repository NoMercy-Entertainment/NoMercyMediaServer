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

[Trait("Category", "Unit")]
public class PaletteSourceRegistryTests
{
    private sealed class FakeSource(string type) : IPaletteSource
    {
        public string EntityType => type;

        public Task<string?> CurrentPaletteAsync(
            MediaContext db,
            string id,
            CancellationToken ct
        ) => Task.FromResult<string?>("");

        public Task<PaletteResult> GenerateAsync(
            MediaContext db,
            string id,
            CancellationToken ct
        ) => Task.FromResult(PaletteResult.NoImage());

        public Task PersistAsync(MediaContext db, string id, string json, CancellationToken ct) =>
            Task.CompletedTask;
    }

    [Fact]
    public void Resolves_registered_source_and_null_for_unknown()
    {
        PaletteSourceRegistry registry = new([new FakeSource("movie"), new FakeSource("tv")]);

        registry.Resolve("movie").Should().NotBeNull();
        registry.Resolve("nope").Should().BeNull();
        registry.EntityTypes.Should().BeEquivalentTo(["movie", "tv"]);
    }
}
