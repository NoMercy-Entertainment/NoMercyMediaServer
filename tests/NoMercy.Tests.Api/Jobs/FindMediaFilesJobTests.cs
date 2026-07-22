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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Data.Jobs;
using NoMercy.Database.Models.Libraries;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.NmSystem.Domain;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Jobs;

// ---------------------------------------------------------------------------
// FindMediaFilesJob.Handle() creates its own MediaContext (`new MediaContext()`,
// no injected options) — it is not driveable from an isolated unit test the way
// the rest of the suite tests jobs. NoMercyApiFactory is the one place in this
// codebase that already owns a safe, process-wide, idempotent setup of the real
// on-disk media.db (locked init, AppFiles paths, seeded libraries), so this
// reuses that fixture rather than touching AppFiles.MediaDatabase unguarded.
//
// The scan id (555555) is never seeded by NoMercyApiFactory, so
// FileLogic.MediaType() resolves Movie = null and Paths() returns immediately
// with no storage access — Handle() still reaches its final, unconditional
// LibraryRefreshedEvent publish with an empty file scan.
// ---------------------------------------------------------------------------
[Trait(name: "Category", value: "Jobs")]
public class FindMediaFilesJobTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;

    public FindMediaFilesJobTests(NoMercyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Handle_PublishesLibraryRefreshedEvent_WithStringIdElement()
    {
        IEventBus eventBus = _factory.Services.GetRequiredService<IEventBus>();
        List<LibraryRefreshedEvent> captured = [];
        using IDisposable subscription = eventBus.Subscribe<LibraryRefreshedEvent>(
            handler: (evt, _) =>
            {
                captured.Add(item: evt);
                return Task.CompletedTask;
            }
        );

        const int unseededMovieId = 555555;
        Library library = new()
        {
            Id = NoMercyApiFactory.MovieLibraryId,
            Type = MediaTypes.MovieMediaType,
        };

        FindMediaFilesJob job = new(id: unseededMovieId, library: library)
        {
            LoggerFactory = NullLoggerFactory.Instance,
        };

        await job.Handle();

        captured
            .Should()
            .ContainSingle(
                predicate: evt =>
                    evt.QueryKey.SequenceEqual(
                        new object?[] { MediaTypes.MovieMediaType, unseededMovieId.ToString() }
                    ),
                because: "the published id element must be the STRING form of the scan id, not a raw int — "
                         + "a bare `Id` element here would fail this SequenceEqual against \"555555\""
            );
    }
}
