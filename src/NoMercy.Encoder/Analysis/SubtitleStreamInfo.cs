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

using NoMercy.Encoder.Subtitles;

namespace NoMercy.Encoder.Analysis;

public record SubtitleStreamInfo(
    int Index,
    string Codec,
    string? Language,
    bool IsDefault,
    bool IsForced,
    string? Title = null
)
{
    // Delegates to SubtitleClassifier so the text/bitmap classification has
    // a single source of truth. Previously the TextCodecs set was duplicated
    // here and in SubtitleClassifier, which is exactly the kind of split-table
    // bug the consolidation guards against.
    public bool IsTextBased => SubtitleClassifier.IsTextBased(codec: Codec);
}
