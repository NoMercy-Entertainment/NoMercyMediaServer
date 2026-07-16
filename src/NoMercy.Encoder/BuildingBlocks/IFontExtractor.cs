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

using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Commands;

namespace NoMercy.Encoder.BuildingBlocks;

public interface IFontExtractor
{
    FfmpegCommand BuildExtractionCommand(
        string ffmpegPath,
        string inputPath,
        string outputDirectory,
        IReadOnlyList<AttachmentInfo> attachments
    );

    /// <summary>
    /// Writes fonts.json and returns the number of font files it lists, so the
    /// caller can verify every embedded font was extracted.
    /// </summary>
    Task<int> WriteFontManifestAsync(string outputDirectory, CancellationToken ct);

    /// <summary>Counts how many of the source attachments are fonts.</summary>
    int CountFontAttachments(IReadOnlyList<AttachmentInfo> attachments);

    /// <summary>
    /// Resolves the sanitized, collision-free "-dump_attachment" target for
    /// each attachment (relative to the extraction command's working
    /// directory). Used by ExtractionCommandBuilder to merge attachment
    /// dumping into the combined subtitle+font extraction command.
    /// </summary>
    IReadOnlyList<AttachmentDumpTarget> ResolveAttachmentDumpTargets(
        IReadOnlyList<AttachmentInfo> attachments
    );
}
