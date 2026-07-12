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

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Dashboard;

/// <summary>
/// The import/"add files" path (ServerController.AddFiles) must honor the
/// per-library <see cref="Library.AutoEncodeOnScan"/> + <see cref="Library.EncodePresetId"/>
/// gate instead of unconditionally dispatching a <c>VideoEncodeJob</c> per
/// file. QueueRunner is a real singleton in the test host (resolved via
/// ServerController's own constructor parameter), so a Dispatch call writes
/// a real row into the shared queue.db — asserted here by filtering on the
/// unique per-test LibraryId embedded in every job payload.
/// </summary>
[Trait("Category", "DashboardServer")]
public class ServerControllerAddFilesTests : IClassFixture<NoMercyApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _authed;

    private readonly Ulid _autoEncodeLibraryId = Ulid.NewUlid();
    private readonly Ulid _manualLibraryId = Ulid.NewUlid();
    private readonly Ulid _folderId = Ulid.NewUlid();
    private readonly Ulid _presetId = Ulid.NewUlid();

    public ServerControllerAddFilesTests(NoMercyApiFactory factory)
    {
        _authed = factory.CreateClient().AsAuthenticated();
    }

    public async Task InitializeAsync()
    {
        await using MediaContext ctx = new();

        Library autoEncodeLibrary = new()
        {
            Id = _autoEncodeLibraryId,
            Title = "Auto Encode Movies",
            Type = "movie",
            AutoEncodeOnScan = true,
            EncodePresetId = _presetId,
        };
        Library manualLibrary = new()
        {
            Id = _manualLibraryId,
            Title = "Manual Movies",
            Type = "movie",
            AutoEncodeOnScan = false,
        };

        ctx.Libraries.AddRange(autoEncodeLibrary, manualLibrary);
        await ctx.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await using MediaContext ctx = new();
        await ctx
            .Libraries.Where(l => l.Id == _autoEncodeLibraryId || l.Id == _manualLibraryId)
            .ExecuteDeleteAsync();
    }

    private static List<string> QueryPayloadsContaining(string marker)
    {
        using QueueContext queueCtx = new();
        return queueCtx
            .QueueJobs.AsNoTracking()
            .Where(j => j.Payload.Contains(marker))
            .Select(j => j.Payload)
            .ToList();
    }

    private static StringContent JsonBody(object obj) =>
        new(JsonSerializer.Serialize(obj), Encoding.UTF8, "application/json");

    [Fact]
    public async Task AddFiles_AutoEncodeOff_DispatchesZeroVideoEncodeJobs()
    {
        object body = new
        {
            library_id = _manualLibraryId.ToString(),
            folder_id = _folderId.ToString(),
            source_driver_id = (string?)null,
            files = new[]
            {
                new { path = "manual/movie-1.mkv", id = "910001" },
                new { path = "manual/movie-2.mkv", id = "910002" },
            },
        };

        HttpResponseMessage response = await _authed.PostAsync(
            "/api/v1/dashboard/server/addfiles",
            JsonBody(body)
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        List<string> payloads = QueryPayloadsContaining(_manualLibraryId.ToString());

        payloads.Should().HaveCount(2, "one job per added file, none of them an encode");
        payloads
            .Should()
            .OnlyContain(
                p => !p.Contains("VideoEncodeJob"),
                "AutoEncodeOnScan is off — no VideoEncodeJob may be dispatched"
            );
        payloads
            .Should()
            .OnlyContain(
                p => p.Contains("FileRescanJob"),
                "the flag-off path indexes the file via FileRescanJob instead of encoding"
            );
    }

    [Fact]
    public async Task AddFiles_AutoEncodeOnWithPreset_DispatchesVideoEncodeJobPerFileWithSourceDriverAndPreset()
    {
        Ulid sourceDriverId = Ulid.NewUlid();

        object body = new
        {
            library_id = _autoEncodeLibraryId.ToString(),
            folder_id = _folderId.ToString(),
            source_driver_id = sourceDriverId.ToString(),
            files = new[]
            {
                new { path = "auto/movie-1.mkv", id = "920001" },
                new { path = "auto/movie-2.mkv", id = "920002" },
            },
        };

        HttpResponseMessage response = await _authed.PostAsync(
            "/api/v1/dashboard/server/addfiles",
            JsonBody(body)
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        List<string> payloads = QueryPayloadsContaining(_autoEncodeLibraryId.ToString());

        payloads.Should().HaveCount(2, "one VideoEncodeJob per added file");
        payloads.Should().OnlyContain(p => p.Contains("VideoEncodeJob"));
        payloads
            .Should()
            .OnlyContain(
                p => p.Contains(sourceDriverId.ToString()),
                "SourceDriverId from the request must be preserved, same as the old call"
            );
        payloads
            .Should()
            .OnlyContain(
                p => p.Contains(_presetId.ToString()),
                "the library's EncodePresetId must be carried onto the dispatched job"
            );
    }
}
