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

using FluentAssertions;
using Moq;
using NoMercy.MediaProcessing.Shows;
using Xunit;

namespace NoMercy.Tests.MediaProcessing.Shows;

// Handle() opens its own AppDbContext() against the real configured store
// (mirrors PaletteBackfillJob, which is untested at this level for the same
// reason) - only the parts reachable without touching that context are
// covered here. AnimeEnrichmentBackfillStateTests covers the cursor/complete
// logic itself against an injected in-memory context.
public class AnimeEnrichmentBackfillJobTests
{
    [Fact]
    public void QueueName_IsExtras()
    {
        AnimeEnrichmentBackfillJob job = new(Mock.Of<IAnimeEnrichmentService>());

        job.QueueName.Should().Be("extras");
    }

    [Fact]
    public void Priority_IsLowestSoLiveWorkDrainsFirst()
    {
        AnimeEnrichmentBackfillJob job = new(Mock.Of<IAnimeEnrichmentService>());

        job.Priority.Should().Be(0);
    }

    [Fact]
    public async Task Handle_NoServicesInjected_DoesNotThrow()
    {
        // The queue's parameterless constructor path with no InjectStorageServices
        // call (e.g. a direct Handle() invocation in a test double) must no-op
        // rather than throw a NullReferenceException.
        AnimeEnrichmentBackfillJob job = new();

        Func<Task> act = () => job.Handle();

        await act.Should().NotThrowAsync();
    }
}
