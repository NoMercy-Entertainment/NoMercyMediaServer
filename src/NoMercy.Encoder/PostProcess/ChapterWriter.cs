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
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Storage;

namespace NoMercy.Encoder.PostProcess;

public class ChapterWriter(IStorage storage) : IChapterWriter
{
    public async Task WriteChaptersAsync(
        string outputDirectory,
        IReadOnlyList<ChapterInfo> chapters,
        CancellationToken ct,
        bool includeThumbUris = false
    )
    {
        if (chapters.Count == 0)
            return;

        StringBuilder sb = new();
        sb.AppendLine("WEBVTT");
        sb.AppendLine();

        for (int i = 0; i < chapters.Count; i++)
        {
            ChapterInfo chapter = chapters[i];
            sb.AppendLine($"Chapter {i + 1}");
            sb.AppendLine($"{FormatVttTime(chapter.Start)} --> {FormatVttTime(chapter.End)}");
            sb.AppendLine(chapter.Title ?? $"Chapter {i + 1}");

            if (includeThumbUris)
                sb.AppendLine($"chapters/{i:D2}.webp");

            sb.AppendLine();
        }

        string chaptersFile = Path.Combine(outputDirectory, "chapters.vtt");
        await storage.WriteAsync(chaptersFile, Encoding.UTF8.GetBytes(sb.ToString()), ct);
    }

    private static string FormatVttTime(TimeSpan ts) =>
        $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
}
