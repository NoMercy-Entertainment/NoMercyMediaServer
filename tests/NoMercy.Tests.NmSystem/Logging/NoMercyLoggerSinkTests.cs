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

using Microsoft.Extensions.Logging;
using NoMercy.NmSystem.Logging;

namespace NoMercy.Tests.NmSystem;

/// <summary>
/// Pins the JSON file sink and the record callback: each entry produces one JSON
/// line carrying the resolved category, and the callback receives a structured
/// record. A throwing callback never breaks logging.
/// </summary>
[Trait("Category", "Unit")]
public class NoMercyLoggerSinkTests
{
    [Fact]
    public void JsonFileSink_WritesOneJsonLinePerEntry()
    {
        string path = Path.Combine(Path.GetTempPath(), $"nm-log-{Guid.NewGuid():N}.jsonl");
        try
        {
            NoMercyLoggerOptions options = new() { Color = false, JsonFilePath = path };
            using (NoMercyLoggerProvider provider = new(options, new StringWriter()))
            {
                ILogger logger = provider.CreateLogger("NoMercy.Providers.TMDB.Client.TmdbBaseClient");
                logger.LogInformation("Fetching {Id}", 27205);
            }

            string[] lines = File.ReadAllLines(path);
            lines.Should().HaveCount(1);
            lines[0].Should().Contain("\"CategoryKey\":\"moviedb\"");
            lines[0].Should().Contain("Fetching 27205");
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void OnRecord_ReceivesStructuredRecord()
    {
        List<NoMercyLogRecord> received = new();
        NoMercyLoggerOptions options = new() { Color = false, OnRecord = received.Add };
        using NoMercyLoggerProvider provider = new(options, new StringWriter());
        ILogger logger = provider.CreateLogger("NoMercy.Providers.TVDB.Client.TvdbBaseClient");

        logger.LogWarning("rate limited");

        received.Should().ContainSingle();
        received[0].CategoryKey.Should().Be("tvdb");
        received[0].Level.Should().Be(LogLevel.Warning);
        received[0].Message.Should().Be("rate limited");
    }

    [Fact]
    public void OnRecord_ThatThrows_DoesNotBreakLogging()
    {
        StringWriter sink = new();
        NoMercyLoggerOptions options =
            new()
            {
                Color = false,
                OnRecord = _ => throw new InvalidOperationException("boom"),
            };
        using NoMercyLoggerProvider provider = new(options, sink);
        ILogger logger = provider.CreateLogger("NoMercy.Service.X");

        Action act = () => logger.LogInformation("still logged");

        act.Should().NotThrow();
        sink.ToString().Should().Contain("still logged");
    }
}
