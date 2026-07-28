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

using NoMercy.MediaProcessing.Files.Parsing;
using NoMercy.MediaProcessing.Files.Parsing.Adapters;

namespace NoMercy.Tests.MediaProcessing.Files;

/// <summary>
/// The real adapter set, for tests that build a <c>FileManager</c>. Parsing a
/// name is pure logic with no IO, so a mock here would only hide what the scan
/// actually decides a file is.
/// </summary>
internal static class TestFilenameParser
{
    public static IFilenameParserPipeline Default =>
        new FilenameParserPipeline(
            new IFilenameParseAdapter[]
            {
                new EpisodePrefixAdapter(),
                new EpisodeWordAdapter(),
                new CrossFormatAdapter(),
                new SeasonEpisodeAdapter(),
                new SeasonSpecialAdapter(),
                new AnimeAbsoluteAdapter(),
                new EpisodeShortFormAdapter(),
                new SpecialsAdapter(),
                new MovieDetectorAdapter(),
            }
        );
}
