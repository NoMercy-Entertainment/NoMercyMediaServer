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

using NoMercy.Encoder.Commands;

namespace NoMercy.Encoder.BuildingBlocks;

public interface IFontExtractor
{
    FfmpegCommand BuildExtractionCommand(
        string ffmpegPath,
        string inputPath,
        string outputDirectory
    );

    Task WriteFontManifestAsync(string outputDirectory, CancellationToken ct);
}
