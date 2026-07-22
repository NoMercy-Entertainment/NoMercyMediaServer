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

using System.Text;
using System.Xml.Linq;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Output;

/// <summary>
/// Verifies that <see cref="DashOutputStrategy.FinalizeAsync"/> injects an
/// <c>&lt;EventStream&gt;</c> with one <c>&lt;Event&gt;</c> per chapter into
/// every <c>&lt;Period&gt;</c> of the MPD.
/// </summary>
public class DashChapterEventStreamTests : IDisposable
{
    private readonly string _tempDir;

    public DashChapterEventStreamTests()
    {
        _tempDir = Path.Combine(path1: Path.GetTempPath(), path2: $"DashChapterTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: _tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(path: _tempDir))
            Directory.Delete(path: _tempDir, recursive: true);
    }

    // Minimal MPD with one Period so we can verify post-processing
    private const string MinimalMpd = """
        <?xml version="1.0" encoding="utf-8"?>
        <MPD xmlns="urn:mpeg:dash:schema:mpd:2011" minBufferTime="PT1.5S" type="static" mediaPresentationDuration="PT5400S" profiles="urn:mpeg:dash:profile:isoff-on-demand:2011">
          <Period id="0" start="PT0S" duration="PT5400S">
            <AdaptationSet contentType="video" />
          </Period>
        </MPD>
        """;

    private static readonly IReadOnlyList<ChapterInfo> ThreeChapters =
    [
        new(Start: TimeSpan.Zero, End: TimeSpan.FromMinutes(minutes: 10), Title: "Opening"),
        new(Start: TimeSpan.FromMinutes(minutes: 10), End: TimeSpan.FromMinutes(minutes: 50), Title: "Act One & Two"),
        new(Start: TimeSpan.FromMinutes(minutes: 50), End: TimeSpan.FromMinutes(minutes: 90), Title: "<Finale>"),
    ];

    // ------------------------------------------------------------------

    [Fact]
    public async Task FinalizeAsync_WithChapters_InjectsEventStreamElement()
    {
        string mpdPath = await WriteMpdAndFinalize(title: "movie1");

        string xml = await File.ReadAllTextAsync(path: mpdPath);
        xml.Should().Contain(expected: "EventStream");
        xml.Should().Contain(expected: "urn:nomercy:chapters");
    }

    [Fact]
    public async Task FinalizeAsync_WithChapters_TimescaleIs1000()
    {
        string mpdPath = await WriteMpdAndFinalize(title: "movie2");

        string xml = await File.ReadAllTextAsync(path: mpdPath);
        xml.Should().Contain(expected: "timescale=\"1000\"");
    }

    [Fact]
    public async Task FinalizeAsync_WithChapters_EmitsOneEventPerChapter()
    {
        string mpdPath = await WriteMpdAndFinalize(title: "movie3");

        XDocument doc = XDocument.Load(uri: mpdPath);
        XNamespace ns = "urn:mpeg:dash:schema:mpd:2011";
        List<XElement> events = doc.Descendants(name: ns + "Event").ToList();

        events.Should().HaveCount(expected: ThreeChapters.Count);
    }

    [Fact]
    public async Task FinalizeAsync_WithChapters_EventPresentationTimesAreInMilliseconds()
    {
        string mpdPath = await WriteMpdAndFinalize(title: "movie4");

        XDocument doc = XDocument.Load(uri: mpdPath);
        XNamespace ns = "urn:mpeg:dash:schema:mpd:2011";
        List<XElement> events = doc.Descendants(name: ns + "Event").ToList();

        // Chapter 1: 0 ms, Chapter 2: 10 min = 600000 ms, Chapter 3: 50 min = 3000000 ms
        events[index: 0].Attribute(name: "presentationTime")!.Value.Should().Be(expected: "0");
        events[index: 1].Attribute(name: "presentationTime")!.Value.Should().Be(expected: "600000");
        events[index: 2].Attribute(name: "presentationTime")!.Value.Should().Be(expected: "3000000");
    }

    [Fact]
    public async Task FinalizeAsync_WithChapters_EventIdsAreZeroBasedIndex()
    {
        string mpdPath = await WriteMpdAndFinalize(title: "movie5");

        XDocument doc = XDocument.Load(uri: mpdPath);
        XNamespace ns = "urn:mpeg:dash:schema:mpd:2011";
        List<XElement> events = doc.Descendants(name: ns + "Event").ToList();

        events[index: 0].Attribute(name: "id")!.Value.Should().Be(expected: "0");
        events[index: 1].Attribute(name: "id")!.Value.Should().Be(expected: "1");
        events[index: 2].Attribute(name: "id")!.Value.Should().Be(expected: "2");
    }

    [Fact]
    public async Task FinalizeAsync_WithChapters_TitlesAreXmlEscaped()
    {
        string mpdPath = await WriteMpdAndFinalize(title: "movie6");

        // Raw XML text — the & and < in titles must be entity-escaped
        string rawXml = await File.ReadAllTextAsync(path: mpdPath);
        rawXml.Should().Contain(expected: "Act One &amp; Two");
        rawXml.Should().Contain(expected: "&lt;Finale&gt;");
    }

    [Fact]
    public async Task FinalizeAsync_WithoutChapters_DoesNotAddEventStream()
    {
        string mpdPath = WriteMpd(title: "movie7");
        OutputPlan plan = CreatePlan(chapters: null);

        DashOutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        await strategy.FinalizeAsync(outputDirectory: _tempDir, plan: plan, mediaTitle: "movie7", ct: default);

        string xml = await File.ReadAllTextAsync(path: mpdPath);
        xml.Should().NotContain(unexpected: "EventStream");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>Writes manifest.mpd, runs FinalizeAsync, returns path to renamed MPD.</summary>
    private async Task<string> WriteMpdAndFinalize(string title)
    {
        WriteMpd(title: title);
        OutputPlan plan = CreatePlan(chapters: ThreeChapters);
        DashOutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal());
        await strategy.FinalizeAsync(outputDirectory: _tempDir, plan: plan, mediaTitle: title, ct: default);
        return Path.Combine(path1: _tempDir, path2: $"{title}.mpd");
    }

    private string WriteMpd(string title)
    {
        // FinalizeAsync renames manifest.mpd → {title}.mpd so we always write manifest.mpd
        string path = Path.Combine(path1: _tempDir, path2: "manifest.mpd");
        File.WriteAllText(path: path, contents: MinimalMpd, encoding: Encoding.UTF8);
        return Path.Combine(path1: _tempDir, path2: $"{title}.mpd");
    }

    private static OutputPlan CreatePlan(IReadOnlyList<ChapterInfo>? chapters) =>
        new(
            Format: OutputFormat.Dash,
            VideoOutputs:
            [
                new(
                    Width: 1920,
                    Height: 1080,
                    EncoderName: "libx264",
                    Crf: 23,
                    BitrateKbps: 8000,
                    Preset: "medium",
                    Profile: "high",
                    Level: "4.0",
                    TenBit: false,
                    PixelFormat: "yuv420p",
                    MapLabel: "[v0]",
                    ExtraFlags: new()
                ),
            ],
            AudioOutputs: [new(EncoderName: "aac", BitrateKbps: 192, Channels: 2, SampleRate: 48000, Action: StreamAction.Transcode, Language: "eng", MapLabel: "0:a:0")],
            SubtitleOutputs: [],
            Thumbnails: null,
            Chapters: chapters
        );
}
