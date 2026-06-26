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
using NoMercy.Encoder.Output;
using NoMercy.Encoder.PostProcess;

namespace NoMercy.Encoder.BuildingBlocks;

public interface ISubtitleExtractor
{
    SubtitleOutputInfo ResolveOutput(
        SubtitleOutputPlan plan,
        SubtitleStreamInfo stream,
        string outputDirectory,
        string mediaTitle
    );

    string ResolvePlaylistUri(
        SubtitleOutputPlan plan,
        SubtitleStreamInfo stream,
        string mediaTitle
    );
}
