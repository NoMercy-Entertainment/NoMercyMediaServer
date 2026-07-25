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

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using NoMercy.Database;

namespace NoMercy.Tests.Database;

public class AdultContentQueryFilterTests
{
    // Movie and Person carry the adult-content query filter. Every required dependent of
    // those principals (join rows such as GenreMovie, LibraryMovie, Cast, Crew) must declare
    // a matching filter, otherwise a hidden adult Movie/Person still leaks its join rows.
    // EF Core reports that gap as PossibleIncorrectRequiredNavigationWithQueryFilterInteraction;
    // promoting it to a throw makes any future unfiltered required dependent fail this test.
    [Fact]
    public void Model_BuildsWithoutRequiredDependentFilterMismatch()
    {
        DbContextOptionsBuilder<MediaContext> optionsBuilder = new();
        optionsBuilder.UseSqlite("Data Source=:memory:");
        optionsBuilder.ConfigureWarnings(warnings =>
            warnings.Throw(
                CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning
            )
        );

        using MediaContext context = new(optionsBuilder.Options);

        IModel model = context.Model;

        Assert.NotNull(model);
    }
}
