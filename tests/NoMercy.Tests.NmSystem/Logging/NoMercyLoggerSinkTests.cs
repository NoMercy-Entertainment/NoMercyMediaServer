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
/// Pins the per-run JSONL file sink and the record callback: each run writes its own
/// file with a rich, query-friendly line; old runs are pruned to MaxRunFiles; the
/// callback receives a structured record and a throwing callback never breaks logging.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class NoMercyLoggerSinkTests
{
    [Fact]
    public void PerRunFile_WritesRichQueryableJsonLine()
    {
        string dir = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-logdir-{Guid.NewGuid():N}");
        try
        {
            NoMercyLoggerOptions options = new() { Color = false, LogDirectory = dir };
            using (NoMercyLoggerProvider provider = new(options: options, output: new StringWriter()))
            {
                ILogger logger = provider.CreateLogger(
                    categoryName: "NoMercy.Providers.TMDB.Client.TmdbBaseClient"
                );
                logger.LogInformation(message: "Fetching {Id}", args: 27205);
            }

            string[] runs = Directory.GetFiles(path: dir, searchPattern: "run-*.jsonl");
            runs.Should().ContainSingle();

            string[] lines = File.ReadAllLines(path: runs[0]);
            lines.Should().ContainSingle();

            string line = lines[0];
            line.Should().Contain(expected: "\"@t\":");
            line.Should().Contain(expected: "\"Type\":\"moviedb\"");
            line.Should().Contain(expected: "\"Group\":\"Providers\"");
            line.Should().Contain(expected: "\"Level\":\"Information\"");
            line.Should().Contain(expected: "Fetching 27205");
        }
        finally
        {
            if (Directory.Exists(path: dir))
                Directory.Delete(path: dir, recursive: true);
        }
    }

    [Fact]
    public void Retention_KeepsOnlyMaxRunFiles()
    {
        string dir = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-logdir-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: dir);
        try
        {
            foreach (
                string old in new[]
                {
                    "run-20200101-000001-1.jsonl",
                    "run-20200102-000001-1.jsonl",
                    "run-20200103-000001-1.jsonl",
                }
            )
                File.WriteAllText(path: Path.Combine(path1: dir, path2: old), contents: string.Empty);

            NoMercyLoggerOptions options = new()
            {
                Color = false,
                LogDirectory = dir,
                MaxRunFiles = 2,
            };
            using (NoMercyLoggerProvider provider = new(options: options, output: new StringWriter())) { }

            Directory.GetFiles(path: dir, searchPattern: "run-*.jsonl").Length.Should().Be(expected: 2);
        }
        finally
        {
            if (Directory.Exists(path: dir))
                Directory.Delete(path: dir, recursive: true);
        }
    }

    [Fact]
    public void WriteEntry_AppendsLegacyLineToRunFile()
    {
        string dir = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-logdir-{Guid.NewGuid():N}");
        try
        {
            NoMercyLoggerOptions options = new() { Color = false, LogDirectory = dir };
            using (NoMercyLoggerProvider provider = new(options: options, output: new StringWriter()))
            {
                provider.WriteEntry(
                    entry: new()
                    {
                        Type = "queue",
                        Message = "legacy line",
                        Level = "Information",
                        ThreadId = 7,
                        Time = DateTime.UtcNow,
                    }
                );
            }

            string[] runs = Directory.GetFiles(path: dir, searchPattern: "run-*.jsonl");
            runs.Should().ContainSingle();
            string line = File.ReadAllLines(path: runs[0]).Single();
            line.Should().Contain(expected: "\"Type\":\"queue\"");
            line.Should().Contain(expected: "legacy line");
        }
        finally
        {
            if (Directory.Exists(path: dir))
                Directory.Delete(path: dir, recursive: true);
        }
    }

    [Fact]
    public void OnRecord_ReceivesStructuredRecord()
    {
        List<NoMercyLogRecord> received = new();
        NoMercyLoggerOptions options = new() { Color = false, OnRecord = received.Add };
        using NoMercyLoggerProvider provider = new(options: options, output: new StringWriter());
        ILogger logger = provider.CreateLogger(categoryName: "NoMercy.Providers.TVDB.Client.TvdbBaseClient");

        logger.LogWarning(message: "rate limited");

        received.Should().ContainSingle();
        received[index: 0].CategoryKey.Should().Be(expected: "tvdb");
        received[index: 0].Level.Should().Be(expected: LogLevel.Warning);
        received[index: 0].Message.Should().Be(expected: "rate limited");
    }

    [Fact]
    public void OnRecord_ThatThrows_DoesNotBreakLogging()
    {
        StringWriter sink = new();
        NoMercyLoggerOptions options = new()
        {
            Color = false,
            OnRecord = _ => throw new InvalidOperationException(message: "boom"),
        };
        using NoMercyLoggerProvider provider = new(options: options, output: sink);
        ILogger logger = provider.CreateLogger(categoryName: "NoMercy.Service.X");

        Action act = () => logger.LogInformation(message: "still logged");

        act.Should().NotThrow();
        sink.ToString().Should().Contain(expected: "still logged");
    }
}
