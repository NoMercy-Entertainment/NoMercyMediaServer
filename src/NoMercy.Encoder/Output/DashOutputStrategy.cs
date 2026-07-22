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
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Pipeline;
using NoMercy.Storage;

namespace NoMercy.Encoder.Output;

public class DashOutputStrategy(IStorage storage) : IOutputStrategy
{
    public OutputFormat Format => OutputFormat.Dash;

    public void ConfigureOutput(
        FfmpegCommandBuilder builder,
        OutputPlan plan,
        string outputDirectory
    )
    {
        string mpdPath = Path.Combine(path1: outputDirectory, path2: "manifest.mpd");
        List<string> mapStreams = [];

        foreach (VideoOutputPlan video in plan.VideoOutputs)
            mapStreams.Add(item: video.MapLabel);

        foreach (AudioOutputPlan audio in plan.AudioOutputs)
            if (audio.Action is StreamAction.Copy or StreamAction.Transcode)
                mapStreams.Add(item: audio.MapLabel);

        VideoOutputPlan? primaryVideo = plan.VideoOutputs.Length > 0 ? plan.VideoOutputs[0] : null;
        AudioOutputPlan? primaryAudio = plan.AudioOutputs.Length > 0 ? plan.AudioOutputs[0] : null;

        Dictionary<string, string> extraFlags = new()
        {
            [key: "-f"] = "dash",
            [key: "-seg_duration"] = plan.SegmentDurationSeconds.ToString(),
            [key: "-init_seg_name"] = "init_$RepresentationID$.m4s",
            [key: "-media_seg_name"] = "seg_$RepresentationID$_$Number%05d$.m4s",
            [key: "-use_template"] = "1",
            [key: "-use_timeline"] = "1",
            [key: "-adaptation_sets"] = "id=0,streams=v id=1,streams=a",
        };

        // VFR sources must be muxed CFR for segmented DASH — same rationale as
        // HLS: variable PTS gaps drift segment durations off the target.
        if (plan.NormalizeToConstantFrameRate)
            extraFlags[key: "-fps_mode"] = "cfr";

        if (
            primaryAudio?.Action == StreamAction.Transcode
            && !string.IsNullOrEmpty(value: primaryAudio.AudioFilter)
        )
        {
            extraFlags[key: "-af"] = primaryAudio.AudioFilter;
        }

        builder.AddOutput(
            output: new(
                FilePath: mpdPath,
                VideoCodec: primaryVideo?.EncoderName,
                AudioCodec: primaryAudio?.Action == StreamAction.Copy
                    ? "copy"
                    : primaryAudio?.EncoderName,
                Crf: primaryVideo is { Crf: > 0 } ? primaryVideo.Crf : null,
                VideoBitrateKbps: primaryVideo is { BitrateKbps: > 0 }
                    ? primaryVideo.BitrateKbps
                    : null,
                Preset: primaryVideo?.Preset,
                Profile: primaryVideo?.Profile,
                PixelFormat: primaryVideo is { TenBit: true } ? primaryVideo.PixelFormat : null,
                AudioBitrateKbps: primaryAudio?.Action == StreamAction.Transcode
                    ? primaryAudio.BitrateKbps
                    : null,
                MapStreams: mapStreams.ToArray(),
                ExtraFlags: extraFlags
            )
        );
    }

    public async Task FinalizeAsync(
        string outputDirectory,
        OutputPlan plan,
        string mediaTitle,
        CancellationToken ct
    )
    {
        // Rename the generic manifest.mpd to use the media title
        string sourcePath = Path.Combine(path1: outputDirectory, path2: "manifest.mpd");
        string targetPath = Path.Combine(path1: outputDirectory, path2: $"{mediaTitle}.mpd");

        if (storage.Exists(path: sourcePath) && sourcePath != targetPath)
        {
            storage.Delete(path: targetPath);
            storage.Move(from: sourcePath, to: targetPath);
        }

        // Post-process MPD to inject <EventStream> chapter cues when chapters present
        if (plan.Chapters is { Count: > 0 } chapters && storage.Exists(path: targetPath))
        {
            await InjectChapterEventStreamAsync(mpdPath: targetPath, chapters: chapters, ct: ct);
        }
    }

    /// <summary>
    /// Parses the MPD XML and injects a chapter <c>&lt;EventStream&gt;</c> element
    /// into every <c>&lt;Period&gt;</c>.  Uses timescale=1000 (milliseconds).
    /// </summary>
    private async Task InjectChapterEventStreamAsync(
        string mpdPath,
        IReadOnlyList<ChapterInfo> chapters,
        CancellationToken ct
    )
    {
        byte[] rawBytes = await storage.ReadAsync(path: mpdPath, ct: ct);

        // Use XDocument.Load from a MemoryStream so the XML reader handles any
        // BOM (UTF-8, UTF-16) automatically — XDocument.Parse on a raw string
        // produced by Encoding.UTF8.GetString fails when the file starts with a BOM.
        XDocument doc;
        using (MemoryStream ms = new(buffer: rawBytes))
            doc = XDocument.Load(stream: ms);
        XNamespace ns = doc.Root?.Name.Namespace ?? XNamespace.None;

        XElement eventStream = new(
            name: ns + "EventStream", content: [new XAttribute(name: "schemeIdUri", value: "urn:nomercy:chapters"), new XAttribute(name: "timescale", value: "1000")]
        );

        for (int i = 0; i < chapters.Count; i++)
        {
            ChapterInfo chapter = chapters[index: i];
            long startMs = (long)chapter.Start.TotalMilliseconds;
            long endMs =
                i + 1 < chapters.Count
                    ? (long)chapters[index: i + 1].Start.TotalMilliseconds
                    : (long)chapter.End.TotalMilliseconds;
            long durationMs = endMs - startMs;
            string title = chapter.Title ?? $"Chapter {i + 1}";

            eventStream.Add(
                content: new XElement(
                    name: ns + "Event", content: [new XAttribute(name: "presentationTime", value: startMs), new XAttribute(name: "duration", value: durationMs), new XAttribute(name: "id", value: i), new XText(value: title)]
                )
            );
        }

        // Inject into every Period element
        IEnumerable<XElement> periods = doc.Descendants(name: ns + "Period");
        foreach (XElement period in periods)
            period.AddFirst(content: eventStream);

        byte[] updated = Encoding.UTF8.GetBytes(s: doc.ToString());
        await storage.WriteAsync(path: mpdPath, bytes: updated, ct: ct);
    }

    public string[] GetOutputSubdirectories(OutputPlan plan) => [];
}
